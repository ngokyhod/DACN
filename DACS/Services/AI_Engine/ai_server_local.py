import os
import uvicorn
import asyncio
import re
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

# ===== SQL TOOL (MỚI THÊM) =====
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
print("⏳ Đang khởi động Server AI (Agentic RAG + Custom Prompt)...")

# --- GLOBAL VARIABLES ---
vector_db = None
llm = None
rephrase_chain = None
qa_chain = None
router_chain = None # <--- MỚI: Chain phân loại
sql_engine = None   # <--- MỚI: Kết nối SQL

bm25 = None
docs_list = []
reranker = None

# ==============================================================================
# 1. CẤU HÌNH SQL (ĐÃ ĐIỀN ĐÚNG TỪ APPSETTINGS CỦA BẠN)
# ==============================================================================
DB_CONNECTION_STRING = "mssql+pyodbc://HOA230969\\SQLEXPRESS/QuanLyPhuPham?driver=ODBC+Driver+17+for+SQL+Server&trusted_connection=yes&TrustServerCertificate=yes"

def query_product_db(keyword):
    """Hàm Tool: Tìm kiếm sản phẩm trong SQL Server"""
    try:
        if not sql_engine: return "Lỗi: Chưa kết nối được SQL Server."
        
        # Tìm sản phẩm theo tên (Gần đúng)
        # Giả định bảng tên là 'SanPhams' (cột TenSanPham, Gia, SoLuongTon)
        query = text("""
            SELECT TOP 5 TenSanPham, Gia
            FROM SanPhams 
            WHERE TenSanPham LIKE :kw
        """)
        
        with sql_engine.connect() as conn:
            result = conn.execute(query, {"kw": f"%{keyword}%"}).fetchall()
            
        if not result:
            return f"Hệ thống kho: Không tìm thấy sản phẩm nào có tên chứa '{keyword}'."
        
        # Format dữ liệu thành văn bản cho AI đọc
        info = []
        for row in result:
            price = f"{int(row[1]):,} VNĐ" if row[1] else "Liên hệ"
            info.append(f"- Sản phẩm: {row[0]} | Giá: {price} | Tồn kho: {row[2]}")
            
        return "Dữ liệu tìm thấy từ hệ thống:\n" + "\n".join(info)
        
    except Exception as e:
        return f"Lỗi truy vấn SQL: {str(e)}"

# ==============================================================================
# 2. KHỞI TẠO SERVER
# ==============================================================================
if os.path.exists(DB_PATH):
    try:
        # --- A. Load RAG (FAISS + BM25 + Reranker) ---
        embedding_model = HuggingFaceEmbeddings(model_name="all-MiniLM-L6-v2")
        vector_db = FAISS.load_local(DB_PATH, embedding_model, allow_dangerous_deserialization=True)

        # Build BM25
        docs_list = list(vector_db.docstore._dict.values())
        def tokenize(text: str): return re.findall(r"\w+", text.lower())
        bm25_corpus = [tokenize(doc.page_content) for doc in docs_list]
        bm25 = BM25Okapi(bm25_corpus)

        # Load Re-ranker
        print("⚖️ Đang tải model Re-ranking...")
        reranker = CrossEncoder('cross-encoder/ms-marco-MiniLM-L-6-v2')

        # --- B. Load LLM ---
        llm = ChatOllama(model="llama3", temperature=0.3)

        # --- C. Load SQL Engine ---
        try:
            sql_engine = create_engine(DB_CONNECTION_STRING)
            # Test kết nối
            with sql_engine.connect() as conn: pass
            print("✅ Đã kết nối SQL Server thành công!")
        except Exception as e:
            print(f"⚠️ Cảnh báo: Lỗi kết nối SQL ({e}). Tính năng tra giá sẽ không hoạt động.")

        # --- D. Các Chains ---
        
        # 1. Chain Router: Phân loại câu hỏi (MỚI)
        router_system = """Bạn là bộ phận điều hướng thông minh.
        Nhiệm vụ: Phân loại câu hỏi người dùng thành 2 loại: "SQL" hoặc "RAG".
        
        Quy tắc:
        - Nếu câu hỏi về: Giá bán, Bao nhiêu tiền, Tồn kho, Số lượng, Có hàng không, Mua bán -> Loại "SQL".
          Đồng thời trích xuất "keyword" là tên sản phẩm.
        - Các câu hỏi khác (Kỹ thuật, khái niệm, cách làm, tư vấn) -> Loại "RAG".
        
        Output bắt buộc là JSON: {{"type": "SQL", "keyword": "..."}} hoặc {{"type": "RAG", "keyword": null}}
        """
        router_prompt = ChatPromptTemplate.from_messages([("system", router_system), ("human", "{input}")])
        router_chain = router_prompt | llm | JsonOutputParser()

        # 2. Chain Rephrase (Giữ nguyên)
        rephrase_system_prompt = """Dựa vào lịch sử chat và câu hỏi mới nhất. 
        Nhiệm vụ: Viết lại câu hỏi đó thành một câu hoàn chỉnh, rõ nghĩa để tìm kiếm thông tin.
        CHỈ TRẢ LỜI CÂU HỎI ĐÃ VIẾT LẠI. KHÔNG TRẢ LỜI NỘI DUNG."""
        
        rephrase_prompt = ChatPromptTemplate.from_messages([
            ("system", rephrase_system_prompt),
            MessagesPlaceholder("chat_history"),
            ("human", "{input}"),
        ])
        rephrase_chain = rephrase_prompt | llm | StrOutputParser()

        # 3. Chain QA (Giữ nguyên Prompt của bạn)
        qa_system_prompt = """Bạn là chuyên gia Nông nghiệp Việt Nam.

⚠️ QUY TẮC BẮT BUỘC:
- Chỉ được sử dụng thông tin trong phần "Thông tin tham khảo".
- Không được dùng kiến thức bên ngoài.
- Nếu câu hỏi không liên quan đến nông nghiệp → trả lời: "Câu hỏi ngoài phạm vi dữ liệu".
- Nếu tài liệu không có thông tin → trả lời: "Dữ liệu của tôi chưa cập nhật vấn đề này".

⚠️ NGÔN NGỮ:
- Trả lời 100% tiếng Việt
- Trình bày Markdown
- Dùng gạch đầu dòng (-)

Thông tin tham khảo:
{context}"""
        
        qa_prompt = ChatPromptTemplate.from_messages([
            ("system", qa_system_prompt),
            ("human", "{input}") 
        ])
        qa_chain = qa_prompt | llm | StrOutputParser()

        print("✅ AI Server đã sẵn sàng (Agentic Mode)!")

    except Exception as e:
        print(f"❌ Lỗi khởi động: {e}")

