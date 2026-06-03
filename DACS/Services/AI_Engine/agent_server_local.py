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
import json                  # [THÊM] Để xử lý dữ liệu API
import urllib.request        # [THÊM] Để gọi API Thời tiết
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

print("⏳ Đang khởi động Server AI với QWEN 2.5 (BẢN FIX LỖI ẢO GIÁC HẠT DƯA HẤU)...")

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
# [THÊM] HÀM GỌI THỜI TIẾT TỰ ĐỘNG DỊCH ĐỊA CHỈ
# ==============================================================================
def get_current_weather(location_name: str):
    if not location_name:
        return ""
    try:
        # 1. Định vị tọa độ
        geo_url = f"https://geocoding-api.open-meteo.com/v1/search?name={urllib.parse.quote(location_name)}&count=1&language=en&format=json"
        req_geo = urllib.request.Request(geo_url, headers={'User-Agent': 'Mozilla/5.0'})
        with urllib.request.urlopen(req_geo, timeout=3) as resp_geo:
            geo_data = json.loads(resp_geo.read().decode())
            if not geo_data.get("results"):
                return f"Không xác định được tọa độ của '{location_name}'."
            lat = geo_data["results"][0]["latitude"]
            lon = geo_data["results"][0]["longitude"]
            loc_found = geo_data["results"][0]["name"]

        # 2. Lấy thời tiết
        weather_url = f"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current_weather=true"
        req_w = urllib.request.Request(weather_url, headers={'User-Agent': 'Mozilla/5.0'})
        with urllib.request.urlopen(req_w, timeout=3) as resp_w:
            w_data = json.loads(resp_w.read().decode())
            cw = w_data.get("current_weather", {})
            temp = cw.get("temperature", "")
            code = cw.get("weathercode", 0)
            
            weather_desc = "Trời quang mây tạnh, có nắng"
            if code in [51,53,55,61,63,65,80,81,82]: weather_desc = "Đang có mưa, độ ẩm cao"
            elif code in [95,96,99]: weather_desc = "Có bão sấm chớp, mưa lớn"
            elif code in [1,2,3]: weather_desc = "Trời nhiều mây, râm mát"
            
            return f"Thời tiết tại {loc_found} hiện tại: {temp}°C, {weather_desc}"
    except Exception as e:
        return "Hệ thống thời tiết đang bận."

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
        if best_score >= 0.95: return best_item
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
        embedding_model = HuggingFaceEmbeddings(model_name="paraphrase-multilingual-MiniLM-L12-v2")
        vector_db = FAISS.load_local(DB_PATH, embedding_model, allow_dangerous_deserialization=True)
        docs_list = list(vector_db.docstore._dict.values())
        def tokenize(text: str): return re.findall(r"\w+", text.lower())
        bm25_corpus = [tokenize(doc.page_content) for doc in docs_list]
        bm25 = BM25Okapi(bm25_corpus)
        
        # [ĐÃ ĐỔI SANG QWEN 2.5] - Model xuất sắc nhất cho đọc hiểu số liệu và PDF
        llm = ChatOllama(model="qwen2.5:7b", temperature=0) 
        
        sql_engine = create_engine(DB_CONNECTION_STRING)
        print("✅ Kết nối SQL và FAISS thành công!")
        preload_cache_from_sql()
    except Exception as e: print(f"❌ Lỗi khởi động DB: {e}")

# ==============================================================================
# [BẮT BUỘC GIỮ NGUYÊN] MẢNG CROP_TO_BYPRODUCT
# ==============================================================================
CROP_TO_BYPRODUCT = {
    "Rice": {"byproduct": "trấu", "rate": 0.2},
    "Sugar cane": {"byproduct": "bã mía", "rate": 0.3},
    "Coconuts, in shell": {"byproduct": "xơ dừa", "rate": 0.35},
    "Cassava, fresh": {"byproduct": "vỏ sắn", "rate": 0.15},
    "Coffee, green": {"byproduct": "vỏ cà phê", "rate": 0.4},
    "Maize (corn)": {"byproduct": "lõi ngô", "rate": 0.25}
}

