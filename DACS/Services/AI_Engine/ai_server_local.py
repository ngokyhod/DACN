import os
import uvicorn
import asyncio
import re
import json
import numpy as np
import urllib.parse
import base64 
import torch  
import clip   
from PIL import Image 
import io 
from typing import Optional
from fastapi import FastAPI, HTTPException
from fastapi.responses import StreamingResponse
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

# ===== IMPORT LANGCHAIN =====
from langchain_huggingface import HuggingFaceEmbeddings
from langchain_community.vectorstores import FAISS
from langchain_ollama import ChatOllama
from langchain_core.prompts import ChatPromptTemplate, MessagesPlaceholder
from langchain_community.chat_message_histories import ChatMessageHistory
from langchain_core.output_parsers import StrOutputParser, JsonOutputParser

# ===== BM25 & RE-RANKER =====
from rank_bm25 import BM25Okapi
from sentence_transformers import CrossEncoder

# ===== SQL TOOL =====
from sqlalchemy import create_engine, text

app = FastAPI()

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

DB_PATH = "./faiss_db_local"
print("⏳ Đang khởi động Server AI (Bản Fix Tối Hậu: RAG Thông Minh & Chống Loạn Não)...")

# --- GLOBAL VARIABLES ---
vector_db = None
llm = None
qa_chain = None
extractor_chain = None
sql_engine = None   
embedding_model = None

bm25 = None
docs_list = None
reranker = None

# ==============================================================================
# CẤU HÌNH CLIP: IMAGE-TO-IMAGE MATCHING
# ==============================================================================
device = "cuda" if torch.cuda.is_available() else "cpu"
reference_image_features = None
reference_labels = []

try:
    print(f"⏳ Đang tải mô hình CLIP [ViT-L/14] lên thiết bị: {device}...")
    model_clip, preprocess = clip.load("ViT-L/14", device=device)
    
    BASE_DIR = os.path.dirname(os.path.abspath(__file__))
    REF_IMAGE_DIR = os.path.join(BASE_DIR, "KnowledgeBase", "ReferenceImages")
    
    if os.path.exists(REF_IMAGE_DIR):
        print("⏳ Đang mã hóa các hình ảnh mẫu (Reference Images)...")
        features_list = []
        for filename in os.listdir(REF_IMAGE_DIR):
            if filename.lower().endswith(('.png', '.jpg', '.jpeg', '.webp')):
                file_path = os.path.join(REF_IMAGE_DIR, filename)
                label = os.path.splitext(filename)[0].lower()
                
                try:
                    img = Image.open(file_path).convert("RGB")
                    img_input = preprocess(img).unsqueeze(0).to(device)
                    with torch.no_grad():
                        img_feature = model_clip.encode_image(img_input)
                        img_feature /= img_feature.norm(dim=-1, keepdim=True)
                        features_list.append(img_feature)
                        reference_labels.append(label)
                except Exception as e:
                    print(f"Lỗi khi đọc ảnh mẫu {filename}: {e}")
        
        if features_list:
            reference_image_features = torch.cat(features_list)
            print(f"✅ Đã mã hóa {len(reference_labels)} ảnh mẫu thành công: {reference_labels}")
        else:
            print("⚠️ Không tìm thấy ảnh mẫu hợp lệ trong thư mục ReferenceImages.")
    else:
        print("⚠️ Không tìm thấy thư mục ReferenceImages. Vui lòng tạo thư mục này và cho ảnh mẫu vào.")
        
except Exception as e:
    print(f"⚠️ Không thể tải mô hình CLIP: {e}")
    model_clip = None

