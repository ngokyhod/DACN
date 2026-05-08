import os
import pandas as pd
from langchain_core.documents import Document
from langchain_community.document_loaders import PyPDFLoader
from langchain_text_splitters import RecursiveCharacterTextSplitter
from langchain_community.vectorstores import FAISS
from langchain_huggingface import HuggingFaceEmbeddings

# Import file từ điển quy đổi bạn vừa tạo
try:
    from conversion_factors import CROP_TO_BYPRODUCT
except ImportError:
    CROP_TO_BYPRODUCT = {}

# --- CẤU HÌNH ---
PDF_FOLDER = "./KnowledgeBase"
DB_PATH = "./faiss_db_local"

def process_fao_csv(csv_path, filename):
    print(f"   - 📊 Đang xử lý file CSV FAO: {filename}")
    docs = []
    try:
        df = pd.read_csv(csv_path)
        # Lọc chỉ lấy các dòng Sản lượng (Production)
        if 'Element' in df.columns and 'Unit' in df.columns:
            df_prod = df[(df['Element'] == 'Production') & ((df['Unit'] == 't') | (df['Unit'] == 'm3'))]
        else:
            df_prod = df
            
        for index, row in df_prod.iterrows():
            crop_name = row.get('Item', '')
            year = row.get('Year', '')
            crop_volume = float(row.get('Value', 0))
            unit = "tấn" if row.get('Unit', '') == 't' else row.get('Unit', 'tấn')

            if crop_name in CROP_TO_BYPRODUCT:
                mapping = CROP_TO_BYPRODUCT[crop_name]
                byproduct_name = mapping['byproduct']
                rate = mapping['rate']
                byproduct_volume = crop_volume * rate

                sentence = f"Vào năm {year}, tổng sản lượng {crop_name.lower()} tại Việt Nam đạt {crop_volume:,.0f} {unit}. Dựa trên hệ số quy đổi {rate*100}%, ước tính sản lượng phụ phẩm '{byproduct_name}' thu được trên toàn quốc là {byproduct_volume:,.0f} {unit}. (Nguồn: Báo cáo thống kê FAOSTAT)"
                
                doc = Document(
                    page_content=sentence,
                    metadata={
                        "source_file": filename,
                        "material": "General_Crop",
                        "application": "Statistics",
                        "page_label": "CSV Data",
                        "type": "fao_data",
                        "year": year
                    }
                )
                docs.append(doc)
        print(f"     ✅ Tạo thành công {len(docs)} câu dữ liệu từ CSV.")
    except Exception as e:
        print(f"     ❌ Lỗi khi đọc CSV {filename}: {e}")
    return docs

def build_knowledge_base():
    if not os.path.exists(PDF_FOLDER):
        os.makedirs(PDF_FOLDER)
        print(f"⚠️ Thư mục '{PDF_FOLDER}' chưa có. Hãy copy file PDF và CSV vào đó!")
        return

    documents = []
    print("🔄 Đang quét và phân tích tài liệu (PDF & CSV)...")
    
    for file in os.listdir(PDF_FOLDER):
        file_path = os.path.join(PDF_FOLDER, file)
        
        # 1. Xử lý file PDF và Cắt chuỗi Tên File
        if file.lower().endswith(".pdf"):
            try:
                # Tách tên file bỏ đuôi .pdf
                filename_no_ext = os.path.splitext(file)[0]
                
                # Cắt chuỗi dựa trên dấu gạch dưới "_"
                parts = filename_no_ext.split("_")
                
                # [ĐÃ SỬA TẠI ĐÂY] Trích xuất linh hoạt từ 2 đầu để không sót Ứng dụng
                if len(parts) >= 4:
                    material = parts[0]
                    year = parts[-1]                        # Lấy phần tử cuối cùng làm Năm
                    author = parts[-2]                      # Lấy phần tử áp chót làm Tác giả
                    application = "_".join(parts[1:-2])     # Gom toàn bộ các mảnh ở giữa làm Ứng dụng
                else:
                    # Phòng hờ trường hợp file đặt tên ngắn/thiếu định dạng
                    material = parts[0] if len(parts) > 0 else "Unknown"
                    application = parts[1] if len(parts) > 1 else "General"
                    author = parts[2] if len(parts) > 2 else "Unknown"
                    year = parts[3] if len(parts) > 3 else "Unknown"

                loader = PyPDFLoader(file_path)
                docs = loader.load()
                
                for doc in docs:
                    # Gán Metadata để RAG biết đường mà lọc
                    doc.metadata.update({
                        "source_file": file,
                        "material": material,
                        "application": application,
                        "author": author,
                        "year": year,
                        "page_label": doc.metadata.get("page", 0) + 1
                    })
                documents.extend(docs)
                print(f"   - ✅ Đã đọc & Gán nhãn: {file} -> [{material} | Ứng dụng: {application} | Tác giả: {author} | Năm: {year}]")
            except Exception as e:
                print(f"   - ❌ Lỗi đọc PDF {file}: {e}")
                
        # 2. Xử lý file CSV của FAO
        elif file.lower().endswith(".csv"):
            csv_docs = process_fao_csv(file_path, file)
            documents.extend(csv_docs)

    if not documents:
        print("❌ Không tìm thấy tài liệu nào hợp lệ!")
        return

    text_splitter = RecursiveCharacterTextSplitter(
        chunk_size=2500,  
        chunk_overlap=500, 
        separators=["\n\n", "\n", ". ", " ", ""], 
        is_separator_regex=False
    )
    
    splits = text_splitter.split_documents(documents)
    print(f"✂️  Đã tổng hợp thành {len(splits)} đoạn thông tin (Chunk) có dán nhãn đầy đủ.")

    print("🧠 Đang nạp kiến thức vào Vector DB (FAISS)...")
    try:
        embedding_model = HuggingFaceEmbeddings(model_name="paraphrase-multilingual-MiniLM-L12-v2")
        vector_db = FAISS.from_documents(documents=splits, embedding=embedding_model)
        vector_db.save_local(DB_PATH)
        print(f"✅ HOÀN TẤT! Dữ liệu đã lưu tại: {DB_PATH}")
    except Exception as e:
        print(f"❌ Lỗi khi embedding: {e}")

if __name__ == "__main__":
    build_knowledge_base()