import os
import uvicorn
import asyncio
import re
import numpy as np
import urllib.parse
import base64 
import torch  
import clip   
from PIL import Image 
import io 
from typing import Optional, List, TypedDict

from fastapi import FastAPI, HTTPException
from fastapi.responses import StreamingResponse
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

# ===== LANGCHAIN & LANGGRAPH =====
from langchain_huggingface import HuggingFaceEmbeddings
from langchain_community.vectorstores import FAISS
from langchain_ollama import ChatOllama
from langchain_core.prompts import ChatPromptTemplate, MessagesPlaceholder
from langchain_core.output_parsers import StrOutputParser, JsonOutputParser
from langchain_community.chat_message_histories import ChatMessageHistory

from langgraph.graph import StateGraph, END, START

# ===== OTHER =====
from rank_bm25 import BM25Okapi
from sentence_transformers import CrossEncoder
from sqlalchemy import create_engine, text

app = FastAPI()
app.add_middleware(CORSMiddleware, allow_origins=["*"], allow_credentials=True, allow_methods=["*"], allow_headers=["*"])

DB_PATH = "./faiss_db_local"
DB_CONNECTION_STRING = "mssql+pyodbc://HOA230969\\SQLEXPRESS/QuanLyPhuPham?driver=ODBC+Driver+17+for+SQL+Server&trusted_connection=yes&TrustServerCertificate=yes"

print("⏳ Đang khởi động Server AI (BẢN FIX LỖI MẤT BUTTON SUGGESTION)...")

# --- GLOBAL VARIABLES ---
vector_db = None
llm = None
sql_engine = None   
embedding_model = None
bm25 = None
docs_list = None
reranker = None
semantic_cache = [] 

# ==============================================================================
# CẤU HÌNH CLIP: IMAGE-TO-IMAGE MATCHING
# ==============================================================================
device = "cuda" if torch.cuda.is_available() else "cpu"
reference_image_features = None
reference_labels = []

try:
    print(f"⏳ Đang tải CLIP [ViT-L/14] lên thiết bị: {device}...")
    model_clip, preprocess = clip.load("ViT-L/14", device=device)
    BASE_DIR = os.path.dirname(os.path.abspath(__file__))
    REF_IMAGE_DIR = os.path.join(BASE_DIR, "KnowledgeBase", "ReferenceImages")
    if os.path.exists(REF_IMAGE_DIR):
        features_list = []
        for filename in os.listdir(REF_IMAGE_DIR):
            if filename.lower().endswith(('.png', '.jpg', '.jpeg', '.webp')):
                label = os.path.splitext(filename)[0].lower()
                try:
                    img = Image.open(os.path.join(REF_IMAGE_DIR, filename)).convert("RGB")
                    img_input = preprocess(img).unsqueeze(0).to(device)
                    with torch.no_grad():
                        feat = model_clip.encode_image(img_input)
                        feat /= feat.norm(dim=-1, keepdim=True)
                        features_list.append(feat)
                        reference_labels.append(label)
                except: pass
        if features_list:
            reference_image_features = torch.cat(features_list)
            print("✅ Đã mã hóa ảnh mẫu thành công.")
except: model_clip = None

def classify_image_clip_by_image(base64_str):
    if not model_clip or reference_image_features is None: return None
    try:
        image_data = base64.b64decode(base64_str)
        user_image = Image.open(io.BytesIO(image_data)).convert("RGB")
        user_image_input = preprocess(user_image).unsqueeze(0).to(device)
        with torch.no_grad():
            user_feature = model_clip.encode_image(user_image_input)
            user_feature /= user_feature.norm(dim=-1, keepdim=True)
            similarity = (user_feature @ reference_image_features.T).squeeze(0)
            idx = similarity.argmax().item()
            if similarity[idx].item() < 0.5: return None
        return reference_labels[idx]
    except: return None