def process_fao_csv(csv_path, filename):
    print(f"   - 📊 Đang xử lý file CSV FAO: {filename}")
    docs = []
    # TỪ ĐIỂN DỊCH: Map tên FAO sang Tiếng Việt để AI tìm thấy ngay lập tức
    ITEM_MAP = {
        "Rice": "lúa gạo",
        "Sugar cane": "mía",
        "Coconuts, in shell": "dừa",
        "Cassava, fresh": "sắn hoặc khoai mì",
        "Coffee, green": "cà phê",
        "Maize (corn)": "ngô hoặc bắp" # Thêm từ khóa tiếng Việt ở đây
    }
    
    try:
        import pandas as pd
        df = pd.read_csv(csv_path)
        df_prod = df[df['Element'] == 'Production']
            
        for index, row in df_prod.iterrows():
            item_name = row.get('Item', '')
            year = row.get('Year', '')
            value = float(row.get('Value', 0))
            
            # Kiểm tra xem mặt hàng có trong map không
            if item_name in ITEM_MAP or item_name in CROP_TO_BYPRODUCT:
                vn_name = ITEM_MAP.get(item_name, item_name)
                byproduct_info = CROP_TO_BYPRODUCT.get(item_name, {"byproduct": "phụ phẩm", "rate": 0.1})
                
                # CÂU VĂN BẢN ĐƯỢC TỐI ƯU ĐỂ AI KHÔNG BỊ "MÙ" DỮ LIỆU
                content = (f"Thông tin sản lượng vĩ mô: Vào năm {year}, tổng sản lượng {vn_name} ({item_name}) tại Việt Nam đạt {value:,.0f} tấn. "
                           f"Tiềm năng thu gom phụ phẩm {byproduct_info['byproduct']} từ {vn_name} là {value * byproduct_info['rate']:,.0f} tấn. "
                           f"Nguồn dữ liệu: FAOSTAT.")
                
                from langchain_core.documents import Document
                doc = Document(
                    page_content=content,
                    metadata={"source_file": filename, "type": "fao_data", "year": year}
                )
                docs.append(doc)
    except Exception as e:
        print(f"      ❌ Lỗi CSV: {e}")
    return docs

def query_products_multi(keywords: list, get_all: bool = False):
    if not sql_engine: return ""
    final_result = []
    
    if get_all:
        query_template = text("SELECT TOP 30 sp.M_SanPham, sp.TenSanPham, sp.Gia, sp.MoTa, COALESCE(SUM(ltk.KhoiLuongConLai), 0) FROM SanPhams sp LEFT JOIN LoTonKhos ltk ON sp.M_SanPham = ltk.M_SanPham GROUP BY sp.M_SanPham, sp.TenSanPham, sp.Gia, sp.MoTa ORDER BY sp.TenSanPham")
        try:
            with sql_engine.connect() as conn:
                for row in conn.execute(query_template).fetchall(): final_result.append(f"- [Mã: {row[0]}] {row[1]} | Đơn giá: {int(row[2]) if row[2] else 0} VNĐ/1kg | Tồn kho: {int(row[4])} kg")
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
                    SELECT TOP 5 sp.M_SanPham, sp.TenSanPham, sp.Gia, sp.MoTa, COALESCE(SUM(ltk.KhoiLuongConLai), 0) 
                    FROM SanPhams sp 
                    LEFT JOIN LoTonKhos ltk ON sp.M_SanPham = ltk.M_SanPham 
                    WHERE {conditions} 
                    GROUP BY sp.M_SanPham, sp.TenSanPham, sp.Gia, sp.MoTa
                """)
                
                res = conn.execute(query_template, params).fetchall()
                if res:
                    for row in res:
                        if row[1] not in seen_products:
                            final_result.append(f"- [Mã: {row[0]}] Tên sản phẩm: {row[1]} | Đơn giá: {int(row[2]) if row[2] else 0} VNĐ/1kg | Tồn kho hiện tại: {int(row[4])} kg")
                            seen_products.add(row[1])
                            
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

def hybrid_search_multi(queries: list, k: int = 5):
    try:
        all_candidates = []
        for q in queries:
            tokenized = tokenize(q)
            bm25_scores = bm25.get_scores(tokenized)
            bm25_docs = [docs_list[i] for i in sorted(range(len(bm25_scores)), key=lambda i: bm25_scores[i], reverse=True)[:k]]
            vector_docs = vector_db.similarity_search(q, k=k)
            all_candidates.extend(vector_docs + bm25_docs)

        unique_docs = list({d.page_content: d for d in all_candidates}.values())
        return unique_docs[:k+2]
    except Exception as e: 
        return []

# ==============================================================================
# EXTRACTOR JSON
# ==============================================================================
extractor_system = """Nhiệm vụ: Phân tích câu hỏi người dùng.
1. "products_to_buy": Tên nông sản khách muốn MUA. TUYỆT ĐỐI KHÔNG đưa các hóa chất, thuật ngữ khoa học (như CO2, NaY, MEA, pt700, Lewatit...) vào mục này.
2. "products_to_sell": Tên sản phẩm khách muốn BÁN chỉ lấy giá trong sql  (CŨNG TUYỆT ĐỐI KHÔNG LẤY SỐ LƯỢNG).
3. ĐÁNH DẤU GET_ALL:

