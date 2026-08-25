# Saikahana Web — Trang thu gom & buôn bán phụ phẩm nông nghiệp

Phiên bản: nội bộ (local)
Tác giả: ngokyhod

## Tóm tắt (What this is)
Saikahana-web là một website thương mại điện tử dành cho thu gom và mua bán phụ phẩm nông nghiệp, bao gồm:
- Giao diện web ASP.NET MVC (project DACS) cho chức năng mua/bán, giỏ hàng, wishlist, quản lý đơn, quản lý kho,...
- AI assistant tích hợp RAG (retrieval-augmented generation) và image matching (CLIP) chạy local (thư mục DACS/Services/AI_Engine).
- Kết nối backend → mobile bằng API (các controller trong `DACS/Controllers/Api` và các route MVC).
- Hỗ trợ gửi email, SMS, đồng bộ Firebase, và tích hợp blockchain (Nethereum) để lưu giao dịch/truy xuất.

---

## 📹 Demo & Screenshots

**Xem video giới thiệu đầy đủ về các tính năng chính của Saikahana Web:**

[![Saikahana Web Demo](https://img.youtube.com/vi/QHBbG-jc3zM/0.jpg)](https://youtu.be/QHBbG-jc3zM)

- 🛒 Mua / Bán sản phẩm phụ phẩm nông nghiệp
- 🤖 Tư vấn AI với RAG (Retrieval-Augmented Generation) + LLM Local QWEN 2.5
- 💬 Chat real-time với SignalR
- 📸 Nhận diện ảnh sản phẩm bằng CLIP
- 📱 API cho ứng dụng mobile

---

## Stack chính
- Language(s): C# (ASP.NET MVC) + Python (FastAPI AI engine) + SQL Server
- Framework / runtime:
  - ASP.NET Core (WebApplication.CreateBuilder — tương thích .NET 6+)
  - FastAPI (AI server) + Uvicorn
- Notable libraries:
  - .NET: Entity Framework Core, Identity, SignalR, Swashbuckle (Swagger), Nethereum
  - Python AI: CLIP, torch, HuggingFace embeddings (langchain_huggingface), FAISS, sentence-transformers, CrossEncoder, rank_bm25, langchain_ollama (ChatOllama)

---

## Cấu trúc repo (đã đọc các phần chính)
```
DACS/                       # ASP.NET MVC project (server chính)
  Controllers/              # Controllers web + Api + nhiều controller xử lý SP, ThuGom, Cart...
  Models/                   # Entity models + ApplicationDbContext.cs
  Services/                 # Service layer: EmailService, ESmsService, BlockchainService, AIMatchingService, SocketServer...
    AI_Engine/              # AI components (FastAPI servers, indexer, CLIP, faiss DB, reference images, knowledgebase)
      agent_server_local.py # Local agent server (chạy trên port 5000)
      indexer_local.py      # Script để tạo FAISS DB từ PDF/CSV
      KnowledgeBase/        # PDF & ReferenceImages (được serve tĩnh tại /pdfs)
  Views/                    # Razor views
  Program.cs                # Khởi tạo app, DI, Swagger, SignalR, static files, routes (mapping ChatHub)
  appsettings.json          # Cấu hình (ConnectionStrings, EmailSettings, ESmsSettings, ...)
DACS.sln
Mau_Import_SanPham.xlsx     # Mẫu import sản phẩm
SQLQueryprovip.sql         # Script SQL / query hỗ trợ
.gitignore
```

How it fits together:
- ASP.NET app (DACS) là frontend + backend chính, phục vụ web và API cho mobile; khởi động đồng thời một SocketServer/SignalR cho chat thời gian thực.
- AI engine (Python) chạy độc lập (FastAPI) trên cổng **5000**; webapp gọi endpoint `/chat` để hỏi AI. AI engine dùng FAISS/embeddings + BM25 + cross-encoder để RAG + LLM Local (QWEN 2.5).
- Dữ liệu động (giá, tồn kho, lịch sử chat AI) được lấy trực tiếp từ SQL Server bằng SQLAlchemy trong Python (DB_CONNECTION_STRING) và bằng EF Core trong C#.

---

## Tính năng chính

### 🛍️ Mua / Bán sản phẩm
- Giỏ hàng, đặt hàng, trạng thái đơn
- Tính tiền tự động dựa trên giá SQL + số lượng

### 🌾 Thu gom sản phẩm
- Thu gom từ nông dân (form & quy trình)
- Định giá tự động

### 🤖 Tư vấn AI bằng tiếng Việt 100%
- **RAG (Retrieval-Augmented Generation)**: Trả lời dựa trên 2 nguồn:
  - **SQL Database**: Giá, tồn kho, thông tin kho bãi
  - **PDF/CSV Knowledge Base**: Định nghĩa sản phẩm, thông số kỹ thuật, công dụng, sản lượng FAO
- **LLM Local**: QWEN 2.5 (model tối ưu cho tiếng Việt + đọc hiểu số liệu)
- **Hybrid Search**: BM25 (từ khóa chính xác) + Vector Search (ngữ nghĩa sâu)
- **Cache Semantic 95%**: Lưu 2000 câu hỏi đã trả lời → Trả lời tức thời 0.1 giây nếu câu hỏi tương tự
- **Anti-Hallucination**: 7 lệnh tối cao chống AI bịa đặt

### 📸 Nhận diện ảnh sản phẩm
- CLIP (ViT-L/14) nhận diện ảnh upload → Gợi ý sản phẩm
- Reference Images được mã hóa trước

### 💬 Chat thời gian thực
- SignalR + socket server
- Lưu lịch sử chat vào SQL

### ⛅ API Thời tiết
- Tự động kích hoạt khi khách hỏi về bảo quản/phơi
- Lấy tọa độ từ tên địa chỉ → Gọi Open-Meteo API
- Tích hợp vào lời tư vấn của AI

### 📧 Email & SMS
- Thông báo đơn hàng, giao hàng
- Xác nhận thanh toán

### ⛓️ Blockchain (Nethereum)
- Lưu giao dịch traceability
- Truy xuất lịch sử

### 📱 API cho mobile
- Các controller trong `Controllers/Api`

---

## Yêu cầu trước khi cài

- Hệ điều hành: Linux / Windows (phù hợp môi trường dev)
- .NET SDK: 6.0+ (WebApplication.CreateBuilder — dùng .NET 6/7 đều chạy)
- Visual Studio / dotnet CLI
- SQL Server (local hoặc remote) với database tương thích schema của dự án
- Python 3.10+ (khuyến nghị) với pip
- Nếu muốn dùng CLIP + acceleration: GPU + CUDA (nếu không có, chạy trên CPU nhưng chậm)
- **OLLAMA** và model **QWEN 2.5** (bắt buộc cho LLM local) → Tải tại [ollama.ai](https://ollama.ai)
- FAISS: cài faiss-cpu hoặc faiss-gpu tùy cấu hình

---

## Cài đặt & cấu hình nhanh (local)

### 1) Clone repo
```bash
git clone https://github.com/ngokyhod/saikahana-web.git
cd saikahana-web
```

### 2) Cấu hình database & appsettings (DACS/appsettings.json)
- Mở `DACS/appsettings.json` và chỉnh:
  - ConnectionStrings: `DefaultConnection` → chuỗi kết nối tới SQL Server của bạn (ví dụ: Server=.;Database=QuanLyPhuPham;Trusted_Connection=True;)
  - EmailSettings: host/port/username/password/from
  - ESmsSettings: cấu hình nhà cung cấp SMS nếu dùng
- File firebase: `DACS/firebase_config.json` phải chứa credential service account của Firebase. Program.cs đặt biến môi trường:
  - GOOGLE_APPLICATION_CREDENTIALS -> đường dẫn tới `firebase_config.json`
  - Nếu chạy local, đảm bảo file tồn trong `DACS/` hoặc đặt đường dẫn tuyệt đối

### 3) Cài .NET dependencies & migrations
- Tạo database & apply migrations (nếu dùng EF Migrations có sẵn):
```bash
cd DACS
# Nếu bạn dùng dotnet-ef:
dotnet tool install --global dotnet-ef
dotnet restore
dotnet ef database update
# hoặc mở solution DACS.sln trong Visual Studio và update database bằng Package Manager Console
```

### 4) Cài Python dependencies cho AI engine
- Tạo virtualenv và cài:
```bash
cd DACS/Services/AI_Engine
python -m venv .venv
# Linux
source .venv/bin/activate
# Windows
# .venv\Scripts\activate

pip install --upgrade pip
pip install uvicorn fastapi pydantic numpy torch pillow langchain_huggingface langchain_community langchain_ollama sentence-transformers faiss-cpu rank_bm25 cross-encoder sqlalchemy google-auth
# (Tên package có thể khác; tùy hệ thống GPU vs CPU chọn faiss-gpu hoặc faiss-cpu)
```

### 5) Chuẩn bị KnowledgeBase và ReferenceImages
- Thư mục: `DACS/Services/AI_Engine/KnowledgeBase/`
  - Đặt PDF tài liệu (dùng cho RAG). Các file PDF sẽ được serve tĩnh từ webapp (Program.cs maps PhysicalFileProvider → /pdfs).
  - Tạo `ReferenceImages` dưới KnowledgeBase và bỏ ảnh mẫu (png/jpg/webp). CLIP sẽ mã hóa ảnh để so khớp.
- **📝 Quy tắc đặt tên PDF** (QUAN TRỌNG):
  ```
  [Material]_[Application]_[Author]_[Year].pdf
  Ví dụ: Cassava_Ethanol_Nguyen_2025.pdf
  ```
  Metadata sẽ được trích xuất để AI biết đó là tài liệu nào.
- Tạo Faiss DB (nếu repo chưa có): chạy script indexer (nếu có) hoặc dùng `indexer_local.py` để tạo `faiss_db_local/`.

### 6) Cấu hình biến môi trường cho AI/Python
- (Ví dụ Linux / macOS)
```bash
export GOOGLE_APPLICATION_CREDENTIALS="/path/to/DACS/firebase_config.json"
export DB_CONNECTION_STRING="mssql+pyodbc://<SERVER>/<DB>?driver=ODBC+Driver+17+for+SQL+Server&trusted_connection=yes&TrustServerCertificate=yes"
# (Bạn có thể chỉnh trực tiếp biến trong agent_server_local.py hoặc dùng conf file)
```

### 7) Khởi động OLLAMA (LLM Local) — **BẮTBUỘC**
- Terminal 1:
```bash
ollama serve
```

- Terminal 2 (chạy 1 lần):
```bash
ollama pull qwen2.5:7b
```

**Lưu ý**: Nếu không có QWEN 2.5, chỉnh `llm = ChatOllama(model="qwen2.5:7b", temperature=0)` trong `agent_server_local.py` sang model bạn có.

### 8) Chạy AI server (local)
- Từ thư mục `DACS/Services/AI_Engine/`:
```bash
# Terminal 3: Chạy agent server (PORT 5000)
uvicorn agent_server_local:app --host 0.0.0.0 --port 5000
```

Kết quả:
```
⏳ Đang khởi động Server AI với QWEN 2.5 (BẢN FIX LỖI ẢO GIÁC)...
✅ Kết nối SQL và FAISS thành công!
INFO:     Uvicorn running on http://0.0.0.0:5000
```

**Lưu ý**: `agent_server_local.py` chứa endpoint POST `/chat` để trả lời streaming; webapp tự gọi endpoint này để giao tiếp với AI.

### 9) Chạy Webapp ASP.NET (DACS)
- Từ thư mục gốc hoặc trong Visual Studio mở `DACS.sln`:
```bash
cd DACS
dotnet restore
dotnet run
# Hoặc mở DACS.sln và run trong Visual Studio (IIS Express / Kestrel)
```

- Program.cs sẽ:
  - Khởi động socketServer (Service SocketServer) và map SignalR hub tại `Hubs/ChatHub`.
  - Serve static files trong thư mục KnowledgeBase dưới route `/pdfs` → ví dụ /pdfs/YourDoc.pdf.
  - Khởi chạy BlockchainService.TestBlockchainAsync() (non-blocking) nếu cấu hình blockchain hợp lệ.

### 10) Kết nối mobile / API
- API dành cho mobile được triển khai trong `DACS/Controllers/Api` và các controller khác. Kiểm tra route (ở Program.cs routing/MapControllers). Dùng base URL của webapp (ví dụ: http://localhost:5001).

---

## 📊 AI Engine Workflow (LangGraph 6 Bước)

### Input
Câu hỏi + Ảnh (nếu có)

### Quy trình xử lý
```
Input: Câu hỏi + Ảnh (nếu có)
│
├─→ [1. process_image]
│   └─ CLIP nhận diện ảnh → Thêm vào câu hỏi
│
├─→ [2. extract_intent]
│   └─ AI phân tích:
│      • Muốn mua hay bán?
│      • Hỏi về giá (SQL) hay định nghĩa (RAG)?
│      • Route: "sql" / "rag" / "both"
│      • Trích xuất từ khóa tiếng Anh để search RAG
│
├─→ [3. retrieve_sql] (nếu route có SQL)
│   └─ Query MSSQL:
│      • Giá sản phẩm
│      • Tồn kho
│      • Thông tin kho hàng
│
├─→ [4. retrieve_pdf] (nếu route có RAG)
│   └─ Hybrid search FAISS:
│      • BM25: Tìm từ khóa chính xác
│      • Vector: Tìm ngữ nghĩa tương tự
│      • Top 5 documents + metadata
│
├─→ [5. prepare_prompt]
│   └─ Chuẩn bị prompt cuối:
│      • Nối SQL context + RAG context
│      • Gọi API thời tiết nếu cần (dựa trên địa chỉ khách)
│      • Kiểm tra hạn chế (không tạo ACTION nếu hết hàng)
│
└─→ [6. LLM (QWEN 2.5)]
    └─ Trả lời bằng Tiếng Việt 100%
       • Kèm [ACTION_BUY: mã | tên | số lượng] nếu mua (riêng biệt cho từng sản phẩm)
       • Kèm [SUGGESTION] 3 câu hỏi gợi ý
       • Kèm [SUGGESTION] 3 câu hỏi gợi ý
```

### Hai Nguồn Dữ Liệu Chính

| Nguồn | Loại Dữ Liệu | Dùng Cho |
|-------|-------------|---------|
| FAISS (PDF/CSV) | Định nghĩa, thông số kỹ thuật, phương pháp xử lý, sản lượng FAO | Câu hỏi về "là gì?", "công dụng?", "sản lượng?" |
| SQL Database | Giá, tồn kho, thông tin mua/bán, kho bãi | Câu hỏi về "giá bao nhiêu?", "còn hàng?", "mua/bán" |

---

## ⚡ Tối Ưu Hiệu Năng

### 🚀 Cache Semantic 95%
- Lưu 2000 câu hỏi đã trả lời (vector + kết quả)
- Nếu câu hỏi tương tự → Trả lời tức thời (0.1 giây)
- Không cần gọi FAISS + LLM lần thứ 2

### 🔍 Hybrid Search (BM25 + Vector)
- BM25: Tìm kiếm từ khóa chính xác (nhanh)
- Vector: Tìm ngữ nghĩa sâu (tương tự)
- Kết hợp → Kết quả tốt hơn so với 1 phương pháp

### 🎯 Anti-Hallucination (7 Lệnh Tối Cao)
1. **Trích xuất số liệu thô** - Không được ước lượng
2. **Tách biệt RAG/SQL** - Không lấy giá từ PDF
3. **Từ chối ngoài dữ liệu** - Không hỏi về thời tiết/chính trị (ngoài khi liên quan đến bảo quản)
4. **Kiểm tra hết hàng** - Chỉ tạo ACTION khi sản phẩm còn
5. **Tiếng Việt 100%** - Không phục vụ tiếng khác
6. **[ACTION_BUY]** - Phải in hoa, không được gộp nhiều thẻ thành 1 (mỗi sản phẩm một thẻ riêng)
7. **[SUGGESTION]** - Bắt buộc 3 câu hỏi gợi ý ở cuối

---

## 🔧 Cấu Hình & Tuỳ Chỉnh

### File: `agent_server_local.py`
```python
DB_PATH = "./faiss_db_local"                    # Vector database
DB_CONNECTION_STRING = "mssql+pyodbc://..."     # SQL connection
llm = ChatOllama(model="qwen2.5:7b", temperature=0)  # LLM (QWEN 2.5)
embedding_model = HuggingFaceEmbeddings(
    model_name="paraphrase-multilingual-MiniLM-L12-v2"
)  # Embedding model
```

### File: `indexer_local.py` (nếu cần chạy lại indexing)
```python
PDF_FOLDER = "./KnowledgeBase"      # Nơi các PDF
DB_PATH = "./faiss_db_local"        # Output vector DB
chunk_size = 2500                   # Kích thước mỗi chunk
chunk_overlap = 500                 # Overlap giữa chunks
```

---

## 📡 API Endpoints

### POST /chat - Gửi Câu Hỏi
```bash
curl -X POST http://localhost:5000/chat \
  -H "Content-Type: application/json" \
  -d '{
    "session_id": "user_123",
    "question": "Giá trấu nghiền bao nhiêu? Tôi muốn mua 100kg",
    "image": null
  }'
```

### Response (Streaming)
```
🚀 Hệ thống đã tiếp nhận yêu cầu...
🔍 AI tra cứu giá Database cho trấu nghiền...
📊 Thông tin nhanh từ hệ thống:
- [Mã: SP001] Tên sản phẩm: Trấu nghiền | Đơn giá: 50000 VNĐ/1kg | Tồn kho hiện tại: 5000 kg

Trấu nghiền hiện có giá 50,000 VNĐ/1kg.
Với mua 100kg, tổng tiền: 5,000,000 VNĐ.

[ACTION_BUY: SP001 | Trấu nghiền | 100]

[SUGGESTION] Bạn có muốn mua thêm bã mía không?
[SUGGESTION] Có kho nào gần khu vực Long An không?
[SUGGESTION] Giá giao hàng bao nhiêu?
```

### Image Recognition (CLIP)
```bash
curl -X POST http://localhost:5000/chat \
  -H "Content-Type: application/json" \
  -d '{
    "session_id": "user_123",
    "question": "Cái này là gì?",
    "image": "data:image/png;base64,iVBORw0KGgo..."
  }'
```

---

## 🌾 Ví Dụ Sử Dụng Thực Tế

### Usecase 1: Mua Sản Phẩm (SQL + Tính Tiền)
**👤 User**: Tôi muốn mua 50kg trấu nghiền

**🤖 AI**:
```
Trấu nghiền hiện có sẵn trong kho.
Đơn giá: 50,000 VNĐ/1kg
Tổng tiền: 50 × 50,000 = 2,500,000 VNĐ

[ACTION_BUY: SP001 | Trấu nghiền | 50]

[SUGGESTION] Bạn có muốn mua thêm bã mía không?
[SUGGESTION] Có kho nào gần khu vực Long An không?
[SUGGESTION] Giá giao hàng bao nhiêu?
```

### Usecase 2: Hỏi Định Nghĩa & Công Dụng (RAG)
**👤 User**: Bã mía có thể dùng làm gì?

**🤖 AI**:
```
Bã mía (bagasse) là phụ phẩm sau khi ép nước mía.

Ứng dụng chính:
- 🔥 Sản xuất biochar để xử lý nước thải
- 🏗️ Ứng dụng xây dựng (vật liệu composite)
- ⚡ Sản xuất nhiên liệu sinh học
- 🌱 Phân bón hữu cơ

Thông số kỹ thuật:
- Carbon content: 45-50%
- Độ ẩm: 10-15%

---
📚 Nguồn: Sugarcane_Bagasse.pdf | Sugarcane_Biochar_Adha_2026.pdf

[SUGGESTION] Giá bã mía bao nhiêu?
[SUGGESTION] Làm sao để sản xuất biochar từ bã mía?
[SUGGESTION] Kho nào có bã mía sẵn?
```

### Usecase 3: Hỏi Danh Sách Sản Phẩm
**👤 User**: Cửa hàng có bao nhiêu loại sản phẩm?

**🤖 AI**:
```
Cửa hàng hiện có 8 loại sản phẩm phụ phẩm:

- [Mã: SP001] Trấu nghiền | Giá: 50,000 VNĐ/1kg | Tồn: 5,000 kg
- [Mã: SP002] Bã mía | Giá: 35,000 VNĐ/1kg | Tồn: 2,500 kg
- [Mã: SP003] Xơ dừa | Giá: 45,000 VNĐ/1kg | Tồn: 1,200 kg
- ...

Bạn muốn mua loại nào?

[SUGGESTION] Tôi muốn mua tất cả
[SUGGESTION] Mua 100kg trấu nghiền
[SUGGESTION] Giá nào rẻ nhất?
```

### Usecase 4: Mua Nhiều Sản Phẩm (Chống Gộp Thẻ)
**👤 User**: Mua tất cả

**🤖 AI**:
```
Danh sách tất cả sản phẩm:
- [Mã: SP001] Trấu nghiền | Giá: 50,000 VNĐ/1kg | Tồn: 5,000 kg
- [Mã: SP002] Bã mía | Giá: 35,000 VNĐ/1kg | Tồn: 2,500 kg
- [Mã: SP003] Xơ dừa | Giá: 45,000 VNĐ/1kg | Tồn: 1,200 kg

Tôi sẽ tạo đơn mua cho tất cả sản phẩm.

[ACTION_BUY: SP001 | Trấu nghiền | 0]
[ACTION_BUY: SP002 | Bã mía | 0]
[ACTION_BUY: SP003 | Xơ dừa | 0]

[SUGGESTION] Bạn muốn mua bao nhiêu kg của mỗi loại?
[SUGGESTION] Có nơi giao hàng ưu tiên không?
[SUGGESTION] Thanh toán bằng cách nào?
```

---

## ⚠️ Ghi Chú Quan Trọng

### Lần Khởi Động Đầu Tiên

| Bước | Thời Gian | Ghi Chú |
|------|-----------|--------|
| Indexing FAISS | 15-30 phút | Tuỳ số lượng PDF/CSV |
| Tải model QWEN 2.5 | 5-10 phút | ~4.4GB lần đầu |
| Tải CLIP model | 2-3 phút | ~300MB |
| **Tổng cộng** | **22-43 phút** | ☕ Uống cà phê chờ đã |

### Lần Khởi Động Tiếp Theo
- ✅ Không cập nhật tài liệu → Chỉ 30 giây (load FAISS từ cache)
- 🔄 Cập nhật tài liệu → Chạy lại `indexer_local.py` (15-30 phút)

### Xử Lý Lỗi Phổ Biến

#### Lỗi 1: ConnectionError: localhost:11434
```
→ OLLAMA chưa chạy
→ Fix: ollama serve
```

#### Lỗi 2: FileNotFoundError: faiss_db_local
```
→ Chưa chạy indexing
→ Fix: python indexer_local.py
```

#### Lỗi 3: MSSQL Connection Error
```
→ Sửa DB_CONNECTION_STRING trong agent_server_local.py
→ Hoặc chạy lại với DB mock (disable SQL queries)
```

#### Lỗi 4: AI trả lời không chính xác / bịa đặt
```
→ Tăng chi tiết trong prompt
→ Kiểm tra FAISS có đúng metadata không
→ Xem log chi tiết: tail -f server.log
```

---

## 📋 Lưu Ý Vận Hành (Quan trọng nhất)

1. **SQL connection đúng và dữ liệu tồn kho/giá phải chính xác** — AI sẽ đọc trực tiếp dữ liệu SQL để đưa báo giá/tồn kho (RẤT QUAN TRỌNG).

2. **Firebase credential phải tồn tại và hợp lệ** nếu bạn dùng các chức năng liên quan.

3. **Đặt ảnh mẫu (ReferenceImages)** để AI có thể nhận diện ảnh người dùng upload.

4. **Tạo hoặc tải Faiss index** (`faiss_db_local`) trước khi dùng AI — nếu không, RAG sẽ không hoạt động.

5. **QWEN 2.5 là bắt buộc** — Nếu không có, cần chỉnh `agent_server_local.py` để gọi service LLM bạn có (OpenAI / llama.cpp wrapper / local model khác).

6. **Bảo mật: KHÔNG commit** các credential (firebase keys, email passwords, connection strings) vào repo công khai.

---

## 📁 Các script & file hữu ích

- `DACS/Services/AI_Engine/agent_server_local.py` — 🔴 Main server (FastAPI + LangGraph)
- `DACS/Services/AI_Engine/indexer_local.py` — 🟡 Chuyển PDF/CSV thành vector FAISS
- `DACS/Services/PythonBridge.cs` — cầu nối nếu ASP.NET cần gọi python scripts nội bộ
- `DACS/Services/SocketServer.cs` + `DACS/Hubs/ChatHub` — real-time chat
- `DACS/Services/BlockchainService.cs` — tích hợp Nethereum
- `Mau_Import_SanPham.xlsx` — mẫu import sản phẩm
- `SQLQueryprovip.sql` — câu truy vấn / helper SQL

---

## 🚀 Triển khai (gợi ý)

- **Production**: Tách AI service lên máy riêng hoặc container (Docker) để dễ scale. Cần GPU cho hiệu năng CLIP/embeddings tốt hơn.
- **Webapp (DACS)**: Deploy như một App Service / VM / Container; cấu hình biến môi trường cho connection string & credentials.
- **Docker**: Nếu muốn chạy toàn bộ trong Docker, cần tạo Dockerfile cho cả .NET app và Python AI service, và docker-compose để link network.

---

## 🐛 Debug & Troubleshooting

- **AI server báo "AI chưa sẵn sàng"**: Kiểm tra `faiss_db_local` tồn tại và `embedding_model` được load, kiểm tra logs của uvicorn.
- **CLIP không detect ảnh**: Kiểm tra `DACS/Services/AI_Engine/KnowledgeBase/ReferenceImages` có ảnh hợp lệ hay không; xem output logs khi khởi động.
- **Lỗi SQL**: Kiểm tra `DB_CONNECTION_STRING` trong `agent_server_local.py` và `DefaultConnection` trong `appsettings.json`.
- **Firebase lỗi credential**: Kiểm tra đường dẫn `GOOGLE_APPLICATION_CREDENTIALS` và kiểm tra nội dung `firebase_config.json`.
- **Uvicorn không start**: Kiểm tra port 5000 có đang bị chiếm không. Dùng `lsof -i :5000` (Linux/Mac) hoặc `netstat -ano | findstr :5000` (Windows).

---

## ✅ Checklist trước khi chạy lần đầu

- [ ] Đã đặt `DefaultConnection` tới SQL Server và chạy migrations
- [ ] `firebase_config.json` hợp lệ và có trong `DACS/`
- [ ] Đã cài Python deps và tạo `faiss_db_local` (hoặc có ready DB)
- [ ] Đã đặt ReferenceImages phù hợp trong `KnowledgeBase/ReferenceImages`
- [ ] Đã cấu hình Email/SMS nếu dùng
- [ ] **OLLAMA đã chạy** (`ollama serve`) trên terminal riêng
- [ ] **QWEN 2.5 đã pull** (`ollama pull qwen2.5:7b`)
- [ ] Khởi chạy AI server (`uvicorn agent_server_local:app --host 0.0.0.0 --port 5000`) trước khi chạy webapp

---

## 🎯 Roadmap

- [ ] Web UI (Streamlit / React)
- [ ] Multi-language support (Tiếng Anh, Trung Quốc)
- [ ] Webhook integration (gửi ORDER sang ERP)
- [ ] SMS/Email notification
- [ ] Analytics dashboard
- [ ] Fine-tune QWEN trên dữ liệu phụ phẩm nông nghiệp
- [ ] Docker Compose setup

---

## 📄 License & Credits

**Author**: ngokyhod  
**Version**: 1.0  
**Updated**: 2026-08-25

**Hỗ trợ**: Hỏi trong repo hoặc GitHub Discussions! 🚀