def classify_image_clip_by_image(base64_str):
    if not model_clip or reference_image_features is None: 
        return None
    try:
        image_data = base64.b64decode(base64_str)
        user_image = Image.open(io.BytesIO(image_data)).convert("RGB")
        user_image_input = preprocess(user_image).unsqueeze(0).to(device)

        with torch.no_grad():
            user_feature = model_clip.encode_image(user_image_input)
            user_feature /= user_feature.norm(dim=-1, keepdim=True)
            
            similarity = (user_feature @ reference_image_features.T).squeeze(0)
            
            best_match_idx = similarity.argmax().item()
            best_score = similarity[best_match_idx].item()
            
            if best_score < 0.5:
                return None
                
        return reference_labels[best_match_idx]
    except Exception as e:
        print(f"❌ Lỗi xử lý ảnh bằng CLIP Image-to-Image: {e}")
        return None

# ==============================================================================
# HỆ THỐNG SEMANTIC CACHING
# ==============================================================================
semantic_cache = []
CACHE_THRESHOLD = 0.95 

def get_from_cache(query: str):
    if not semantic_cache or not embedding_model: return None
    try:
        query_vector = np.array(embedding_model.embed_query(query))
        best_score = 0
        best_answer = None
        for item in semantic_cache:
            dot_product = np.dot(query_vector, item["vector"])
            norm_q = np.linalg.norm(query_vector)
            norm_i = np.linalg.norm(item["vector"])
            score = dot_product / (norm_q * norm_i)
            if score > best_score:
                best_score = score
                best_answer = item["answer"]
        if best_score >= CACHE_THRESHOLD:
            print(f"⚡ [CACHE HIT] Lấy dữ liệu siêu tốc (Độ giống: {best_score:.2f})")
            return best_answer
        return None
    except Exception as e:
        return None

def add_to_cache(query: str, answer: str):
    try:
        if embedding_model:
            vector = np.array(embedding_model.embed_query(query))
            semantic_cache.append({"question": query, "vector": vector, "answer": answer})
            if len(semantic_cache) > 2000:
                semantic_cache.pop(0)
    except Exception as e:
        pass

# ==============================================================================
# 1. CẤU HÌNH SQL (SẢN PHẨM & KHO HÀNG)
# ==============================================================================
DB_CONNECTION_STRING = "mssql+pyodbc://HOA230969\\SQLEXPRESS/QuanLyPhuPham?driver=ODBC+Driver+17+for+SQL+Server&trusted_connection=yes&TrustServerCertificate=yes"

def query_products_multi(keywords: list, get_all: bool = False):
    if not sql_engine: return ""
    final_result = []
    
    if get_all:
        query_template = text("""
            SELECT TOP 30 sp.TenSanPham, sp.Gia, sp.MoTa, COALESCE(SUM(ltk.KhoiLuongConLai), 0) as TongTonKho
            FROM SanPhams sp LEFT JOIN LoTonKhos ltk ON sp.M_SanPham = ltk.M_SanPham
            GROUP BY sp.M_SanPham, sp.TenSanPham, sp.Gia, sp.MoTa ORDER BY sp.TenSanPham
        """)
        try:
            with sql_engine.connect() as conn:
                result = conn.execute(query_template).fetchall()
                if result:
                    for row in result:
                        ten, gia, ton_kho = row[0], (int(row[1]) if row[1] else 0), int(row[3])
                        final_result.append(f"- {ten} | Đơn giá: {gia} VNĐ | Tồn kho: {ton_kho} kg")
            return "[[[ DỮ LIỆU SỐ LIỆU TỪ SQL (DANH SÁCH SẢN PHẨM HIỆN CÓ) ]]]\n" + "\n".join(final_result) + "\n"
        except: return ""

    query_template = text("""
        SELECT sp.TenSanPham, sp.Gia, sp.MoTa, COALESCE(SUM(ltk.KhoiLuongConLai), 0) as TongTonKho
        FROM SanPhams sp LEFT JOIN LoTonKhos ltk ON sp.M_SanPham = ltk.M_SanPham
        WHERE sp.TenSanPham LIKE :kw GROUP BY sp.M_SanPham, sp.TenSanPham, sp.Gia, sp.MoTa
    """)
    try:
        with sql_engine.connect() as conn:
            for kw in keywords:
                kw_clean = kw.lower().replace("giá", "").replace("tiền", "").replace("mua", "").replace("ở đâu", "").replace("?", "").replace(".", "").replace(",", "").strip()
                result = conn.execute(query_template, {"kw": f"%{kw_clean}%"}).fetchall()
                if result:
                    for row in result:
                        ten, gia, ton_kho = row[0], (int(row[1]) if row[1] else 0), int(row[3])
                        final_result.append(f"- Tên sản phẩm: {ten} | Đơn giá: {gia} VNĐ/đơn_vị | Tồn kho hiện tại: {ton_kho} kg")
        if not final_result: return ""
        return "[[[ DỮ LIỆU SỐ LIỆU TỪ SQL (BẮT BUỘC DÙNG ĐỂ TÍNH TIỀN/BÁO GIÁ) ]]]\n" + "\n".join(final_result) + "\n"
    except: return ""