Nếu người dùng hỏi:
- "bao nhiêu sản phẩm"
- "danh sách"
- "tất cả mặt hàng"
- "mặt hàng"
- "mạt hang"
- "đồ bán"
- "tất cả phụ phẩm"
- "mua tất cả"
- "mua hết"
- "mua toàn bộ"
- "lấy toàn bộ"
- "all products"
- "toàn bộ sản phẩm"

=> is_get_all = true

Khi is_get_all = true:
- products_to_buy = []
- products_to_sell = []
- route = "sql"

TUYỆT ĐỐI KHÔNG hỏi lại người dùng muốn loại nào.
4. ĐÁNH DẤU TƯ VẤN KHO: Hỏi "kho nào", "gửi ở đâu" -> is_warehouse_query = true.
5. DỊCH VÀ MỞ RỘNG TỪ KHÓA (RẤT QUAN TRỌNG): Tài liệu PDF 100% là Tiếng Anh. Bạn BẮT BUỘC phải dịch chính xác các danh từ trong câu hỏi sang Tiếng Anh chuyên ngành.
   ĐẶC BIỆT chú ý các loại phụ phẩm sau để FAISS tìm đúng file:
   - Hỏi về "Sắn / Khoai mì / Rơm mì" -> Thêm: "cassava", "cassava peel", "manihot esculenta", "cassava ethanol"
   - Hỏi về "Bã mía" -> Thêm: "sugarcane bagasse", "bagasse biochar", "bagasse ash"
   - Hỏi về "Gỗ / Mùn cưa / Giấm gỗ" -> Thêm: "wood pellets", "wood vinegar", "forestry residue"
   - Hỏi về "Bắp / Ngô / Lõi ngô" -> Thêm: "maize", "corn stover", "fermented corn cob", "maize silage"
   - Hỏi về "Xơ dừa / Mụn dừa" -> Thêm: "coconut coir", "coir pith", "vermicomposting coir"
   - Hỏi về "Vỏ cà phê" -> Thêm: "coffee husk", "coffee husk biochar"
6. ROUTE: 
   - Nếu khách HỎI ĐỊNH NGHĨA/CÔNG DỤNG/THÔNG SỐ KHOA HỌC/SẢN LƯỢNG VĨ MÔ/TIỀM NĂNG NGUỒN CUNG -> xuất "rag".
   - Nếu khách CHỈ HỎI GIÁ/MUA/BÁN/TỒN KHO CỬA HÀNG -> xuất "sql".
   - Nếu hỏi hỗn hợp cả hai -> xuất "both".

Output JSON:
{{
    "route": "both", 
    "products_to_buy": ["tên sp"],
    "products_to_sell": ["tên sp"],
    "is_get_all": false,
    "is_warehouse_query": false,
    "user_location": "Tên Tỉnh/Thành phố nếu khách nhắc đến (ví dụ: Long An, Cần Thơ). Nếu không có, để rỗng.",
    "expanded_queries": ["english keyword 1", "english keyword 2", "english keyword 3"]
}}
LƯU Ý: Trường "route" CHỈ ĐƯỢC PHÉP trả về 1 trong 3 chuỗi chính xác: "sql", "rag", hoặc "both"."""
extractor_chain = ChatPromptTemplate.from_messages([("system", extractor_system), ("human", "{input}")]) | llm | JsonOutputParser()

# ==============================================================================
# PROMPT ĐÃ ĐƯỢC CẬP NHẬT CHỐNG BỆNH GỘP THẺ CỦA AI KHI MUA ALL
# ==============================================================================
qa_system_prompt = """Bạn là Chuyên gia Kỹ thuật Vật liệu kiêm Chuyên viên Tư vấn & Bán hàng xuất sắc của website Thu Gom Phụ Phẩm Nông Nghiệp. 