# ==============================================================================
# HỆ THỐNG CACHE 95% & LỊCH SỬ SQL
# ==============================================================================
def get_from_cache(query: str):
    if not semantic_cache or not embedding_model: return None
    try:
        query_vector = np.array(embedding_model.embed_query(query))
        best_score = 0
        best_item = None
        for item in semantic_cache:
            dot_product = np.dot(query_vector, item["vector"])
            score = dot_product / (np.linalg.norm(query_vector) * np.linalg.norm(item["vector"]))
            if score > best_score:
                best_score = score
                best_item = item
        if best_score >= 0.97: return best_item
        return None
    except: return None

def add_to_cache(query: str, sql_ctx: str, rag_ctx: str, sources: list):
    try:
        if embedding_model:
            vector = np.array(embedding_model.embed_query(query))
            semantic_cache.append({
                "question": query, 
                "vector": vector, 
                "sql_context": sql_ctx, 
                "rag_context": rag_ctx,
                "source_display": sources
            })
            if len(semantic_cache) > 2000: semantic_cache.pop(0)
    except: pass

def preload_cache_from_sql():
    if not sql_engine or not embedding_model: return
    print("✅ Bỏ qua load Cache SQL (Bảo vệ luồng Context Cache mới).")

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
    except: pass
    return history

# ==============================================================================
# KHỞI TẠO CƠ SỞ DỮ LIỆU & HÀM SQL/RAG
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
        llm = ChatOllama(model="llama3.1:8b", temperature=0) 
        sql_engine = create_engine(DB_CONNECTION_STRING)
        print("✅ Kết nối SQL và FAISS thành công!")
        preload_cache_from_sql()
    except Exception as e: print(f"❌ Lỗi khởi động DB: {e}")

def query_products_multi(keywords: list, get_all: bool = False):
    if not sql_engine: return ""
    final_result = []
    
    if get_all:
        query_template = text("SELECT TOP 30 sp.TenSanPham, sp.Gia, sp.MoTa, COALESCE(SUM(ltk.KhoiLuongConLai), 0) FROM SanPhams sp LEFT JOIN LoTonKhos ltk ON sp.M_SanPham = ltk.M_SanPham GROUP BY sp.M_SanPham, sp.TenSanPham, sp.Gia, sp.MoTa ORDER BY sp.TenSanPham")
        try:
            with sql_engine.connect() as conn:
                for row in conn.execute(query_template).fetchall(): final_result.append(f"- {row[0]} | Đơn giá: {int(row[1]) if row[1] else 0} VNĐ/1kg | Tồn kho: {int(row[3])} kg")
            return "[[[ DỮ LIỆU SỐ LIỆU TỪ SQL (DANH SÁCH SẢN PHẨM HIỆN CÓ) ]]]\n" + "\n".join(final_result) + "\n"
        except: return ""

    seen_products = set()
    try:
        with sql_engine.connect() as conn:
            for kw in keywords:
                kw_clean = kw.lower().replace("giá", "").strip()
                if not kw_clean: continue
                
                sub_keywords = kw_clean.split()
                conditions = " AND ".join([f"sp.TenSanPham LIKE :kw{i}" for i in range(len(sub_keywords))])
                params = {f"kw{i}": f"%{word}%" for i, word in enumerate(sub_keywords)}
                
                query_template = text(f"""
                    SELECT TOP 5 sp.TenSanPham, sp.Gia, sp.MoTa, COALESCE(SUM(ltk.KhoiLuongConLai), 0) 
                    FROM SanPhams sp 
                    LEFT JOIN LoTonKhos ltk ON sp.M_SanPham = ltk.M_SanPham 
                    WHERE {conditions} 
                    GROUP BY sp.M_SanPham, sp.TenSanPham, sp.Gia, sp.MoTa
                """)
                
                res = conn.execute(query_template, params).fetchall()
                if res:
                    for row in res:
                        if row[0] not in seen_products:
                            final_result.append(f"- Tên sản phẩm: {row[0]} | Đơn giá: {int(row[1]) if row[1] else 0} VNĐ/1kg | Tồn kho hiện tại: {int(row[3])} kg")
                            seen_products.add(row[0])
                            
        if not final_result: return ""
        return "[[[ DỮ LIỆU SỐ LIỆU TỪ SQL (BẮT BUỘC DÙNG ĐỂ TÍNH TIỀN/BÁO GIÁ) ]]]\n" + "\n".join(final_result) + "\n"
    except Exception as e:
        print(f"Lỗi SQL Fuzzy: {e}")
        return ""