def query_warehouses():
    if not sql_engine: return ""
    final_result = []
    query_template = text("SELECT TenKho, DiaChi, SucChuaTomTat, TrangThai, TenLoaiKho FROM KhoHangs WHERE TrangThai != N'Bảo trì'")
    try:
        with sql_engine.connect() as conn:
            result = conn.execute(query_template).fetchall()
            if result:
                for row in result:
                    final_result.append(f"- **{row[0]}** | Loại kho: {row[4]} | Sức chứa: {row[2]} | Tình trạng: {row[3]} | Địa chỉ: {row[1]}")
        if not final_result: return ""
        return "[[[ DỮ LIỆU KHO HÀNG TỪ SQL (DÙNG ĐỂ TƯ VẤN LƯU TRỮ/GỬI HÀNG CHO KHÁCH) ]]]\n" + "\n".join(final_result) + "\n"
    except: return ""

def preload_cache_from_sql():
    print("⏳ Đang tải trước Dữ liệu Cache từ Database...")
    if not sql_engine or not embedding_model: return
    query = text("SELECT SessionId, IsAi, Content, CreatedAt FROM AIMessages ORDER BY SessionId, CreatedAt ASC")
    try:
        with sql_engine.connect() as conn:
            rows = conn.execute(query).fetchall()
            last_question = None
            loaded_count = 0
            for row in rows:
                is_ai, content = row[1], row[2]
                if not is_ai: last_question = content
                elif is_ai and last_question: 
                    if "BẮT BUỘC DÙNG" not in content and "[SUGGESTION]" in content:
                        add_to_cache(last_question, content)
                        loaded_count += 1
                    last_question = None 
            print(f"✅ Đã nạp thành công {loaded_count} câu hỏi vào Semantic Cache!")
    except Exception as e: print(f"⚠️ Lỗi nạp Cache từ DB: {e}")