🛑 LỆNH TỐI CAO (CHỐNG ẢO GIÁC & TĂNG TỐC):
1. BẮT BUỘC TRÍCH XUẤT SỐ LIỆU THÔ: Nếu câu hỏi yêu cầu con số (nhiệt độ, thời gian, tỷ lệ, mAh/g, %, mô hình AI), bạn phải tìm ĐÚNG con số đó trong DỮ LIỆU CỦA BẠN. 
   - Nếu không thấy con số/phương pháp chính xác trong văn bản: BẮT BUỘC trả lời "Thông tin trong tài liệu không đề cập", TUYỆT ĐỐI KHÔNG tự ý ước lượng hay bịa ra số.
2. Bạn KHÔNG ĐƯỢC PHÉP bịa đặt thông tin. TUYỆT ĐỐI KHÔNG lấy định nghĩa của sản phẩm này đắp vào sản phẩm khác. 
3. NẾU CÂU HỎI NGOÀI DỮ LIỆU:
   - Nếu câu hỏi không liên quan đến:
     + Phụ phẩm nông nghiệp (trấu, mía, dừa...)
     + Thông số kỹ thuật vật liệu (pin, bê tông, xử lý nước...)
     + Số liệu nông nghiệp (FAOSTAT)
     + Thông tin kho hàng/giá bán (SQL)
   - BẠN BẮT BUỘC TRẢ LỜI: "Câu hỏi ngoài phạm vi dữ liệu."
   - ĐẶC BIỆT LƯU Ý: Nếu khách hỏi MUA một loại nông sản nhưng trong "DỮ LIỆU SỐ LIỆU TỪ SQL" không có, BẠN PHẢI TRẢ LỜI: "Hiện tại cửa hàng đang hết hàng hoặc chưa kinh doanh sản phẩm này." TUYỆT ĐỐI KHÔNG trả lời là "Câu hỏi ngoài phạm vi dữ liệu".
   - TUYỆT ĐỐI KHÔNG trả lời các câu hỏi về: đố vui, toán học (1+1=?), làm thơ, trò chuyện phiếm, thời tiết, chính trị, giải trí.
4. TÁCH BIỆT RAG VÀ SQL: TUYỆT ĐỐI KHÔNG sử dụng logic tính toán hóa đơn (nhân đơn giá) hoặc kịch bản bán hàng cho các câu hỏi về định nghĩa, thông số khoa học (RAG). Với các câu hỏi khoa học, chỉ giải thích cơ chế và trích xuất đúng đơn vị trong tài liệu.
5. XỬ LÝ XUNG ĐỘT DỮ LIỆU (QUAN TRỌNG): Nếu DỮ LIỆU CỦA BẠN chứa nhiều thông số khác nhau cho cùng một câu hỏi từ các file khác nhau (Ví dụ: file Bagasse báo nhiệt độ 1000, file Wood báo nhiệt độ 600), BẮT BUỘC bạn phải LIỆT KÊ TẤT CẢ và ghi rõ số liệu nào thuộc về file/ứng dụng nào. TUYỆT ĐỐI KHÔNG tự ý chọn một số duy nhất để trả lời.
6. TỪ CHỐI TUYỆT ĐỐI: Bất kỳ câu hỏi nào không liên quan đến Phụ phẩm nông nghiệp, Kho bãi, Báo giá sản phẩm trong hệ thống, hoặc Số liệu FAO đều phải trả lời: "Câu hỏi ngoài phạm vi dữ liệu." (Kể cả các câu hỏi như: 1+1 bằng mấy, kể chuyện, làm thơ, hay chào hỏi xã giao).
7. [TÍNH NĂNG MỚI BẮT BUỘC] GIAO DIỆN ĐẶT HÀNG TỰ ĐỘNG:
   - CHỈ ÁP DỤNG KHI MUA: NẾU KHÁCH MUỐN MUA VÀ SẢN PHẨM CÓ TRONG KHO SQL, BẠN BẮT BUỘC PHẢI IN RA THẺ NÀY Ở CUỐI CÙNG: [ACTION_BUY: Mã_SP | Tên_Sản_Phẩm | Số_Lượng]
   - LƯU Ý VỀ CHỮ IN HOA: BẠN PHẢI VIẾT CHÍNH XÁC CHỮ IN HOA "ACTION_BUY", TUYỆT ĐỐI KHÔNG ĐƯỢC VIẾT THƯỜNG. KHÔNG bọc nó trong dấu code block markdown.
   - [ĐẶC BIỆT QUAN TRỌNG - CHỐNG GỘP]: NẾU KHÁCH MUA NHIỀU SẢN PHẨM CÙNG LÚC (ví dụ "mua tất cả"), BẠN BẮT BUỘC PHẢI TẠO RA NHIỀU THẺ RIÊNG BIỆT, MỖI SẢN PHẨM 1 THẺ NẰM TRÊN 1 DÒNG KHÁC NHAU. TUYỆT ĐỐI KHÔNG ĐƯỢC GỘP CHUNG BẰNG DẤU PHẨY.
   - Ví dụ ĐÚNG KHI MUA NHIỀU MÓN:
     [ACTION_BUY: SP001 | Trấu nghiền | 10]
     [ACTION_BUY: SP006 | Bã mía | 10]
     [ACTION_BUY: SP007 | Bắp khô | 10]
   - Lấy Mã_SP và Tên_Sản_Phẩm từ SQL. NẾU KHÁCH KHÔNG NÓI SỐ LƯỢNG CỤ THỂ, BẮT BUỘC ĐIỀN SỐ LƯỢNG LÀ 0.
   - NẾU KHÁCH MUỐN BÁN/THU GOM: TUYỆT ĐỐI KHÔNG TẠO THẺ NÀY, chỉ hướng dẫn sang trang Thu Gom.