def query_warehouses():
    if not sql_engine: return ""
    final_result = []
    q = text("SELECT TenKho, DiaChi, SucChuaTomTat, TrangThai, TenLoaiKho FROM KhoHangs WHERE TrangThai != N'Bảo trì'")
    try:
        with sql_engine.connect() as conn:
            for row in conn.execute(q).fetchall(): final_result.append(f"- **{row[0]}** | Loại kho: {row[4]} | Sức chứa: {row[2]} | Tình trạng: {row[3]} | Địa chỉ: {row[1]}")
        if not final_result: return ""
        return "[[[ DỮ LIỆU KHO HÀNG TỪ SQL (DÙNG ĐỂ TƯ VẤN LƯU TRỮ/GỬI HÀNG CHO KHÁCH) ]]]\n" + "\n".join(final_result) + "\n"
    except: return ""

def hybrid_search_multi(queries: list, k: int = 2):
    try:
        all_candidates = []
        for q in queries:
            tokenized = tokenize(q)
            bm25_scores = bm25.get_scores(tokenized)
            bm25_docs = [docs_list[i] for i in sorted(range(len(bm25_scores)), key=lambda i: bm25_scores[i], reverse=True)[:k*2]]
            vector_docs = vector_db.similarity_search(q, k=k*2)
            all_candidates.extend(vector_docs + bm25_docs)

        unique_docs = list({d.page_content: d for d in all_candidates}.values())
        if not unique_docs: return []
        
        main_query = queries[0]
        pairs = [[main_query, d.page_content] for d in unique_docs]
        scores = reranker.predict(pairs)
        
        sorted_docs = [d for s, d in sorted(zip(scores, unique_docs), key=lambda x: x[0], reverse=True)]
        
        final_docs = []
        for doc in sorted_docs:
            is_dup = False
            for f_doc in final_docs:
                if doc.page_content[:60] in f_doc.page_content or f_doc.page_content[:60] in doc.page_content:
                    is_dup = True
                    break
            if not is_dup:
                final_docs.append(doc)
            if len(final_docs) >= k:
                break
                
        return final_docs
    except: return []

# ==============================================================================
# EXTRACTOR JSON
# ==============================================================================
extractor_system = """Nhiệm vụ: Phân tích câu hỏi người dùng.
1. "products_to_buy": Tên sản phẩm khách muốn MUA (TUYỆT ĐỐI KHÔNG BAO GỒM CÁC TỪ CHỈ SỐ LƯỢNG NHƯ: kg, tấn, tạ, lít, 10, 100... Chỉ lấy đúng tên. VD: "100kg trấu nghiền" -> "trấu nghiền").
2. "products_to_sell": Tên sản phẩm khách muốn BÁN chỉ lấy giá trong sql  (CŨNG TUYỆT ĐỐI KHÔNG LẤY SỐ LƯỢNG).
3. ĐÁNH DẤU GET_ALL: Nếu hỏi "bao nhiêu sản phẩm", "danh sách", "tất cả mặt hàng", "mạt hang", "đồ bán" -> is_get_all = true.
4. ĐÁNH DẤU TƯ VẤN KHO: Hỏi "kho nào", "gửi ở đâu" -> is_warehouse_query = true.
5. MỞ RỘNG TỪ KHÓA (Synonym): Liệt kê thêm các từ đồng nghĩa, biến thể của sản phẩm vào "expanded_queries" (VD: "trấu" -> ["vỏ lúa", "vỏ trấu", "trấu tươi"]).
6. ROUTE: Nếu khách CẦN HỎI ĐỊNH NGHĨA/CÔNG DỤNG thì xuất "rag". Nếu khách CHỈ HỎI GIÁ/MUA/BÁN/KHO thì xuất "sql". Nếu hỏi hỗn hợp cả hai thì xuất "both".

Output JSON:
{{
    "route": "sql/rag/both",
    "products_to_buy": ["tên sp"],
    "products_to_sell": ["tên sp"],
    "is_get_all": false,
    "is_warehouse_query": false,
    "expanded_queries": ["từ đồng nghĩa 1"]
}}"""
extractor_chain = ChatPromptTemplate.from_messages([("system", extractor_system), ("human", "{input}")]) | llm | JsonOutputParser()

