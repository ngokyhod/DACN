import os
from langchain_community.document_loaders import PyPDFLoader
from langchain_text_splitters import RecursiveCharacterTextSplitter
from langchain_community.vectorstores import FAISS
from langchain_huggingface import HuggingFaceEmbeddings

# --- CẤU HÌNH ---
PDF_FOLDER = "./KnowledgeBase"
DB_PATH = "./faiss_db_local"

def build_knowledge_base():
    # 1. Kiểm tra thư mục
    if not os.path.exists(PDF_FOLDER):
        os.makedirs(PDF_FOLDER)
        print(f"⚠️ Thư mục '{PDF_FOLDER}' chưa có. Hãy copy file PDF vào đó!")
        return

    documents = []
    print("🔄 Đang quét và phân tích tài liệu...")
    
    # 2. Đọc file PDF và giữ lại Metadata (Số trang, Tên file)
    for file in os.listdir(PDF_FOLDER):
        if file.lower().endswith(".pdf"):
            pdf_path = os.path.join(PDF_FOLDER, file)
            try:
                loader = PyPDFLoader(pdf_path)
                docs = loader.load()
                
                # Bổ sung tên file vào metadata cho từng trang
                for doc in docs:
                    doc.metadata["source_file"] = file
                    # PyPDFLoader đã tự động lấy số trang và lưu vào doc.metadata['page']
                    # Ta cộng thêm 1 vì máy tính đếm từ 0, người đọc đếm từ 1
                    doc.metadata["page_label"] = doc.metadata.get("page", 0) + 1
                
                documents.extend(docs)
                print(f"   - ✅ Đã đọc: {file} ({len(docs)} trang)")
            except Exception as e:
                print(f"   - ❌ Lỗi đọc file {file}: {e}")

    if not documents:
        print("❌ Không tìm thấy tài liệu nào hợp lệ!")
        return

    # 3. CẮT TÀI LIỆU NÂNG CAO (Semantic-like & Overlap)
    # - chunk_size=1000: Mỗi đoạn khoảng 1000 ký tự (đủ dài để giữ ngữ cảnh).
    # - chunk_overlap=200: Chồng lấn 20% để không bị đứt mạch ý.
    # - separators: Ưu tiên cắt theo đoạn văn (\n\n) trước, nếu dài quá mới cắt theo câu.
    text_splitter = RecursiveCharacterTextSplitter(
        chunk_size=1000, 
        chunk_overlap=200,
        separators=["\n\n", "\n", ". ", " ", ""], 
        is_separator_regex=False
    )
    
    splits = text_splitter.split_documents(documents)
    print(f"✂️  Đã cắt thành {len(splits)} đoạn thông tin (Chunk).")

    # 4. SỐ HÓA DỮ LIỆU (Embedding)
    print("🧠 Đang nạp kiến thức vào bộ não (Embedding)...")
    try:
        embedding_model = HuggingFaceEmbeddings(model_name="all-MiniLM-L6-v2")
        
        vector_db = FAISS.from_documents(
            documents=splits,
            embedding=embedding_model
        )
        
        vector_db.save_local(DB_PATH)
        print(f"✅ HOÀN TẤT! Dữ liệu đã lưu tại: {DB_PATH}")
        
    except Exception as e:
        print(f"❌ Lỗi khi embedding: {e}")

if __name__ == "__main__":
    build_knowledge_base()