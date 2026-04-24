import os
import pandas as pd
from langchain.schema import Document
from langchain_community.document_loaders import PyPDFLoader
from langchain_text_splitters import RecursiveCharacterTextSplitter
from langchain_community.vectorstores import FAISS
from langchain_huggingface import HuggingFaceEmbeddings

# Import file từ điển quy đổi bạn vừa tạo
from conversion_factors import CROP_TO_BYPRODUCT

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

            # Nếu cây trồng nằm trong danh sách quy đổi
            if crop_name in CROP_TO_BYPRODUCT:
                mapping = CROP_TO_BYPRODUCT[crop_name]
                byproduct_name = mapping['byproduct']
                rate = mapping['rate']
                byproduct_volume = crop_volume * rate

                # Tạo ra một câu văn bản hoàn chỉnh cho AI đọc
                sentence = f"Vào năm {year}, tổng sản lượng {crop_name.lower()} tại Việt Nam đạt {crop_volume:,.0f} {unit}. Dựa trên hệ số quy đổi {rate*100}%, ước tính sản lượng phụ phẩm '{byproduct_name}' thu được trên toàn quốc là {byproduct_volume:,.0f} {unit}. (Nguồn: Báo cáo thống kê FAOSTAT)"
                
                doc = Document(
                    page_content=sentence,
                    metadata={
                        "source_file": filename,
                        "page_label": "CSV Data",
                        "type": "fao_data",
                        "year": year
                    }
                )
                docs.append(doc)
        print(f"      ✅ Tạo thành công {len(docs)} câu dữ liệu từ CSV.")
    except Exception as e:
        print(f"      ❌ Lỗi khi đọc CSV {filename}: {e}")
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
        
        # 1. Nếu là file PDF
        if file.lower().endswith(".pdf"):
            try:
                loader = PyPDFLoader(file_path)
                docs = loader.load()
                for doc in docs:
                    doc.metadata["source_file"] = file
                    doc.metadata["page_label"] = doc.metadata.get("page", 0) + 1
                documents.extend(docs)
                print(f"   - ✅ Đã đọc: {file} ({len(docs)} trang)")
            except Exception as e:
                print(f"   - ❌ Lỗi đọc PDF {file}: {e}")
                
        # 2. Nếu là file CSV của FAO
        elif file.lower().endswith(".csv"):
            csv_docs = process_fao_csv(file_path, file)
            documents.extend(csv_docs)

    if not documents:
        print("❌ Không tìm thấy tài liệu nào hợp lệ!")
        return

    text_splitter = RecursiveCharacterTextSplitter(
        chunk_size=1000, 
        chunk_overlap=200,
        separators=["\n\n", "\n", ". ", " ", ""], 
        is_separator_regex=False
    )
    
    splits = text_splitter.split_documents(documents)
    print(f"✂️  Đã tổng hợp thành {len(splits)} đoạn thông tin (Chunk).")

    print("🧠 Đang nạp kiến thức vào Vector DB (FAISS)...")
    try:
        embedding_model = HuggingFaceEmbeddings(model_name="all-MiniLM-L6-v2")
        vector_db = FAISS.from_documents(documents=splits, embedding=embedding_model)
        vector_db.save_local(DB_PATH)
        print(f"✅ HOÀN TẤT! Dữ liệu đã lưu tại: {DB_PATH}")
    except Exception as e:
        print(f"❌ Lỗi khi embedding: {e}")

if __name__ == "__main__":
    build_knowledge_base()