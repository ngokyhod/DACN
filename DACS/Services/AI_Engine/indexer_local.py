import os
from langchain_community.document_loaders import PyPDFLoader
from langchain_text_splitters import RecursiveCharacterTextSplitter 
from langchain_community.vectorstores import FAISS # <--- Đổi từ Chroma sang FAISS
from langchain_huggingface import HuggingFaceEmbeddings

# CẤU HÌNH ĐƯỜNG DẪN
PDF_FOLDER = "./KnowledgeBase"
DB_PATH = "./faiss_db_local" # Đổi tên folder cho dễ phân biệt

def build_knowledge_base():
    if not os.path.exists(PDF_FOLDER):
        os.makedirs(PDF_FOLDER)
        print(f"⚠️ Vui lòng copy file PDF vào thư mục: {PDF_FOLDER}")
        return

    documents = []
    print("🔄 Đang quét tài liệu...")
    
    for file in os.listdir(PDF_FOLDER):
        if file.lower().endswith(".pdf"):
            pdf_path = os.path.join(PDF_FOLDER, file)
            loader = PyPDFLoader(pdf_path)
            docs = loader.load()
            for doc in docs:
                doc.metadata["source_file"] = file
            documents.extend(docs)
            print(f"   - Đã đọc: {file}")

    if not documents:
        print("❌ Không tìm thấy tài liệu nào!")
        return

    text_splitter = RecursiveCharacterTextSplitter(chunk_size=500, chunk_overlap=50)
    splits = text_splitter.split_documents(documents)

    print("🧠 Đang số hóa dữ liệu (Embedding)...")
    embedding_model = HuggingFaceEmbeddings(model_name="all-MiniLM-L6-v2")
    
    # --- KHÁC BIỆT CỦA FAISS ---
    vector_db = FAISS.from_documents(
        documents=splits,
        embedding=embedding_model
    )
    # FAISS phải gọi hàm save_local để lưu xuống ổ cứng
    vector_db.save_local(DB_PATH)
    
    print(f"✅ Xong! Đã nạp {len(splits)} đoạn kiến thức vào bộ não Local (FAISS).")

if __name__ == "__main__":
    build_knowledge_base()