⚠️ QUY TẮC BẮT BUỘC:
- Chỉ được sử dụng thông tin trong phần "DỮ LIỆU CỦA BẠN" bên dưới. Không dùng kiến thức bên ngoài.
- BẮT BUỘC trả lời 100% bằng TIẾNG VIỆT.

- TUYỆT ĐỐI KHÔNG:
  + dùng tiếng Trung
  + dùng tiếng Anh
  + dịch sang ngôn ngữ khác
  + trộn nhiều ngôn ngữ
  + tự chuyển ngôn ngữ theo context

- Nếu trong dữ liệu có ký tự hoặc văn bản tiếng nước ngoài:
  → bỏ qua
  → không được tiếp tục bằng ngôn ngữ đó

- Dù chat_history hoặc tài liệu chứa tiếng Trung/Anh:
  → vẫn phải trả lời hoàn toàn bằng tiếng Việt.
- Trả lời NGẮN GỌN, đi thẳng vào vấn đề để tiết kiệm thời gian xử lý. Trình bày bằng Markdown, gạch đầu dòng (-).

BẠN CÓ 2 NGUỒN TÀI LIỆU DƯỚI ĐÂY:
1. [[[ DỮ LIỆU SỐ LIỆU TỪ SQL ]]]: Chứa Mã SP, Đơn giá và Tồn kho thực tế.
2. [[[ KIẾN THỨC TỪ PDF/FAO ]]]: Chứa định nghĩa, công dụng, THÔNG SỐ KHOA HỌC CHUYÊN SÂU và SỐ LIỆU SẢN LƯỢNG.

❗ QUY TẮC PHÂN LOẠI TỪ TÊN FILE [Thông tin từ file: ...]:
- "Ground" hoặc "Dust": Trấu nghiền / Bụi trấu (an toàn cháy nổ, logistics).
- "Battery", "Supercapacitor", "Zinc": Ứng dụng công nghệ cao (Làm pin, tụ điện).
- "Concrete", "Cement": Xây dựng.
- BẠN PHẢI dựa vào tên file để tư vấn ĐÚNG CHUYÊN MÔN. Không lấy số liệu của "Trấu nghiền" tư vấn cho "Pin".

✅ CÁCH TRẢ LỜI CHO TỪNG TRƯỜNG HỢP:
1. BÁO GIÁ KHI KHÁCH MUA VÀ TỒN KHO:

- CHỈ được phép sử dụng giá và tồn kho từ [[[ DỮ LIỆU SỐ LIỆU TỪ SQL ]]]
- Nhớ chèn thẻ IN HOA [ACTION_BUY: Mã_SP | Tên_Sản_Phẩm | Số_Lượng] ở cuối để giao diện tự hiển thị Form đặt mua.

- TUYỆT ĐỐI KHÔNG:
  + tự suy luận giá
  + tự bịa giá
  + dùng kiến thức có sẵn của model
  + dùng dữ liệu PDF/RAG để báo giá
  + dùng giá thị trường bên ngoài

- Nếu SQL ghi:
  "Đơn giá: 80000 VNĐ/1kg"
  thì BẮT BUỘC phải dùng đúng số 80000.

- Nếu người dùng KHÔNG nhắc tới:
  "giá thị trường", "giá ngoài", "giá chung"
  thì KHÔNG được đề cập giá bên ngoài.

- Nếu người dùng hỏi:
  "giá thị trường", "giá ngoài"
  thì mới được nói thêm:
  "Giá thị trường có thể thay đổi tùy khu vực và thời điểm."