# ==============================================================================
# PROMPT GỐC CỦA BẠN (ĐÃ FIX LỖI ÉP BUỘC NGOẶC VUÔNG SUGGESTION)
# ==============================================================================
qa_system_prompt = """Bạn là Chuyên viên Tư vấn & Bán hàng xuất sắc của website Thu Gom Phụ Phẩm Nông Nghiệp. 

🛑 LỆNH TỐI CAO CHỐNG ẢO GIÁC:
1. Bạn KHÔNG ĐƯỢC PHÉP bịa đặt thông tin. Nếu trong DỮ LIỆU CỦA BẠN bên dưới không có thông tin về sản phẩm khách hỏi, BẮT BUỘC trả lời: "Xin lỗi, hiện tại hệ thống chưa có dữ liệu về thông tin này." (Ngoại trừ trường hợp khách muốn BÁN sản phẩm, hãy luôn sử dụng DỮ LIỆU THU GOM để tư vấn).
2. TUYỆT ĐỐI KHÔNG lấy định nghĩa của sản phẩm này đắp vào sản phẩm khác. 
3. Nếu câu hỏi không liên quan đến nông nghiệp hoặc kho bãi → Trả lời: "Câu hỏi ngoài phạm vi dữ liệu."

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
2. DANH SÁCH SẢN PHẨM: Nếu người dùng hỏi có bao nhiêu sản phẩm, hãy liệt kê dựa trên dữ liệu từ SQL cung cấp.3. KHÁCH MUỐN BÁN CHO HỆ THỐNG: Nếu khách hỏi "tôi muốn bán...", "bán được bao nhiêu", BẠN TUYỆT ĐỐI KHÔNG DÙNG GIÁ TRONG SQL ĐỂ TRẢ LỜI. Bạn BẮT BUỘC trả lời tư vấn như sau: "Giá thu mua phụ phẩm (như trấu, vỏ cà phê...) sẽ thay đổi tùy thuộc vào độ ẩm, tạp chất và chất lượng thực tế của sản phẩm. Để biết chính xác giá thu mua, quý khách vui lòng truy cập vào trang **Thu Gom** trên hệ thống của chúng tôi. Tại đó sẽ có AI chuyên biệt phân tích và định giá chính xác lô hàng của quý khách."
4. TƯ VẤN SẢN PHẨM: Bạn hãy đọc nội dung nằm trong mục [[[ KIẾN THỨC TỪ PDF ]]] (nếu có) để tư vấn "Nó là gì", "Dùng để làm gì". Chú ý xem kỹ thẻ [Thông tin từ file: ...] ở mỗi đoạn văn để biết nội dung đó thuộc sản phẩm nào.
5. TƯ VẤN KHO CHỨA HÀNG: Nếu khách có nhu cầu gửi hàng/lưu trữ (ví dụ "tôi có 50 tấn... kho nào hợp"), HÃY XEM MỤC [[[ DỮ LIỆU KHO HÀNG TỪ SQL ]]]. Chọn và gợi ý 1-2 kho có Trạng thái "Còn trống", Sức chứa đáp ứng đủ nhu cầu của họ và ghi rõ địa chỉ để khách đem tới.
6. KÊU GỌI HÀNH ĐỘNG: Nếu khách mua hàng, luôn kêu gọi khách "Thêm vào giỏ hàng" hoặc "Mua trên website".
7. VĂN PHONG: Chuyên nghiệp, ngắn gọn.
✅ CÁCH TRẢ LỜI:
- Để báo GIÁ BÁN và số lượng TỒN KHO, bạn phải LẤY CÁC CON SỐ nằm trong mục [[[ DỮ LIỆU SỐ LIỆU TỪ SQL ]]] (nếu có). Không được in ra cái tên thẻ này.
- Để tư vấn CÔNG DỤNG, bạn hãy đọc nội dung nằm trong mục [[[ KIẾN THỨC TỪ PDF ]]] (nếu có).
- Để tư vấn KHO BÃI, hãy tìm các kho đang "Còn trống" trong mục [[[ DỮ LIỆU KHO HÀNG TỪ SQL ]]] để gợi ý.
- Cuối câu trả lời, luôn kêu gọi khách "Thêm vào giỏ hàng" hoặc "Mua trên website".
Sau khi trả lời xong, BẮT BUỘC tạo 3 câu hỏi ngắn gọn để gợi ý cho người dùng.
Để hệ thống web tạo thành Button, bạn PHẢI in các câu gợi ý này ở phần CUỐI CÙNG của câu trả lời, sử dụng CHÍNH XÁC định dạng thẻ [SUGGESTION] như sau:
[SUGGESTION] Gợi ý câu hỏi 1
[SUGGESTION] Gợi ý câu hỏi 2
[SUGGESTION] Gợi ý câu hỏi 3

DỮ LIỆU CỦA BẠN ĐỂ TRẢ LỜI CHO LƯỢT NÀY:
{context}"""
qa_chain = ChatPromptTemplate.from_messages([
    ("system", qa_system_prompt), 
    MessagesPlaceholder("chat_history"), 
    ("human", "{input}")
]) | llm | StrOutputParser()


