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
print("⏳ Đang khởi động Server AI (Advanced Math, Hybrid, Memory & Semantic Cache)...")

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
# [MỚI] HỆ THỐNG SEMANTIC CACHING
# ==============================================================================
semantic_cache = []
CACHE_THRESHOLD = 0.95 

def get_from_cache(query: str):
    if not semantic_cache or not embedding_model:
        return None
    
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
            print(f"⚡ [CACHE HIT] Đã tìm thấy trong bộ nhớ đệm (Độ giống: {best_score:.2f})")
            return best_answer
        return None
    except Exception as e:
        print(f"Lỗi Cache: {e}")
        return None

def add_to_cache(query: str, answer: str):
    try:
        if embedding_model:
            vector = np.array(embedding_model.embed_query(query))
            semantic_cache.append({
                "question": query,
                "vector": vector,
                "answer": answer
            })
            if len(semantic_cache) > 1000:
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
    print(f"🔌 SQL: Bắt đầu truy vấn danh sách: {keywords}")
    
    try:
        with sql_engine.connect() as conn:
            for kw in keywords:
                # Xóa bớt các từ gây nhiễu để tìm trong database
                kw_clean = kw.lower().replace("giá", "").replace("tiền", "").replace("mua", "").replace("ở đâu", "").strip()
                result = conn.execute(query_template, {"kw": f"%{kw_clean}%"}).fetchall()
                
                if result:
                    for row in result:
                        ten = row[0]
                        gia = int(row[1]) if row[1] is not None else 0
                        ton_kho = int(row[3])
                        
                        line = f"- Tên sản phẩm: {ten} | Đơn giá: {gia} VNĐ/đơn_vị | Tồn kho hiện tại: {ton_kho} kg"
                        final_result.append(line)
                        print(f"      ✅ Tìm thấy: {line}")
                else:
                    print(f"      ❌ Không tìm thấy sản phẩm nào khớp với '{kw_clean}'")

        if not final_result:
            return "[[[ DỮ LIỆU SỐ LIỆU TỪ SQL (CHÍNH THỨC): Rất tiếc, sản phẩm này hiện không có trên hệ thống hoặc đã hết hàng. ]]]"
            
        return "[[[ DỮ LIỆU SỐ LIỆU TỪ SQL (BẮT BUỘC DÙNG ĐỂ TÍNH TIỀN/BÁO GIÁ) ]]]\n" + "\n".join(final_result) + "\n"
        
    except Exception as e:
        print(f"🔥 LỖI SQL: {e}")
        return ""

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

        # --- EXTRACTOR CHAIN ---
        extractor_system = """Nhiệm vụ: Phân tích câu hỏi người dùng.
        1. Tìm tên TẤT CẢ các sản phẩm trong câu hỏi để tra SQL. Bỏ qua các từ "mua", "giá", "ở đâu".
        2. Tạo ra thêm 2 câu hỏi MỞ RỘNG (dùng từ đồng nghĩa/cách diễn đạt khác) để tìm kiếm tài liệu PDF tốt hơn.
        
        Output bắt buộc là JSON:
        {
            "products": ["tên sp"],
            "expanded_queries": ["câu mở rộng 1", "câu mở rộng 2"]
        }
        """
        extractor_prompt = ChatPromptTemplate.from_messages([("system", extractor_system), ("human", "{input}")])
        extractor_chain = extractor_prompt | llm | JsonOutputParser()

        # --- REPHRASE CHAIN ---
        rephrase_chain = (ChatPromptTemplate.from_messages([
            ("system", "Dựa vào đoạn hội thoại, hãy viết lại câu hỏi cuối cùng của người dùng thành một câu hoàn chỉnh, rõ ràng. Chỉ in ra câu hỏi mới."),
            MessagesPlaceholder("chat_history"), ("human", "{input}")
        ]) | llm | StrOutputParser())

        # --- QA FINAL CHAIN (CẬP NHẬT PROMPT BÁN HÀNG CỰC GẮT) ---
        qa_system_prompt = """Bạn là Chuyên viên Tư vấn & Bán hàng xuất sắc của hệ thống website Thu Gom & Phân Phối Phụ Phẩm Nông Nghiệp. 
Mục tiêu của bạn là: Tư vấn để khách hàng hiểu sản phẩm và CHỐT ĐƠN trực tiếp trên website.

⚠️ BỘ QUY TẮC SỐNG CÒN:
1. BÁO GIÁ VÀ TỒN KHO: BẠN CHỈ ĐƯỢC PHÉP ĐỌC TỪ Mục [[[ DỮ LIỆU SỐ LIỆU TỪ SQL ]]]. Nếu SQL nói "không có", bạn phải nói "hiện không có". TUYỆT ĐỐI KHÔNG lấy giá/tỉ lệ % từ Mục PDF.
2. TƯ VẤN SẢN PHẨM: Đọc Mục [[[ KIẾN THỨC TỪ PDF ]]] để tư vấn "Nó là gì", "Dùng để làm gì". Đừng lôi phần giá trong PDF ra đọc.
3. KÊU GỌI HÀNH ĐỘNG: Vì bạn là người bán hàng trên trang web này, nên nếu khách hỏi "mua ở đâu", hãy trả lời: "Quý khách có thể mua ngay trực tiếp trên trang web này của chúng tôi". Kêu gọi họ "Thêm vào giỏ hàng".
4. TÍNH TOÁN: Nếu khách hỏi "mua X kg giá bao nhiêu", tự động lấy (Đơn giá x Số kg).
5. VĂN PHONG: Chuyên nghiệp, lịch sự, xuống dòng rõ ràng.

== CẤU TRÚC GỢI Ý CÂU HỎI TIẾP THEO (BẮT BUỘC) ==
Sau khi tư vấn xong, dưới cùng phải in chính xác định dạng này (thay chữ bằng gợi ý thực tế):
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

# ===== HYBRID SEARCH MULTI =====
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
# 3. TRÍ NHỚ DÀI HẠN TỪ SQL
# ==============================================================================
def get_history_from_sql(session_id: str, limit: int = 4):
    history = ChatMessageHistory()
    if not sql_engine: return history

    query = text("""
        SELECT TOP (:limit) IsAi, Content 
        FROM AIMessages 
        WHERE SessionId = :sid 
        ORDER BY CreatedAt DESC
    """)

    try:
        with sql_engine.connect() as conn:
            rows = conn.execute(query, {"sid": session_id, "limit": limit}).fetchall()
            
            for is_ai, content in reversed(rows):
                if is_ai:
                    history.add_ai_message(content)
                else:
                    history.add_user_message(content)
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
            if chat_history:
                search_query = await rephrase_chain.ainvoke({"chat_history": chat_history, "input": req.question})
            else:
                search_query = req.question
            
            print(f"\n--- NHẬN CÂU HỎI: '{search_query}' ---")

            products = []
            queries_to_search = [search_query]
            
            try:
                extracted = await extractor_chain.ainvoke({"input": search_query})
                products = extracted.get("products", [])
                if isinstance(products, str): products = [products]
                
                expanded = extracted.get("expanded_queries", [])
                if isinstance(expanded, list):
                    queries_to_search.extend(expanded)
            except Exception as ex:
                products = []

            # Dự phòng tìm sản phẩm
            keywords_check = ["giá", "tiền", "kho", "kg", "tổng", "mua", "ở đâu"]
            if not products and any(k in search_query.lower() for k in keywords_check):
                clean_kw = search_query.lower().replace("giá", "").replace("của", "").replace("bao nhiêu", "").replace("mua", "").replace("ở đâu", "").strip()
                products = [p.strip() for p in clean_kw.split(" và ") if p.strip()]

            # [CACHE LOGIC]
            if not products:
                cached_answer = get_from_cache(search_query)
                if cached_answer:
                    words = cached_answer.split(" ")
                    for word in words:
                        yield word + " "
                        await asyncio.sleep(0.01)
                    return 

            sql_context = ""
            rag_context = ""
            source_display = []

            if products:
                yield f"🔍 *Đang kiểm tra hệ thống: {', '.join(products)}...*\n\n"
                sql_context = query_products_multi(products)
                if "SỬ DỤNG ĐỂ TÍNH TIỀN" in sql_context:
                    source_display.append("**Kho hàng Website**")

            # Giảm K xuống 3 để lấy ít tài liệu rác hơn
            docs = hybrid_search_multi(queries_to_search, k=3)
            if docs:
                # Ghi chú rõ RAG là thông tin tham khảo định nghĩa
                rag_context = "[[[ KIẾN THỨC TỪ PDF (Tuyệt đối bỏ qua phần giá cả trong này, chỉ dùng để tư vấn lợi ích) ]]]\n" + "\n\n".join([d.page_content for d in docs])
                src_map = {}
                for doc in docs:
                    f = doc.metadata.get("source_file", "Doc")
                    p = doc.metadata.get("page_label", "?")
                    src_map.setdefault(f, set()).add(str(p))
                rag_src = " | ".join([f"{k} (Trang {','.join(sorted(v))})" for k,v in src_map.items()])
                source_display.append(f"**Tài liệu:** {rag_src}")

            full_context = f"{sql_context}\n\n{rag_context}"
            
            full_answer = ""
            async for chunk in qa_chain.astream({"context": full_context, "input": req.question}):
                full_answer += chunk
                yield chunk
            
            if source_display:
                full_answer += f"\n\n---\n*Nguồn: {' | '.join(source_display)}*"
                yield f"\n\n---\n*Nguồn: {' | '.join(source_display)}*"

            if not products:
                add_to_cache(search_query, full_answer)
            
            print("--- KẾT THÚC ---")

        except Exception as e:
            yield f"\n❌ Lỗi xử lý: {str(e)}"

    return StreamingResponse(generate(), media_type="text/plain")

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=5000)