# ==============================================================================
# 2. KHỞI TẠO SERVER & PROMPTS CHUYÊN SÂU
# ==============================================================================
if os.path.exists(DB_PATH):
    try:
        embedding_model = HuggingFaceEmbeddings(model_name="all-MiniLM-L6-v2")
        vector_db = FAISS.load_local(DB_PATH, embedding_model, allow_dangerous_deserialization=True)
        docs_list = list(vector_db.docstore._dict.values())
        
        def tokenize(text: str): return re.findall(r"\w+", text.lower())
        bm25_corpus = [tokenize(doc.page_content) for doc in docs_list]
        bm25 = BM25Okapi(bm25_corpus)
        reranker = CrossEncoder('cross-encoder/ms-marco-MiniLM-L-6-v2')
        llm = ChatOllama(model="llama3", temperature=0) 
        sql_engine = create_engine(DB_CONNECTION_STRING)
        print("✅ Kết nối SQL thành công!")

        preload_cache_from_sql()

        extractor_system = """Nhiệm vụ: Phân tích câu hỏi người dùng.
        1. Tìm tên TẤT CẢ các sản phẩm cụ thể lưu vào "products". 
        2. Nếu khách hàng muốn MUA sản phẩm nào, lưu tên sản phẩm đó vào "products_to_buy".
        3. Nếu khách hàng muốn BÁN sản phẩm nào, lưu tên sản phẩm đó vào "products_to_sell".
        4. ĐÁNH DẤU GET_ALL: Nếu hỏi "bao nhiêu sản phẩm", "danh sách" thì set "is_get_all" thành true.
        5. ĐÁNH DẤU TƯ VẤN KHO: Nếu nói có số lượng lớn (vd: 50 tấn), hỏi "kho nào", "gửi ở đâu", set "is_warehouse_query" thành true.
        6. ĐÁNH DẤU BÁN HÀNG: Nếu khách muốn BÁN (có sản phẩm trong products_to_sell), set "is_selling_query" thành true.
        7. MỞ RỘNG BẰNG TIẾNG VIỆT để tìm kiếm tài liệu.
        
        Output mẫu JSON HỢP LỆ:
        {{
            "products": ["tên sp 1", "tên sp 2"],
            "products_to_buy": ["tên sp 1"],
            "products_to_sell": ["tên sp 2"],
            "is_get_all": false,
            "is_warehouse_query": false,
            "is_selling_query": true,
            "expanded_queries": ["câu tiếng việt"]
        }}
        """
        extractor_prompt = ChatPromptTemplate.from_messages([("system", extractor_system), ("human", "{input}")])
        extractor_chain = extractor_prompt | llm | JsonOutputParser()

        # ==============================================================================
        # PROMPT CỦA BẠN (TÔI CHỈ CHUYỂN "LỆNH TỐI CAO 1" XUỐNG DƯỚI CÙNG ĐỂ AI KHÔNG BỊ NGÁO)
        # ==============================================================================
        qa_system_prompt = """Bạn là Chuyên viên Tư vấn & Bán hàng xuất sắc của website Thu Gom Phụ Phẩm Nông Nghiệp. 

⚠️ QUY TẮC BẮT BUỘC:
- Chỉ được sử dụng thông tin trong phần "Thông tin tham khảo" bên dưới.
- Không được dùng kiến thức bên ngoài.
- Nếu câu hỏi không liên quan đến nông nghiệp → trả lời: "Câu hỏi ngoài phạm vi dữ liệu".
- Nếu chỉ hỏi về 1 chủ đề thì chỉ xem tài liệu của 1 chủ đề đó đùng lấn át sang chủ đề khác nếu nó không có liên quan gì đến nhau.

⚠️ NGÔN NGỮ & ĐỊNH DẠNG:
- Trả lời 100% tiếng Việt.
- Trình bày bằng Markdown, gạch đầu dòng (-) rõ ràng, dễ đọc.

BẠN CÓ 2 NGUỒN TÀI LIỆU DƯỚI ĐÂY:
1. [[[ DỮ LIỆU SỐ LIỆU TỪ SQL ]]]: Chứa Đơn giá và Tồn kho thực tế.
2. [[[ KIẾN THỨC TỪ PDF ]]]: Chứa định nghĩa, công dụng, cách làm.

❗ QUY TẮC CỰC KỲ QUAN TRỌNG:
- Mỗi mục "Thông tin về X" CHỈ được dùng để nói về X.
- TUYỆT ĐỐI KHÔNG suy diễn rằng sản phẩm này làm từ sản phẩm kia. TUYỆT ĐỐI KHÔNG lấy định nghĩa của sản phẩm này đắp vào sản phẩm khác. 

✅ CÁCH TRẢ LỜI CHO TỪNG TRƯỜNG HỢP:
1. BÁO GIÁ KHI KHÁCH MUA VÀ TỒN KHO: Bạn LẤY CÁC CON SỐ nằm trong mục [[[ DỮ LIỆU SỐ LIỆU TỪ SQL ]]] (nếu có). TUYỆT ĐỐI KHÔNG lấy giá/tỉ lệ % từ Mục PDF. Không được in ra cái tên thẻ [[[ DỮ LIỆU... ]]] này.
2. DANH SÁCH SẢN PHẨM: Nếu người dùng hỏi có bao nhiêu sản phẩm, hãy liệt kê dựa trên dữ liệu từ SQL cung cấp.
3. KHÁCH MUỐN BÁN CHO HỆ THỐNG: Nếu khách hỏi "tôi muốn bán...", "bán được bao nhiêu", BẠN TUYỆT ĐỐI KHÔNG DÙNG GIÁ TRONG SQL ĐỂ TRẢ LỜI. Bạn BẮT BUỘC trả lời tư vấn như sau: "Giá thu mua phụ phẩm (như trấu, vỏ cà phê...) sẽ thay đổi tùy thuộc vào độ ẩm, tạp chất và chất lượng thực tế của sản phẩm. Để biết chính xác giá thu mua, quý khách vui lòng truy cập vào trang **Thu Gom** trên hệ thống của chúng tôi. Tại đó sẽ có AI chuyên biệt phân tích và định giá chính xác lô hàng của quý khách."
4. TƯ VẤN SẢN PHẨM: Bạn hãy đọc nội dung nằm trong mục [[[ KIẾN THỨC TỪ PDF ]]] (nếu có) để tư vấn "Nó là gì", "Dùng để làm gì". Chú ý xem kỹ thẻ [Thông tin từ file: ...] ở mỗi đoạn văn để biết nội dung đó thuộc sản phẩm nào.
5. TƯ VẤN KHO CHỨA HÀNG: Nếu khách có nhu cầu gửi hàng/lưu trữ (ví dụ "tôi có 50 tấn... kho nào hợp"), HÃY XEM MỤC [[[ DỮ LIỆU KHO HÀNG TỪ SQL ]]]. Chọn và gợi ý 1-2 kho có Trạng thái "Còn trống", Sức chứa đáp ứng đủ nhu cầu của họ và ghi rõ địa chỉ để khách đem tới.
6. KÊU GỌI HÀNH ĐỘNG: Nếu khách mua hàng, luôn kêu gọi khách "Thêm vào giỏ hàng" hoặc "Mua trên website".

== CẤU TRÚC GỢI Ý CÂU HỎI TIẾP THEO (BẮT BUỘC Ở CUỐI CÙNG) ==
Sau khi trả lời xong, BẮT BUỘC tạo 3 câu hỏi ngắn gọn để gợi ý cho người dùng.
Để hệ thống web tạo thành Button, bạn PHẢI in các câu gợi ý này ở phần CUỐI CÙNG của câu trả lời, sử dụng CHÍNH XÁC định dạng thẻ [SUGGESTION] như sau:
[SUGGESTION] Gợi ý câu hỏi 1
[SUGGESTION] Gợi ý câu hỏi 2
[SUGGESTION] Gợi ý câu hỏi 3

🛑 LỆNH TỐI CAO CHỐNG ẢO GIÁC:
Bạn KHÔNG ĐƯỢC PHÉP bịa đặt thông tin. Nếu trong DỮ LIỆU CỦA BẠN bên dưới HOÀN TOÀN KHÔNG CÓ BẤT KỲ THÔNG TIN GÌ liên quan đến câu hỏi của khách, BẮT BUỘC trả lời: "Xin lỗi, hiện tại hệ thống chưa có dữ liệu về thông tin này." (Ngoại trừ trường hợp khách muốn BÁN sản phẩm, hãy luôn sử dụng DỮ LIỆU THU GOM để tư vấn).

DỮ LIỆU CỦA BẠN ĐỂ TRẢ LỜI CHO LƯỢT NÀY:
{context}"""
        
        qa_prompt = ChatPromptTemplate.from_messages([("system", qa_system_prompt), ("human", "{input}")])
        qa_chain = qa_prompt | llm | StrOutputParser()
        print("✅ Server sẵn sàng!")

    except Exception as e:
        print(f"❌ Lỗi khởi động: {e}")

