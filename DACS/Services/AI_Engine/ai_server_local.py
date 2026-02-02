import os
import uvicorn
import asyncio
from fastapi import FastAPI, HTTPException
from fastapi.responses import StreamingResponse
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from operator import itemgetter

# ===== IMPORT LANGCHAIN =====
from langchain_huggingface import HuggingFaceEmbeddings
from langchain_community.vectorstores import FAISS
from langchain_ollama import ChatOllama
from langchain_core.prompts import ChatPromptTemplate, MessagesPlaceholder
from langchain_community.chat_message_histories import ChatMessageHistory
from langchain_core.runnables.history import RunnableWithMessageHistory
from langchain_core.output_parsers import StrOutputParser
from langchain_core.runnables import RunnablePassthrough

app = FastAPI()

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

DB_PATH = "./faiss_db_local"
print("⏳ Đang khởi động Server AI (Streaming + Sources)...")

# --- GLOBAL VARIABLES ---
vector_db = None
retriever = None
llm = None
rephrase_chain = None
qa_chain = None

# --- LOAD DATABASE ---
if os.path.exists(DB_PATH):
    try:
        # 1. Setup Cơ bản
        embedding_model = HuggingFaceEmbeddings(model_name="all-MiniLM-L6-v2")
        vector_db = FAISS.load_local(DB_PATH, embedding_model, allow_dangerous_deserialization=True)
        retriever = vector_db.as_retriever(search_kwargs={"k": 2})
        llm = ChatOllama(model="llama3", temperature=0.3)

        # 2. Chain Viết lại câu hỏi (Hiểu lịch sử)
        rephrase_prompt = ChatPromptTemplate.from_messages([
            ("system", "Dựa vào lịch sử chat, hãy viết lại câu hỏi mới nhất cho rõ nghĩa để tìm kiếm. KHÔNG TRẢ LỜI."),
            MessagesPlaceholder("chat_history"),
            ("human", "{input}"),
        ])
        rephrase_chain = rephrase_prompt | llm | StrOutputParser()

        # 3. Chain Trả lời (QA Only)
        qa_system_prompt = """Bạn là chuyên gia Nông nghiệp Việt Nam.
        Trả lời 100% Tiếng Việt. Trình bày Markdown đẹp (gạch đầu dòng bàng cái này -).
1. TUYỆT ĐỐI KHÔNG dùng tiếng Anh trừ khi họ hỏi bẰng tiếng anh.
 2. Nếu thông tin không có trong tài liệu, hãy nói: "Dữ liệu của tôi chưa cập nhật vấn đề này".
 3. Trình bày rõ ràng, dùng gạch đầu dòng.
 4. Nếu chỉ hỏi về 1 chủ đề thì chỉ xem tài liệu của 1 chủ đề đó đùng lấn át sang chủ đề khác nếu nó không có liên quan gì đến nhau.
        Thông tin tham khảo: {context}"""
        
        qa_prompt = ChatPromptTemplate.from_messages([
            ("system", qa_system_prompt),
            ("human", "{input}") 
        ])
        qa_chain = qa_prompt | llm | StrOutputParser()

        print("✅ AI Server đã sẵn sàng!")
    except Exception as e:
        print(f"❌ Lỗi khởi động: {e}")

# --- MEMORY ---
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
    if not vector_db:
        raise HTTPException(status_code=500, detail="AI chưa sẵn sàng.")

    # Lấy lịch sử chat
    history = get_session_history(req.session_id)
    chat_history = history.messages

    # --- HÀM XỬ LÝ LUỒNG (GENERATOR) ---
    async def generate():
        try:
            # B1: Xác định câu hỏi tìm kiếm (Có lịch sử hoặc không)
            if chat_history:
               search_query = req.question
            else:
                search_query = req.question

            # B2: Tìm tài liệu (Retrieve) - LẤY NGUỒN Ở ĐÂY
            docs = await retriever.ainvoke(search_query)
            
            # Lọc tên file để hiển thị nguồn
            source_names = list(set([doc.metadata.get('source_file', 'Tài liệu') for doc in docs]))
            
            # Format nội dung để đưa vào Prompt
            context_text = "\n\n".join([d.page_content for d in docs])

            # B3: Stream câu trả lời của AI
            full_answer = ""
            async for chunk in qa_chain.astream({"context": context_text, "input": req.question}):
                full_answer += chunk
                yield chunk # Trả chữ về cho Web ngay lập tức

            # B4: Lưu vào bộ nhớ lịch sử (Quan trọng để lần sau còn nhớ)
            history.add_user_message(req.question)
            history.add_ai_message(full_answer)

            # B5: Gửi kèm Nguồn vào cuối dòng (Web sẽ hiển thị luôn)
            if source_names:
                sources_str = ", ".join(source_names)
                # Xuống dòng và in đậm nguồn
                yield f"\n\n---\n**📚 Nguồn tham khảo:** *{sources_str}*"

        except Exception as e:
            yield f"\n❌ Lỗi xử lý: {str(e)}"

    return StreamingResponse(generate(), media_type="text/plain")

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=5000)