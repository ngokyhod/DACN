import os
import uvicorn
import asyncio
import re
import json
import numpy as np
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
print("⏳ Đang khởi động Server AI (Bản Fix lỗi JSON & Lọc dấu câu)...")

# --- GLOBAL VARIABLES ---
vector_db = None
llm = None
rephrase_chain = None
qa_chain = None
extractor_chain = None
sql_engine = None   
embedding_model = None

bm25 = None
docs_list = []
reranker = None

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
        print(f"Lỗi Cache: {e}")
        return None

def add_to_cache(query: str, answer: str):
    try:
        if embedding_model:
            vector = np.array(embedding_model.embed_query(query))
            semantic_cache.append({"question": query, "vector": vector, "answer": answer})
            if len(semantic_cache) > 2000: # Cho phép chứa 2000 câu
                semantic_cache.pop(0)
    except Exception as e:
        print(f"Lỗi lưu Cache: {e}")

# ==============================================================================
# 1. CẤU HÌNH SQL
# ==============================================================================
DB_CONNECTION_STRING = "mssql+pyodbc://HOA230969\\SQLEXPRESS/QuanLyPhuPham?driver=ODBC+Driver+17+for+SQL+Server&trusted_connection=yes&TrustServerCertificate=yes"

def query_products_multi(keywords: list):
    if not sql_engine or not keywords: return ""
    
    query_template = text("""
        SELECT 
            sp.TenSanPham, 
            sp.Gia, 
            sp.MoTa,
            COALESCE(SUM(ltk.KhoiLuongConLai), 0) as TongTonKho
        FROM SanPhams sp
        LEFT JOIN LoTonKhos ltk ON sp.M_SanPham = ltk.M_SanPham
        WHERE sp.TenSanPham LIKE :kw
        GROUP BY sp.M_SanPham, sp.TenSanPham, sp.Gia, sp.MoTa
    """)
    
    final_result = []
    print(f"   ⚙️ Đang chạy lệnh SQL tìm: {keywords}")
    
    try:
        with sql_engine.connect() as conn:
            for kw in keywords:
                # ĐÃ FIX: Lọc sạch các dấu chấm hỏi, chấm, phẩy để SQL không bị lỗi
                kw_clean = kw.lower().replace("giá", "").replace("tiền", "").replace("mua", "").replace("ở đâu", "").replace("?", "").replace(".", "").replace(",", "").strip()
                result = conn.execute(query_template, {"kw": f"%{kw_clean}%"}).fetchall()
                if result:
                    for row in result:
                        ten = row[0]
                        gia = int(row[1]) if row[1] is not None else 0
                        ton_kho = int(row[3])
                        line = f"- Tên sản phẩm: {ten} | Đơn giá: {gia} VNĐ/đơn_vị | Tồn kho hiện tại: {ton_kho} kg"
                        final_result.append(line)
                        print(f"      ✅ SQL tìm thấy: {line}")
                else:
                    print(f"      ❌ SQL không có: '{kw_clean}'")

        if not final_result:
            return "[[[ DỮ LIỆU SỐ LIỆU TỪ SQL (CHÍNH THỨC): Rất tiếc, sản phẩm này hiện không có trên hệ thống. ]]]"
        return "[[[ DỮ LIỆU SỐ LIỆU TỪ SQL (BẮT BUỘC DÙNG ĐỂ TÍNH TIỀN/BÁO GIÁ) ]]]\n" + "\n".join(final_result) + "\n"
    except Exception as e:
        print(f"🔥 LỖI SQL: {e}")
        return ""