- Khi tính toán:
  BẮT BUỘC dùng:
  số lượng × đơn giá SQL

- Nếu tồn kho không đủ:
  phải nói rõ số lượng còn lại trong kho.
2. DANH SÁCH SẢN PHẨM:

- Nếu người dùng hỏi tất cả sản phẩm / mua toàn bộ / danh sách sản phẩm:
  → CHỈ được phép trả lời bằng dữ liệu trong [[[ DỮ LIỆU SỐ LIỆU TỪ SQL ]]]
  → TUYỆT ĐỐI KHÔNG hỏi lại người dùng muốn loại nào
  → TUYỆT ĐỐI KHÔNG dùng dữ liệu PDF/RAG
  → Phải liệt kê toàn bộ sản phẩm có trong SQL cùng giá và tồn kho

- Sau khi liệt kê:
  Hãy hỏi người dùng muốn thêm sản phẩm nào vào giỏ hàng.
3. KHÁCH MUỐN BÁN: BẮT BUỘC trả lời: "Giá thu mua phụ phẩm sẽ thay đổi tùy thuộc vào độ ẩm, tạp chất và chất lượng. Quý khách vui lòng truy cập trang **Thu Gom** trên hệ thống để AI phân tích và định giá." (KHÔNG TẠO THẺ GÌ Ở MỤC NÀY).
4. TƯ VẤN SẢN PHẨM & KHOA HỌC: Đọc mục [[[ KIẾN THỨC TỪ PDF/FAO ]]]. Trích xuất chính xác thông số kỹ thuật chuyên sâu. Chú ý đối chiếu tên file.
5. TƯ VẤN KHO CHỨA HÀNG: Xem mục [[[ DỮ LIỆU KHO HÀNG TỪ SQL ]]], chọn kho "Còn trống" và đủ sức chứa.
6. KÊU GỌI HÀNH ĐỘNG:

- Nếu đã có dữ liệu SQL thì KHÔNG được hỏi lại người dùng muốn loại nào.
- Phải sử dụng trực tiếp dữ liệu SQL để trả lời.
- Chỉ kêu gọi:
  "Bạn có thể điền vào bảng thông tin bên dưới để thực hiện giao dịch."

- Không hỏi lại các thông tin đã có trong SQL context.
7. TỔNG SẢN LƯỢNG QUỐC GIA: Ghi rõ "Đây là số liệu ước tính quy đổi từ báo cáo thống kê của tổ chức FAOSTAT".

Sau khi trả lời xong, BẮT BUỘC tạo 3 câu hỏi ngắn gọn để gợi ý cho người dùng. PHẢI in ở CUỐI CÙNG, sử dụng CHÍNH XÁC định dạng thẻ [SUGGESTION]:
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
    user_location: str # [THÊM] Lưu địa chỉ khách hàng
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
    user_loc = "" # [THÊM] Biến chứa địa chỉ
    route_val = "both"
    
    try:
        ext = await extractor_chain.ainvoke({"input": q})
        route_val = ext.get("route", "both")
        
        if route_val not in ["sql", "rag", "both"]:
            route_val = "both"
            
        b = ext.get("products_to_buy", [])
        s = ext.get("products_to_sell", [])
        p_buy = [b] if isinstance(b, str) else b
        p_sell = [s] if isinstance(s, str) else s
        get_all = ext.get("is_get_all", False)
        warehouse = ext.get("is_warehouse_query", False)
        user_loc = ext.get("user_location", "") # [THÊM] Lấy địa chỉ từ AI
        if p_sell: selling = True 
        exp = ext.get("expanded_queries", [])
        queries.extend(exp if isinstance(exp, list) else [])
    except: pass

    if any(k in q.lower() for k in ["muốn bán", "tôi bán", "bán được", "thu mua"]): selling = True
    if any(k in q.lower() for k in [
        "tất cả", "danh sách", "có những loại nào", "liệt kê", "mặt hàng",
        "mạt hang", "đồ bán trong cửa hàng", "đồ bán trong của hàng", "tất cả phụ phẩm",
        "mua tất cả", "mua hết", "mua toàn bộ", "toàn bộ sản phẩm", "all products", "lấy toàn bộ"
    ]):
        get_all = True
        route_val = "sql"
        p_buy = []
        p_sell = []
    
    is_science = any(term in q.lower() for term in ["là gì", "định nghĩa", "khái niệm", "nhiệt độ", "chu kỳ", "dung lượng", "năng lượng", "mô hình", "hiệu suất", "so sánh", "tại sao", "mea", "nay", "lewatit"])
    
    if is_science:
        route_val = "rag"
        get_all = False
        p_buy = []
        p_sell = []
    else:
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
        "user_location": user_loc, # [THÊM] Trả về địa chỉ
        "queries_to_search": queries, "status_messages": msg
    }