def hybrid_search_multi(queries: list, k: int = 3):
    try:
        all_candidates = []
        for q in queries:
            fetch_k = k * 2 
            tokenized = tokenize(q)
            bm25_scores = bm25.get_scores(tokenized)
            bm25_top_idx = sorted(range(len(bm25_scores)), key=lambda i: bm25_scores[i], reverse=True)[:fetch_k]
            bm25_docs = [docs_list[i] for i in bm25_top_idx]
            vector_docs = vector_db.similarity_search(q, k=fetch_k)
            all_candidates.extend(vector_docs + bm25_docs)

        unique_docs = {d.page_content: d for d in all_candidates}
        candidates = list(unique_docs.values())
        if not candidates: return []
        
        main_query = queries[0]
        pairs = [[main_query, d.page_content] for d in candidates]
        scores = reranker.predict(pairs)
        sorted_docs = [d for s, d in sorted(zip(scores, candidates), key=lambda x: x[0], reverse=True)]
        
        return sorted_docs[:k]
    except: return []

def get_history_from_sql(session_id: str, limit: int = 4):
    history = ChatMessageHistory()
    if not sql_engine: return history
    query = text("SELECT TOP (:limit) IsAi, Content FROM AIMessages WHERE SessionID = :sid ORDER BY CreatedAt DESC")
    try:
        with sql_engine.connect() as conn:
            rows = conn.execute(query, {"sid": session_id, "limit": limit}).fetchall()
            for is_ai, content in reversed(rows):
                if is_ai: history.add_ai_message(content)
                else: history.add_user_message(content)
    except Exception as e: print(f"⚠️ Lỗi đọc AIMessages: {e}")
    return history