# ==============================================================================
# TẢI TRƯỚC BỘ NHỚ ĐỆM TỪ SQL (NẠP TẤT CẢ LỊCH SỬ CHUNG VÀO CACHE)
# ==============================================================================
def preload_cache_from_sql():
    """Hàm chạy 1 lần lúc khởi động server. Đọc toàn bộ DB để nạp Cache"""
    print("⏳ Đang tải trước Dữ liệu Cache từ Database...")
    if not sql_engine or not embedding_model: return
    
    # Lấy ra các cặp Hỏi (IsAi=0) - Đáp (IsAi=1) liền kề nhau
    query = text("""
        SELECT SessionId, IsAi, Content, CreatedAt
        FROM AIMessages
        ORDER BY SessionId, CreatedAt ASC
    """)
    
    try:
        with sql_engine.connect() as conn:
            rows = conn.execute(query).fetchall()
            
            last_question = None
            loaded_count = 0
            
            for row in rows:
                is_ai = row[1]
                content = row[2]
                
                # Nếu không chứa số liệu Live (không có chữ BẮT BUỘC DÙNG)
                if not is_ai: # Là User
                    last_question = content
                elif is_ai and last_question: # Là AI trả lời
                    # CHỈ CACHE NHỮNG CÂU KHÔNG CÓ BẢNG GIÁ (Để tránh lỗi giá cũ)
                    if "BẮT BUỘC DÙNG" not in content and "[SUGGESTION]" in content:
                        add_to_cache(last_question, content)
                        loaded_count += 1
                    last_question = None # Reset
                    
            print(f"✅ Đã nạp thành công {loaded_count} câu hỏi vào Semantic Cache!")
    except Exception as e:
        print(f"⚠️ Lỗi nạp Cache từ DB: {e}")

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

        # --- Nạp Cache từ Database lúc khởi động ---
        preload_cache_from_sql()

        # --- ĐÃ FIX PROMPT: Ép AI câm mồm không được sinh text ngoài JSON, dùng tiếng Việt ---
        extractor_system = """Nhiệm vụ: Phân tích câu hỏi người dùng.
        1. Tìm tên TẤT CẢ các sản phẩm trong câu hỏi để tra SQL. Bỏ qua các từ "mua", "giá", "ở đâu".
        2. Tạo ra thêm 2 câu hỏi MỞ RỘNG BẰNG TIẾNG VIỆT để tìm kiếm tài liệu PDF.
        
        CẢNH BÁO TỐI QUAN TRỌNG:
        - BẠN CHỈ ĐƯỢC PHÉP TRẢ VỀ ĐÚNG 1 CỤM JSON. 
        - TUYỆT ĐỐI KHÔNG giải thích. KHÔNG thêm text. KHÔNG dùng tiếng Anh.
        
        Output bắt buộc phải y hệt định dạng này:
        {{
            "products": ["tên sp"],
            "expanded_queries": ["câu tiếng việt 1", "câu tiếng việt 2"]
        }}
        """
        extractor_prompt = ChatPromptTemplate.from_messages([("system", extractor_system), ("human", "{input}")])
        extractor_chain = extractor_prompt | llm | JsonOutputParser()

        rephrase_chain = (ChatPromptTemplate.from_messages([
            ("system", "Dựa vào đoạn hội thoại, viết lại câu hỏi cuối cùng của người dùng cho rõ nghĩa. Chỉ in ra câu hỏi mới."),
            MessagesPlaceholder("chat_history"), ("human", "{input}")
        ]) | llm | StrOutputParser())

        qa_system_prompt = """Bạn là Chuyên viên Tư vấn & Bán hàng xuất sắc của website Thu Gom Phụ Phẩm Nông Nghiệp. 

⚠️ BỘ QUY TẮC SỐNG CÒN:
1. BÁO GIÁ VÀ TỒN KHO: BẠN CHỈ ĐƯỢC PHÉP ĐỌC TỪ Mục [[[ DỮ LIỆU SỐ LIỆU TỪ SQL ]]]. TUYỆT ĐỐI KHÔNG lấy giá/tỉ lệ % từ Mục PDF.
2. TƯ VẤN SẢN PHẨM: Đọc Mục [[[ KIẾN THỨC TỪ PDF ]]] để tư vấn "Nó là gì", "Dùng để làm gì". Đừng lôi phần giá trong PDF ra đọc.
3. KÊU GỌI HÀNH ĐỘNG: Luôn kêu gọi họ "Thêm vào giỏ hàng" hoặc "Mua trên website".
4. TÍNH TOÁN: Nếu khách hỏi "mua X kg giá bao nhiêu", tự động lấy (Đơn giá x Số kg).
5. VĂN PHONG: Chuyên nghiệp, ngắn gọn.

== CẤU TRÚC GỢI Ý CÂU HỎI TIẾP THEO (BẮT BUỘC) ==
Dưới cùng phải in chính xác định dạng này:
[SUGGESTION] Câu hỏi thứ nhất?
[SUGGESTION] Câu hỏi thứ hai?
[SUGGESTION] Câu hỏi thứ ba?

Dữ liệu của bạn:
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
    except Exception as e:
        print(f"Lỗi Multi-RAG: {e}")
        return []

# ==============================================================================
# 3. TRÍ NHỚ DÀI HẠN CỦA TỪNG SESSION (Lấy TOP 4 để đàm thoại mượt mà)
# ==============================================================================
def get_history_from_sql(session_id: str, limit: int = 4):
    history = ChatMessageHistory()
    if not sql_engine: return history
    query = text("""
        SELECT TOP (:limit) IsAi, Content 
        FROM AIMessages 
        WHERE SessionID = :sid 
        ORDER BY CreatedAt DESC
    """)
    try:
        with sql_engine.connect() as conn:
            rows = conn.execute(query, {"sid": session_id, "limit": limit}).fetchall()
            for is_ai, content in reversed(rows):
                if is_ai: history.add_ai_message(content)
                else: history.add_user_message(content)
    except Exception as e:
        print(f"⚠️ Lỗi đọc AIMessages: {e}")
    return history

class ChatRequest(BaseModel):
    session_id: str
    question: str

@app.post("/chat")
async def chat(req: ChatRequest):
    if not vector_db: raise HTTPException(status_code=500, detail="AI chưa sẵn sàng")
    
    chat_history_obj = get_history_from_sql(req.session_id, limit=4)
    chat_history = chat_history_obj.messages

    async def generate():
        try:
            print(f"\n=======================================================")
            print(f"📨 NHẬN CÂU HỎI: '{req.question}'")

            # 0. CHẠY CACHE TRƯỚC TIÊN (Từ kho Cache tổng đã được nạp lúc bật Server)
            cached_answer = await asyncio.to_thread(get_from_cache, req.question)
            if cached_answer:
                words = cached_answer.split(" ")
                for word in words:
                    yield word + " "
                    await asyncio.sleep(0.01)
                print("✅ HOÀN THÀNH: Trả lời từ CACHE siêu tốc!")
                print(f"=======================================================\n")
                return 

            # 1. REPHRASE
            print("🔄 Bước 1: Đọc lịch sử ngữ cảnh...")
            search_query = req.question
            if chat_history:
                try:
                    rephrased = await rephrase_chain.ainvoke({"chat_history": chat_history, "input": req.question})
                    if "apologize" in rephrased.lower() or "sorry" in rephrased.lower() or len(rephrased) > 150:
                        print(f"   ⚠️ Rephrase bị ngáo. Khôi phục câu hỏi gốc.")
                    else:
                        search_query = rephrased
                    print(f"   -> Câu hỏi được làm rõ: '{search_query}'")
                except Exception as ex: 
                    print(f"   ⚠️ Lỗi Rephrase: {ex}")

            # 2. EXTRACT
            print("🤖 Bước 2: AI trích xuất từ khóa...")
            products = []
            queries_to_search = [search_query]
            try:
                extracted = await extractor_chain.ainvoke({"input": search_query})
                print(f"   -> Kết quả JSON: {extracted}")
                products = extracted.get("products", [])
                if isinstance(products, str): products = [products]
                expanded = extracted.get("expanded_queries", [])
                if isinstance(expanded, list): queries_to_search.extend(expanded)
            except Exception as ex:
                print(f"   ⚠️ Lỗi Extractor (AI không trả ra đúng JSON): {ex}")

            keywords_check = ["giá", "tiền", "kho", "kg", "tổng", "mua", "ở đâu", "là gì"]
            if not products and any(k in search_query.lower() for k in keywords_check):
                clean_kw = search_query.lower().replace("giá", "").replace("của", "").replace("bao nhiêu", "").replace("mua", "").replace("ở đâu", "").replace("là gì", "").replace("?", "").replace(".", "").replace("!", "").strip()
                products = [p.strip() for p in clean_kw.split(" và ") if p.strip()]

            # 3. SQL & RAG
            print("🔍 Bước 3: Thu thập dữ liệu...")
            sql_context = ""
            rag_context = ""
            source_display = []

            if products:
                yield f"🔍 *Đang tra cứu dữ liệu Live cho: {', '.join(products)}...*\n\n"
                sql_context = await asyncio.to_thread(query_products_multi, products)
                if "BẮT BUỘC DÙNG" in sql_context: source_display.append("**Kho hàng Website**")

            print(f"   ⚙️ Đang chạy RAG cho các câu: {queries_to_search}")
            docs = await asyncio.to_thread(hybrid_search_multi, queries_to_search, 3)
            if docs:
                rag_context = "[[[ KIẾN THỨC TỪ PDF (TUYỆT ĐỐI KHÔNG LẤY GIÁ TRONG NÀY) ]]]\n" + "\n\n".join([d.page_content for d in docs])
                src_map = {}
                for doc in docs:
                    f = doc.metadata.get("source_file", "Doc")
                    p = doc.metadata.get("page_label", "?")
                    src_map.setdefault(f, set()).add(str(p))
                rag_src = " | ".join([f"{k} (Trang {','.join(sorted(v))})" for k,v in src_map.items()])
                source_display.append(f"**Tài liệu:** {rag_src}")

            # 4. TRẢ LỜI
            print("✍️ Bước 4: AI đang sinh câu trả lời...")
            full_context = f"{sql_context}\n\n{rag_context}"
            full_answer = ""
            async for chunk in qa_chain.astream({"context": full_context, "input": req.question}):
                full_answer += chunk
                yield chunk
            
            if source_display:
                full_answer += f"\n\n---\n*Nguồn: {' | '.join(source_display)}*"
                yield f"\n\n---\n*Nguồn: {' | '.join(source_display)}*"

            # 5. LƯU CACHE 
            if "BẮT BUỘC DÙNG" not in sql_context:
                await asyncio.to_thread(add_to_cache, req.question, full_answer)
                print("💾 Bước 5: Đã lưu câu trả lời vào Cache.")
            else:
                print("⏩ Bước 5: Bỏ qua Cache (Vì chứa giá cập nhật liên tục).")
            
            print("✅ HOÀN THÀNH: Trả lời thành công!")
            print(f"=======================================================\n")

        except Exception as e:
            print(f"❌ Lỗi cực mạnh: {e}")
            yield f"\n❌ Lỗi hệ thống: Vui lòng thử lại. Chi tiết: {str(e)}"

    return StreamingResponse(generate(), media_type="text/plain")

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=5000)