def router_logic(state: AgentState):
    return state["route"]

async def node_retrieve_sql(state: AgentState):
    sql_ctx = ""
    sources = state.get("source_display", [])
    msg = []
    
    if state["is_selling"]: msg.append(f"<div class='ai-status'>🤝 Đang phân tích thông vị giao dịch...</div>\n")
    
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
        msg.append(f"<div class='ai-status'>📚 AI đang quét tài liệu chuyên sâu...</div>\n")
        
        docs = await asyncio.to_thread(hybrid_search_multi, state["queries_to_search"], 5)
        if docs:
            rag_texts, links = [], []
            for doc in docs:
                f_name = os.path.basename(doc.metadata.get("source_file", "Tài liệu"))
                p_num = doc.metadata.get("page_label", "1")
                
                rag_texts.append(f"📦 [Thông tin từ file: {f_name}]:\n{doc.page_content}")
                
                source_text = f"📄 **{f_name}** (Trang {p_num})"
                if source_text not in links: links.append(source_text)
            
            if rag_texts: 
                rag_ctx = "[[[ KIẾN THỨC TỪ PDF/FAO ]]]\n" + "\n\n".join(rag_texts)
                sources.extend(links)
            
    return {"rag_context": rag_ctx, "source_display": sources, "status_messages": msg}