# ==============================================================================
# LANGGRAPH STATE & WORKFLOW
# ==============================================================================
class AgentState(TypedDict):
    question: str
    original_question: str
    has_image: bool
    image_base64: Optional[str]
    
    route: str
    products_to_buy: List[str]
    products_to_sell: List[str]
    is_get_all: bool
    is_warehouse: bool
    is_selling: bool
    queries_to_search: List[str]
    
    sql_context: str
    rag_context: str
    source_display: List[str]
    status_messages: List[str]
    final_prompt_query: str

async def node_process_image(state: AgentState):
    msg = []
    if state["has_image"]:
        tag = await asyncio.to_thread(classify_image_clip_by_image, state["image_base64"])
        if tag:
            msg.append(f"<div class='ai-status'>✅ AI nhận diện ảnh là: **{tag.upper()}**</div>\n")
            state["question"] = f"Tôi muốn tư vấn về {tag}. {state['question']}"
        else:
            msg.append("<div class='ai-status'>⚠️ Không thể nhận diện hình ảnh.</div>\n")
    return {"question": state["question"], "status_messages": msg}

async def node_extract_intent(state: AgentState):
    msg = ["<div class='ai-status'>🤖 AI Router đang phân tích ý định của bạn...</div>\n"]
    q = state["question"]
    p_buy, p_sell, queries = [], [], [q]
    get_all, warehouse, selling = False, False, False
    route_val = "both"
    
    try:
        ext = await extractor_chain.ainvoke({"input": q})
        route_val = ext.get("route", "both")
        b = ext.get("products_to_buy", [])
        s = ext.get("products_to_sell", [])
        p_buy = [b] if isinstance(b, str) else b
        p_sell = [s] if isinstance(s, str) else s
        get_all = ext.get("is_get_all", False)
        warehouse = ext.get("is_warehouse_query", False)
        if p_sell: selling = True 
        exp = ext.get("expanded_queries", [])
        queries.extend(exp if isinstance(exp, list) else [])
    except: pass

    if any(k in q.lower() for k in ["muốn bán", "tôi bán", "bán được", "thu mua"]): selling = True
    if any(k in q.lower() for k in ["tất cả", "danh sách", "có những loại nào", "liệt kê", "mặt hàng", "mạt hang", "đồ bán trong cửa hàng", "đồ bán trong của hàng"]): get_all = True
    
    all_p = list(set(p_buy + p_sell))
    if not all_p and not get_all:
        clean = q.lower().replace("giá", "").replace("của", "").replace("bao nhiêu", "").replace("mua", "").replace("ở đâu", "").replace("là gì", "").replace("?", "").replace(".", "").strip()
        p_buy = [p.strip() for p in clean.split(" và ") if p.strip()]

    if selling or get_all or warehouse or p_buy:
        if route_val == "rag": route_val = "both"

    return {
        "route": route_val,
        "products_to_buy": p_buy, "products_to_sell": p_sell, 
        "is_get_all": get_all, "is_warehouse": warehouse, "is_selling": selling,
        "queries_to_search": queries, "status_messages": msg
    }