# ===== HYBRID SEARCH + RE-RANKING (GIỮ NGUYÊN) =====
def hybrid_search(query: str, k: int = 6):
    fetch_k = k * 3 
    
    # 1. BM25
    tokenized_query = tokenize(query)
    bm25_scores = bm25.get_scores(tokenized_query)
    bm25_top_idx = sorted(range(len(bm25_scores)), key=lambda i: bm25_scores[i], reverse=True)[:fetch_k]
    bm25_docs = [docs_list[i] for i in bm25_top_idx]

    # 2. FAISS
    vector_docs = vector_db.similarity_search(query, k=fetch_k)

    # 3. Merge
    unique_docs = {}
    for doc in vector_docs + bm25_docs:
        unique_docs[doc.page_content] = doc
    candidates = list(unique_docs.values())

    # 4. Re-rank
    if not candidates: return []
    pairs = [[query, doc.page_content] for doc in candidates]
    scores = reranker.predict(pairs)
    sorted_docs = [doc for score, doc in sorted(zip(scores, candidates), key=lambda x: x[0], reverse=True)]

    return sorted_docs[:k]

# ===== MEMORY =====
session_store = {}
def get_session_history(session_id: str):
    if session_id not in session_store:
        session_store[session_id] = ChatMessageHistory()
    return session_store[session_id]

class ChatRequest(BaseModel):
    session_id: str
    question: str

@app.post("/chat")
async def chat(req: ChatRequest):
    if not vector_db: raise HTTPException(status_code=500, detail="AI chưa sẵn sàng")

    history = get_session_history(req.session_id)
    chat_history = history.messages

    async def generate():
        try:
            # ===== B1: REPHRASE (HIỂU NGỮ CẢNH) =====
            if chat_history:
                # print(f"🔄 Đang suy luận ngữ cảnh: {req.question}")
                search_query = await rephrase_chain.ainvoke({"chat_history": chat_history, "input": req.question})
                # print(f"✅ Câu hỏi mới: {search_query}")
            else:
                search_query = req.question

            # ===== B2: ROUTER (PHÂN LOẠI CÂU HỎI) =====
            # print(f"🚦 Đang định tuyến câu hỏi: {search_query}")
            try:
                route = await router_chain.ainvoke({"input": search_query})
                # print(f"👉 Route quyết định: {route}")
            except:
                route = {"type": "RAG"} # Mặc định nếu lỗi JSON

            context_text = ""
            source_display = ""

            # --- NHÁNH 1: HỎI GIÁ/HÀNG HÓA (SQL) ---
            if route.get("type") == "SQL":
                keyword = route.get("keyword") or search_query
                yield f"🔍 *Đang tra cứu hệ thống sản phẩm: '{keyword}'...*\n\n"
                
                # Gọi Tool SQL
                db_result = query_product_db(keyword)
                context_text = db_result
                source_display = "**Nguồn:** Cơ sở dữ liệu SQL Server (Real-time)"

            # --- NHÁNH 2: HỎI KIẾN THỨC (RAG) ---
            else: 
                # yield f"📖 *Đang đọc tài liệu kỹ thuật...*\n\n"
                docs = hybrid_search(search_query, k=5)
                context_text = "\n\n".join([d.page_content for d in docs])
                
                # Xử lý nguồn PDF
                if docs:
                    src_map = {}
                    for doc in docs:
                        f = doc.metadata.get("source_file", "Doc")
                        p = doc.metadata.get("page_label", "?")
                        src_map.setdefault(f, set()).add(str(p))
                    source_display = " | ".join([f"{k} (Trang {','.join(sorted(v, key=lambda x: int(x) if x.isdigit() else 0))})" for k,v in src_map.items()])
                    source_display = f"**📚 Nguồn tham khảo:** {source_display}"

            # ===== B3: STREAM ANSWER =====
            full_answer = ""
            # Quan trọng: Truyền context vào để Prompt của bạn sử dụng
            async for chunk in qa_chain.astream({"context": context_text, "input": req.question}):
                full_answer += chunk
                yield chunk

            # ===== B4: SAVE MEMORY =====
            history.add_user_message(req.question)
            history.add_ai_message(full_answer)

            # ===== B5: SOURCES =====
            if source_display:
                yield f"\n\n---\n{source_display}"

        except Exception as e:
            yield f"\n❌ Lỗi hệ thống: {str(e)}"

    return StreamingResponse(generate(), media_type="text/plain")

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=5000)