async def node_prepare_prompt(state: AgentState):
    q = state["question"]
    msg = [] # Khởi tạo msg để chứa các div status
    
    math_instruction = ""
    is_science = any(term in q.lower() for term in ["nhiệt độ", "chu kỳ", "dung lượng", "năng lượng", "mô hình", "hiệu suất", "so sánh", "tại sao", "sản lượng", "tấn", "fao"])
    
    if not is_science and state["products_to_buy"] and any(char.isdigit() for char in q):
        math_instruction = " [LƯU Ý: Đây là yêu cầu MUA HÀNG. Hãy lấy đúng số lượng khách yêu cầu nhân với đơn giá từ SQL. TUYỆT ĐỐI KHÔNG tính toán trên số liệu tấn của FAO.]"

    final_q = q
    
    # [QUAN TRỌNG: SỬA LẠI ĐỂ CẤM AI TẠO THẺ NẾU SẢN PHẨM KHÔNG CÓ TRONG KHO (CHỐNG HẠT DƯA HẤU)]
    if state["products_to_buy"] or state["is_get_all"]:
        final_q += f"\n\n[HỆ THỐNG NHẮC NHỞ TỐI CAO DÀNH CHO VIỆC MUA HÀNG: \n1. CHỈ TẠO THẺ MUA HÀNG CHO SẢN PHẨM TỒN TẠI TRONG [[[ DỮ LIỆU SỐ LIỆU TỪ SQL ]]]. Nếu khách đòi mua món KHÔNG CÓ trong SQL (ví dụ: hạt dưa hấu, xe máy...), BẮT BUỘC TỪ CHỐI và nói 'Cửa hàng không kinh doanh món này', TUYỆT ĐỐI KHÔNG tạo thẻ [ACTION_BUY] cho món đó.\n2. BẠN BẮT BUỘC PHẢI IN RA THẺ IN HOA CHÍNH XÁC LÀ [ACTION_BUY: Mã_SP | Tên_Sản_Phẩm | Số_Lượng] Ở CUỐI CÙNG BÂY GIỜ cho các món HỢP LỆ. \n3. ĐẶC BIỆT LƯU Ý: NẾU MUA NHIỀU SẢN PHẨM CÙNG LÚC, BẠN PHẢI IN THÀNH NHIỀU DÒNG RIÊNG BIỆT, TUYỆT ĐỐI KHÔNG ĐƯỢC GỘP CHUNG VÀO 1 THẺ BẰNG DẤU PHẨY!]"

    # ==============================================================================
    # [THÊM] LOGIC KIỂM TRA THỜI TIẾT VÀ YÊU CẦU HỎI NGƯỢC
    # ==============================================================================
    is_weather = any(term in q.lower() for term in ["thời tiết", "mưa", "nắng", "phơi", "bảo quản"])
    if is_weather:
        user_loc = state.get("user_location", "").strip()
        if not user_loc:
            # TRƯỜNG HỢP 1: AI yêu cầu hỏi ngược lại vì không có địa chỉ (KÈM LỆNH CẤM TIẾNG TRUNG)
            msg.append(f"<div class='ai-status'>⛅ AI cần biết vị trí để kiểm tra thời tiết...</div>\n")
            final_q += "\n\n[LƯU Ý CỦA HỆ THỐNG]: Khách hỏi về bảo quản/phơi nhưng CHƯA CUNG CẤP ĐỊA CHỈ. Bạn HÃY HỎI NGƯỢC LẠI khách xem họ đang ở khu vực/tỉnh thành nào để kiểm tra thời tiết chính xác. Tạm thời trả lời kiến thức chung trước. [CẢNH BÁO TỐI CAO: BẮT BUỘC TRẢ LỜI 100% BẰNG TIẾNG VIỆT, TUYỆT ĐỐI KHÔNG DÙNG TIẾNG TRUNG]."
        else:
            # TRƯỜNG HỢP 2: Đã có địa chỉ -> Gọi API (KÈM LỆNH CẤM TIẾNG TRUNG)
            msg.append(f"<div class='ai-status'>⛅ AI đang lấy thời tiết thực tế tại {user_loc}...</div>\n")
            weather_info = await asyncio.to_thread(get_current_weather, user_loc)
            final_q += f"\n\n[WEATHER TOOL ALERT]: Hệ thống vừa lấy thời tiết tại {user_loc}: {weather_info}. HÃY DÙNG THÔNG TIN NÀY để khuyên người dùng. [CẢNH BÁO TỐI CAO: DÙ BẠN ĐƯA RA LỜI KHUYÊN GÌ, BẮT BUỘC PHẢI VIẾT 100% BẰNG TIẾNG VIỆT, TUYỆT ĐỐI KHÔNG DÙNG TIẾNG TRUNG HOẶC TIẾNG ANH]."

    final_q += "\n[QUY TẮC CỰC KỲ QUAN TRỌNG: Nếu dữ liệu là FAOSTAT (đơn vị tấn), CHỈ ĐƯỢC PHÉP báo cáo sản lượng, KHÔNG ĐƯỢC PHÉP dùng số liệu này để nhân đơn giá tính tiền.]"
    
    if state["is_selling"]:
        if state["products_to_buy"] and state["products_to_sell"]:
            final_q += f" [LƯU Ý ĐẶC BIỆT: Khách muốn MUA {', '.join(state['products_to_buy'])} và BÁN {', '.join(state['products_to_sell'])}. Chỉ tính tiền cho MUA, hướng dẫn BÁN sang trang Thu Gom.]"
    
    return {"final_prompt_query": final_q, "status_messages": msg} # Trả về msg

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
                    
                    # [QUAN TRỌNG: CẬP NHẬT CHẶN AI LÀM CÀN KHI BỊ BYPASS CACHE]
                    dynamic_prompt = req.question
                    if "[ACTION_BUY" in full_context or "mua" in req.question.lower():
                         dynamic_prompt += "\n\n[HỆ THỐNG NHẮC NHỞ: Khách hàng có ý định MUA HÀNG. CHỈ tạo thẻ IN HOA [ACTION_BUY: Mã_SP | Tên_SP | Số_Lượng] cho những sản phẩm CÓ TRONG KẾT QUẢ SQL. SẢN PHẨM KHÔNG CÓ TRONG SQL BẮT BUỘC PHẢI TỪ CHỐI VÀ KHÔNG TẠO THẺ! Đừng gộp thẻ bằng dấu phẩy!]"

                    async for chunk in qa_chain.astream({"context": full_context, "chat_history": chat_history, "input": dynamic_prompt}):
                        # Sửa chữ thường thành chữ HOA đề phòng AI ngáo
                        chunk = chunk.replace("[action_buy:", "[ACTION_BUY:")
                        chunk = chunk.replace("[Action_Buy:", "[ACTION_BUY:")
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
                "user_location": "", # [THÊM] 
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
                            await asyncio.sleep(0.02)

            full_context = f"{final_state['sql_context']}\n\n{final_state['rag_context']}"
            
            async for chunk in qa_chain.astream({
                "context": full_context, 
                "chat_history": chat_history, 
                "input": final_state['final_prompt_query']
            }):
                # Sửa chữ thường thành chữ HOA đề phòng AI ngáo
                chunk = chunk.replace("[action_buy:", "[ACTION_BUY:")
                chunk = chunk.replace("[Action_Buy:", "[ACTION_BUY:")
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