def router_logic(state: AgentState):
    return state["route"]

async def node_retrieve_sql(state: AgentState):
    sql_ctx = ""
    sources = state.get("source_display", [])
    msg = []
    
    if state["is_selling"]: msg.append(f"<div class='ai-status'>🤝 Đang phân tích thông tin giao dịch...</div>\n")
    
    synonyms = [kw for kw in state["queries_to_search"] if kw != state["question"]]
    all_p = list(set(state["products_to_buy"] + state["products_to_sell"] + synonyms))
    p_sql = list(set(state["products_to_buy"] + synonyms)) if state["products_to_buy"] else all_p

    if state["is_get_all"]:
        msg.append(f"<div class='ai-status'>🔍 AI truy xuất Database kho hàng...</div>\n")
        res = await asyncio.to_thread(query_products_multi, [], True)
        if res: 
            sql_ctx += res + "\n"
            sources.append("**Danh mục SP**")
    elif p_sql:
        msg.append(f"<div class='ai-status'>🔍 AI tra cứu giá Database cho {', '.join(p_sql)}...</div>\n")
        res = await asyncio.to_thread(query_products_multi, p_sql, False)
        if res: 
            sql_ctx += res + "\n"
            sources.append("**Kho hàng Website**")

    if state["is_selling"]:
        p_names = ", ".join(state["products_to_sell"]) if state["products_to_sell"] else "sản phẩm này"
        sql_ctx += f"[[[ DỮ LIỆU THU GOM SẢN PHẨM TỪ NÔNG DÂN ]]]\nThông tin về {p_names}: Hệ thống có nhận thu mua {p_names} từ người dân. Giá thu mua sẽ thay đổi tùy thuộc vào độ ẩm, tạp chất và chất lượng thực tế của sản phẩm. Để biết chính xác giá thu mua, khách hàng cần truy cập vào trang **Thu Gom** trên hệ thống. Tại đó sẽ có AI chuyên biệt phân tích và định giá chính xác lô hàng của quý khách.\n"

    if state["is_warehouse"]:
        msg.append(f"<div class='ai-status'>🏢 Đang kiểm tra sức chứa Kho bãi...</div>\n")
        res = await asyncio.to_thread(query_warehouses)
        if res: 
            sql_ctx += res + "\n"
            sources.append("**Hệ thống Kho Bãi**")

    return {"sql_context": sql_ctx, "source_display": sources, "status_messages": msg}