class ChatRequest(BaseModel):
    session_id: str
    question: str
    image: Optional[str] = None 

@app.post("/chat")
async def chat(req: ChatRequest):
    if not vector_db: raise HTTPException(status_code=500, detail="AI chưa sẵn sàng")
    
    async def generate():
        try:
            print(f"\n=======================================================")
            
            has_image = False
            if req.image and req.image.strip() and req.image != "null":
                has_image = True
            
            print(f"📨 NHẬN CÂU HỎI: '{req.question}' | Kèm ảnh: {'Có' if has_image else 'Không'}")
            
            search_query = req.question
            products = []
            products_to_buy = []
            products_to_sell = []
            is_get_all = False
            is_warehouse_query = False
            is_selling_query = False
            queries_to_search = [search_query]

            if has_image:
                yield "📸 *Đang so sánh hình ảnh với kho dữ liệu...*\n\n"
                detected_tag = await asyncio.to_thread(classify_image_clip_by_image, req.image)
                if detected_tag:
                    yield f"✅ Dựa trên dữ liệu hình ảnh mẫu, tôi nhận diện đây là: **{detected_tag.upper()}**\n\n"
                    products = [detected_tag] 
                    search_query = f"{detected_tag}. {req.question}"
                    queries_to_search = [search_query]
                else:
                    yield "⚠️ *Không thể nhận diện hình ảnh hoặc độ tin cậy quá thấp.*"
            
            if not has_image:
                cached_answer = await asyncio.to_thread(get_from_cache, req.question)
                if cached_answer:
                    for char in cached_answer:
                        yield char
                        await asyncio.sleep(0.005)
                    print("✅ HOÀN THÀNH: Trả lời từ CACHE siêu tốc!")
                    return 

            print("🤖 Bước 1: AI trích xuất từ khóa (Tách Mua & Bán)...")
            try:
                extracted = await extractor_chain.ainvoke({"input": search_query})
                
                products_extracted = extracted.get("products", [])
                if isinstance(products_extracted, str): products_extracted = [products_extracted]
                
                p_buy = extracted.get("products_to_buy", [])
                if isinstance(p_buy, str): p_buy = [p_buy]
                products_to_buy = p_buy
                
                p_sell = extracted.get("products_to_sell", [])
                if isinstance(p_sell, str): p_sell = [p_sell]
                products_to_sell = p_sell

                if not products: products = products_extracted
                is_get_all = extracted.get("is_get_all", False)
                is_warehouse_query = extracted.get("is_warehouse_query", False)
                is_selling_query = extracted.get("is_selling_query", False)
                
                if products_to_sell: is_selling_query = True 

                expanded = extracted.get("expanded_queries", [])
                if isinstance(expanded, list): queries_to_search.extend(expanded)
            except Exception as ex: print(f"   ⚠️ Lỗi Extractor: {ex}")

            sell_keywords = ["muốn bán", "tôi bán", "bán được", "thu mua"]
            if any(k in search_query.lower() for k in sell_keywords): is_selling_query = True
            list_keywords = ["tất cả", "bao nhiêu sản phẩm", "danh sách", "có những loại nào", "liệt kê"]
            if any(k in search_query.lower() for k in list_keywords): is_get_all = True
            warehouse_keywords = ["kho", "chứa", "để ở đâu", "tấn", "tạ"]
            if any(k in search_query.lower() for k in warehouse_keywords): is_warehouse_query = True
            keywords_check = ["giá", "tiền", "kho", "kg", "tổng", "mua", "ở đâu", "là gì"]
            if not products and not is_get_all and any(k in search_query.lower() for k in keywords_check):
                clean_kw = search_query.lower().replace("giá", "").replace("của", "").replace("bao nhiêu", "").replace("mua", "").replace("ở đâu", "").replace("là gì", "").replace("?", "").replace(".", "").replace("!", "").strip()
                products = [p.strip() for p in clean_kw.split(" và ") if p.strip()]

            print("🔍 Bước 2: Thu thập dữ liệu...")
            sql_context = ""
            rag_context = ""
            source_display = []

            if is_selling_query:
                yield f"🤝 *Đang phân tích thông tin giao dịch...*\n\n"
            
            products_for_sql = products_to_buy if products_to_buy else products

            if is_get_all:
                yield f"🔍 *Đang thống kê danh sách sản phẩm trong kho...*\n\n"
                sql_context_sp = await asyncio.to_thread(query_products_multi, [], True)
                if "SỐ LIỆU TỪ SQL" in sql_context_sp: 
                    sql_context += sql_context_sp + "\n"
                    source_display.append("**Danh mục SP**")
            elif products_for_sql:
                yield f"🔍 *Đang tra cứu dữ liệu Live cho: {', '.join(products_for_sql)}...*\n\n"
                sql_context_sp = await asyncio.to_thread(query_products_multi, products_for_sql, False)
                if "BẮT BUỘC DÙNG" in sql_context_sp: 
                    sql_context += sql_context_sp + "\n"
                    source_display.append("**Kho hàng Website**")

            if is_selling_query:
                p_names_sell = ", ".join(products_to_sell) if products_to_sell else "sản phẩm này"
                selling_info = "[[[ DỮ LIỆU THU GOM SẢN PHẨM TỪ NÔNG DÂN ]]]\n"
                selling_info += f"Thông tin về {p_names_sell}: Hệ thống có nhận thu mua {p_names_sell} từ người dân. Giá thu mua sẽ thay đổi tùy thuộc vào độ ẩm, tạp chất và chất lượng thực tế của sản phẩm. Để biết chính xác giá thu mua, khách hàng cần truy cập vào trang **Thu Gom** trên hệ thống. Tại đó sẽ có AI chuyên biệt phân tích và định giá chính xác lô hàng của quý khách.\n"
                sql_context += selling_info

            if is_warehouse_query:
                yield f"🏢 *Đang tìm kiếm hệ thống kho bãi phù hợp...*\n\n"
                warehouse_context = await asyncio.to_thread(query_warehouses)
                if "DỮ LIỆU KHO HÀNG" in warehouse_context:
                    sql_context += warehouse_context + "\n"
                    if "**Hệ thống Kho Bãi**" not in source_display: source_display.append("**Hệ thống Kho Bãi**")

            # ==============================================================================
            # [ĐÃ FIX LỖI "TẤT CẢ MẶT HÀNG"]: CHẶN RAG KHÔNG CHO TÌM PDF LINH TINH
            # ==============================================================================
            # Chỉ chạy RAG khi thật sự có tên một sản phẩm cụ thể, tránh việc AI lấy hết PDF ra đọc
            if is_get_all and not products:
                print("   ⚙️ Bỏ qua RAG (PDF) vì khách hỏi chung danh sách tất cả sản phẩm.")
                docs = []
            else:
                print(f"   ⚙️ Đang chạy RAG cho các câu: {queries_to_search}")
                docs = await asyncio.to_thread(hybrid_search_multi, queries_to_search, 3)
            
            if docs:
                rag_texts = []
                links_list = []
                for doc in docs:
                    full_path = doc.metadata.get("source_file", "Tài liệu")
                    f_name = os.path.basename(full_path)
                    
                    is_valid_doc = False
                    if not products: is_valid_doc = True
                    else:
                        for p in products:
                            if p.lower() in f_name.lower(): is_valid_doc = True
                    
                    if is_valid_doc:
                        rag_texts.append(f"📦 [Thông tin từ file: {f_name}]:\n{doc.page_content}")
                        p = doc.metadata.get("page_label", "1")
                        encoded_f_name = urllib.parse.quote(f_name)
                        words = doc.page_content.split()
                        if len(words) > 10:
                            prefix = " ".join(words[:5])
                            suffix = " ".join(words[-5:])
                            text_fragment = f"{urllib.parse.quote(prefix)},{urllib.parse.quote(suffix)}"
                        else:
                            text_fragment = urllib.parse.quote(" ".join(words))

                        link_html = f'<a href="/pdfs/{encoded_f_name}#page={p}&:~:text={text_fragment}" target="_blank" style="color:#007bff; text-decoration:none;">📄 <b>{f_name}</b> (Trang {p})</a>'
                        if link_html not in links_list: links_list.append(link_html)

                if rag_texts:
                    rag_context = "[[[ KIẾN THỨC TỪ PDF (TUYỆT ĐỐI KHÔNG LẤY GIÁ TRONG NÀY) ]]]\n" + "\n\n".join(rag_texts)
                if links_list:
                    source_display.append(" | ".join(links_list))

            print("✍️ Bước 3: AI đang sinh câu trả lời...")
            full_context = f"{sql_context}\n\n{rag_context}"
            
            final_prompt_query = search_query
            if is_selling_query:
                if products_to_buy and products_to_sell:
                    final_prompt_query += f" [LƯU Ý CHO AI: Khách đang muốn MUA {', '.join(products_to_buy)} và BÁN {', '.join(products_to_sell)}. Hãy dùng dữ liệu SQL để báo giá Mua, và dùng dữ liệu Thu Gom để hướng dẫn món Bán.]"
                else:
                    final_prompt_query += " [LƯU Ý CHO AI: Khách có nhắc đến việc BÁN sản phẩm. Hãy dùng DỮ LIỆU THU GOM để hướng dẫn khách sang trang Thu Gom.]"

            full_answer = ""
            async for chunk in qa_chain.astream({"context": full_context, "input": final_prompt_query}):
                full_answer += chunk
                yield chunk
            
            if source_display:
                full_answer += f"\n\n---\n*Nguồn: {' | '.join(source_display)}*"
                yield f"\n\n---\n*Nguồn: {' | '.join(source_display)}*"

            # 5. LƯU CACHE 
            if "BẮT BUỘC DÙNG" not in sql_context and "DANH SÁCH" not in sql_context and "KHO HÀNG" not in sql_context and not has_image:
                await asyncio.to_thread(add_to_cache, req.question, full_answer)
                print("💾 Bước 4: Đã lưu câu trả lời vào Cache.")
            else:
                print("⏩ Bước 4: Bỏ qua Cache.")
            
            print("✅ HOÀN THÀNH: Trả lời thành công!")

        except Exception as e:
            print(f"❌ Lỗi cực mạnh: {e}")
            yield f"\n❌ Lỗi hệ thống: Vui lòng thử lại."

    return StreamingResponse(generate(), media_type="text/plain")

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=5000)