async def node_retrieve_pdf(state: AgentState):
    rag_ctx = ""
    sources = state.get("source_display", [])
    msg = []
    
    all_p = list(set(state["products_to_buy"] + state["products_to_sell"]))
    
    if state["is_get_all"] and not all_p:
        pass 
    else:
        msg.append(f"<div class='ai-status'>📚 AI đang tìm định nghĩa từ kho PDF...</div>\n")
        docs = await asyncio.to_thread(hybrid_search_multi, state["queries_to_search"], 2)
        if docs:
            rag_texts, links = [], []
            for doc in docs:
                f_name = os.path.basename(doc.metadata.get("source_file", "Tài liệu"))
                is_valid = False
                if not all_p: is_valid = True
                else:
                    for p in all_p:
                        if p.lower() in f_name.lower(): is_valid = True
                if is_valid:
                    rag_texts.append(f"📦 [Thông tin từ file: {f_name}]:\n{doc.page_content}")
                    p_num = doc.metadata.get("page_label", "1")
                    
                    source_text = f"📄 **{f_name}** (Trang {p_num})"
                    if source_text not in links: links.append(source_text)
            
            if rag_texts: rag_ctx = "[[[ KIẾN THỨC TỪ PDF ]]]\n" + "\n\n".join(rag_texts)
            if links: sources.extend(links)
            
    return {"rag_context": rag_ctx, "source_display": sources, "status_messages": msg}

async def node_prepare_prompt(state: AgentState):
    q = state["question"]
    math_instruction = ""
    if any(char.isdigit() for char in q) or state["is_get_all"]:
        math_instruction = " [LƯU Ý: NẾU KHÁCH HỎI MUA VỚI SỐ LƯỢNG CỤ THỂ (VD: 100kg), BẠN PHẢI LÀM TOÁN NHÂN: Lấy ĐÚNG con số khách yêu cầu NHÂN VỚI [Đơn giá/1kg từ SQL]. TUYỆT ĐỐI KHÔNG tự quy đổi đơn vị. Ghi rõ phép tính (Ví dụ: 100 x 80000 = 8000000 VNĐ) và tính tổng chi phí.]"

    final_q = q
    if state["is_selling"]:
        if state["products_to_buy"] and state["products_to_sell"]:
            final_q += f" [LƯU Ý ĐẶC BIỆT CHO AI: Khách đang muốn MUA {', '.join(state['products_to_buy'])} và BÁN {', '.join(state['products_to_sell'])}. Dùng dữ liệu SQL để báo giá Mua, và dùng dữ liệu Thu Gom để hướng dẫn món Bán.{math_instruction}]"
        else:
            final_q += " [LƯU Ý ĐẶC BIỆT CHO AI: Khách có nhắc đến việc BÁN sản phẩm. Hãy dùng DỮ LIỆU THU GOM để hướng dẫn khách sang trang Thu Gom.]"
    else:
        if math_instruction: final_q += f" [LƯU Ý ĐẶC BIỆT CHO AI:{math_instruction}]"
        
    return {"final_prompt_query": final_q, "status_messages": []}

workflow = StateGraph(AgentState)
workflow.add_node("process_image", node_process_image)
workflow.add_node("extract_intent", node_extract_intent)
workflow.add_node("retrieve_sql", node_retrieve_sql)
workflow.add_node("retrieve_pdf", node_retrieve_pdf)
workflow.add_node("prepare_prompt", node_prepare_prompt)

workflow.add_edge(START, "process_image")
workflow.add_edge("process_image", "extract_intent")

workflow.add_conditional_edges(
    "extract_intent",
    router_logic,
    {
        "sql": "retrieve_sql",   
        "rag": "retrieve_pdf",   
        "both": "retrieve_sql"   
    }
)

def post_sql_router(state: AgentState):
    if state["route"] == "both": return "retrieve_pdf"
    return "prepare_prompt" 

workflow.add_conditional_edges(
    "retrieve_sql",
    post_sql_router,
    {"retrieve_pdf": "retrieve_pdf", "prepare_prompt": "prepare_prompt"}
)

workflow.add_edge("retrieve_pdf", "prepare_prompt")
workflow.add_edge("prepare_prompt", END)

app_graph = workflow.compile()

# ==============================================================================
# API ROUTE 
# ==============================================================================
class ChatRequest(BaseModel):
    session_id: str
    question: str
    image: Optional[str] = None 

@app.post("/chat")
async def chat(req: ChatRequest):
    if not vector_db: raise HTTPException(status_code=500, detail="AI chưa sẵn sàng")
    chat_history = get_history_from_sql(req.session_id, limit=4).messages

    async def generate():
        try:
            print(f"\n=======================================================")
            has_img = bool(req.image and req.image.strip() and req.image != "null")
            
            yield "<div class='ai-status'>🚀 Hệ thống đã tiếp nhận yêu cầu...</div>\n"
            await asyncio.sleep(0.1) 
            
            if not has_img:
                cached_obj = await asyncio.to_thread(get_from_cache, req.question)
                if cached_obj:
                    print("⚡ [CACHE HIT] Lấy dữ liệu siêu tốc!")
                    yield "<div class='ai-status'>⚡ Đang trả lời từ Cache (Siêu tốc)...</div>\n"
                    
                    full_context = f"{cached_obj['sql_context']}\n\n{cached_obj['rag_context']}"
                    
                    async for chunk in qa_chain.astream({
                        "context": full_context, 
                        "chat_history": chat_history, 
                        "input": req.question
                    }):
                        yield chunk
                        if any(c.isdigit() for c in chunk):
                            await asyncio.sleep(0.05)
                        else:
                            await asyncio.sleep(0.01)

                    if cached_obj.get("source_display"):
                        sources_str = " | ".join(cached_obj["source_display"])
                        yield f"\n\n---\n*Nguồn: {sources_str}*"
                    print("✅ HOÀN THÀNH: Trả lời từ CACHE siêu tốc!")
                    return 

            initial_state = {
                "question": req.question, "original_question": req.question,
                "has_image": has_img, "image_base64": req.image if has_img else None,
                "route": "both", 
                "products_to_buy": [], "products_to_sell": [],
                "is_get_all": False, "is_warehouse": False, "is_selling": False,
                "queries_to_search": [], "sql_context": "", "rag_context": "",
                "source_display": [], "status_messages": [], "final_prompt_query": ""
            }

            final_state = initial_state
            quick_price_sent = False

            async for event in app_graph.astream(initial_state):
                for node_name, state_update in event.items():
                    final_state.update(state_update)
                    
                    for msg in state_update.get("status_messages", []):
                        yield msg
                        await asyncio.sleep(0.3) 

                    if node_name == "retrieve_sql" and final_state.get("sql_context") and not quick_price_sent:
                        quick_price_sent = True
                        raw_sql = final_state["sql_context"].split(']]]\n')[-1].strip()
                        if raw_sql:
                            yield f"\n> **📊 Thông tin nhanh từ hệ thống:**\n> {raw_sql}\n\n"
                            yield "<div class='ai-status'>✍️ AI đang tổng hợp chi tiết...</div>\n"
                            await asyncio.sleep(0.2)

            full_context = f"{final_state['sql_context']}\n\n{final_state['rag_context']}"
            
            async for chunk in qa_chain.astream({
                "context": full_context, 
                "chat_history": chat_history, 
                "input": final_state['final_prompt_query']
            }):
                yield chunk
                if any(c.isdigit() for c in chunk) or "=" in chunk:
                    await asyncio.sleep(0.06)
                else:
                    await asyncio.sleep(0.01)
            
            if final_state.get("source_display"):
                sources = " | ".join(final_state["source_display"])
                yield f"\n\n---\n*Nguồn: {sources}*"

            if not has_img: 
                await asyncio.to_thread(add_to_cache, req.question, final_state["sql_context"], final_state["rag_context"], final_state["source_display"])
            
            print("✅ HOÀN THÀNH: State-Graph Workflow trả lời thành công!")

        except Exception as e:
            print(f"❌ Lỗi: {e}")
            yield f"\n❌ Lỗi hệ thống: Vui lòng thử lại."

    return StreamingResponse(generate(), media_type="text/plain")

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=5000)