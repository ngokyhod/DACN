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
- 🤖 Tư vấn AI với RAG (Retrieval-Augmented Generation)
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
      agent_server_local.py # Local agent server
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
- AI engine (Python) chạy độc lập (FastAPI) trên cổng mặc định 5001; webapp gọi endpoint `/chat` để hỏi AI. AI engine dùng FAISS/embeddings + BM25 + cross-encoder để RAG [...]
- Dữ liệu động (giá, tồn kho, lịch sử chat AI) được lấy trực tiếp từ SQL Server bằng SQLAlchemy trong Python (DB_CONNECTION_STRING) và bằng EF Core trong C#.

---

## Tính năng chính
- Mua / Bán sản phẩm (giỏ hàng, đặt hàng, trạng thái đơn)
- Thu gom sản phẩm từ nông dân (form & quy trình)
- Tư vấn AI bằng tiếng Việt (RAG): trả lời dựa trên 2 nguồn — dữ liệu SQL (giá, tồn kho, kho bãi) và tài liệu PDF trong KnowledgeBase (cho tư vấn sản phẩm)
- Nhận diện ảnh sản phẩm bằng CLIP (ReferenceImages)
- Chat thời gian thực via SignalR + socket server
- Email & SMS gửi thông báo
- Blockchain (Nethereum) cho truy xuất/ghi giao dịch traceability
- API cho mobile (có thư mục Controllers/Api)

---

## Yêu cầu trước khi cài
- Hệ điều hành: Linux / Windows (phù hợp môi trường dev)
- .NET SDK: 6.0+ (WebApplication.CreateBuilder — dùng .NET 6/7 đều chạy)
- Visual Studio / dotnet CLI
- SQL Server (local hoặc remote) với database tương thích schema của dự án
- Python 3.10+ (khuyến nghị) với pip
- Nếu muốn dùng CLIP + acceleration: GPU + CUDA (nếu không có, chạy trên CPU nhưng chậm)
- OLLAMA hoặc local LLM endpoint nếu muốn (agent_server_local.py dùng ChatOllama(model="llama3") — cần điều chỉnh tùy môi trường LLM)
- FAISS: cài faiss-cpu hoặc faiss-gpu tùy cấu hình

---

## Cài đặt & cấu hình nhanh (local)

1) Clone repo
```bash
git clone https://github.com/ngokyhod/saikahana-web.git
cd saikahana-web
```

2) Cấu hình database & appsettings (DACS/appsettings.json)
- Mở `DACS/appsettings.json` và chỉnh:
  - ConnectionStrings: `DefaultConnection` → chuỗi kết nối tới SQL Server của bạn (ví dụ: Server=.;Database=QuanLyPhuPham;Trusted_Connection=True;)
  - EmailSettings: host/port/username/password/from
  - ESmsSettings: cấu hình nhà cung cấp SMS nếu dùng
- File firebase: `DACS/firebase_config.json` phải chứa credential service account của Firebase. Program.cs đặt biến môi trường:
  - GOOGLE_APPLICATION_CREDENTIALS -> đường dẫn tới `firebase_config.json`
  - Nếu chạy local, đảm bảo file tồn trong `DACS/` hoặc đặt đường dẫn tuyệt đối

3) Cài .NET dependencies & migrations
- Tạo database & apply migrations (nếu dùng EF Migrations có sẵn):
```bash
cd DACS
# Nếu bạn dùng dotnet-ef:
dotnet tool install --global dotnet-ef
dotnet restore
dotnet ef database update
# hoặc mở solution DACS.sln trong Visual Studio và update database bằng Package Manager Console
```

4) Cài Python dependencies cho AI engine
- Tạo virtualenv và cài:
```bash
cd DACS/Services/AI_Engine
python -m venv .venv
# Linux
source .venv/bin/activate
# Windows
# .venv\Scripts\activate

pip install --upgrade pip
pip install uvicorn fastapi pydantic numpy torch pillow langchain_huggingface langchain_community langchain_ollama sentence-transformers faiss-cpu rank_bm25 cross-encoder sqlalchemy google-auth f[...]
# (Tên package có thể khác; tùy hệ thống GPU vs CPU chọn faiss-gpu hoặc faiss-cpu)
```

5) Chuẩn bị KnowledgeBase và ReferenceImages
- Thư mục: `DACS/Services/AI_Engine/KnowledgeBase/`
  - Đặt PDF tài liệu (dùng cho RAG). Các file PDF sẽ được serve tĩnh từ webapp (Program.cs maps PhysicalFileProvider → /pdfs).
  - Tạo `ReferenceImages` dưới KnowledgeBase và bỏ ảnh mẫu (png/jpg/webp). CLIP sẽ mã hóa ảnh để so khớp.
- Tạo Faiss DB (nếu repo chưa có): chạy script indexer (nếu có) hoặc dùng `indexer_local.py` để tạo `faiss_db_local/`.

6) Cấu hình biến môi trường cho AI/Python
- (Ví dụ Linux / macOS)
```bash
export GOOGLE_APPLICATION_CREDENTIALS="/path/to/DACS/firebase_config.json"
export DB_CONNECTION_STRING="mssql+pyodbc://<SERVER>/<DB>?driver=ODBC+Driver+17+for+SQL+Server&trusted_connection=yes&TrustServerCertificate=yes"
# (Bạn có thể chỉnh trực tiếp biến trong agent_server_local.py hoặc dùng conf file)
```

7) Chạy AI server (local)
- Từ thư mục `DACS/Services/AI_Engine/`:
```bash
# Chạy agent server
uvicorn agent_server_local:app --host 0.0.0.0 --port 5001
```
- Lưu ý: agent_server_local.py chứa endpoint POST `/chat` để trả lời streaming; webapp tự gọi endpoint này để giao tiếp với AI.

8) Chạy Webapp ASP.NET (DACS)
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

9) Kết nối mobile / API
- API dành cho mobile được triển khai trong `DACS/Controllers/Api` và các controller khác. Kiểm tra route (ở Program.cs routing/MapControllers). Dùng base URL của webapp (ví d:[[...]

---

## Cấu hình quan trọng (chi tiết)
- `DACS/appsettings.json`:
  - "ConnectionStrings": { "DefaultConnection": "<chuoi ket noi SQL Server>" }
  - "EmailSettings": { Host, Port, Username, Password, From }
  - "ESmsSettings": { /* tùy provider */ }
- Firebase:
  - File `DACS/firebase_config.json` chứa service account JSON.
  - Program.cs đặt GOOGLE_APPLICATION_CREDENTIALS tới file này.
- AI engine:
  - DB path for FAISS: `DB_PATH = "./faiss_db_local"` trong agent_server_local.py
  - LLM: agent_server_local.py sử dụng ChatOllama(model="llama3") — bạn cần cấu hình môi trường cho Ollama (hoặc chỉnh LLM sang một provider khác).
  - CLIP model: tải tự động "ViT-L/14" nếu có GPU/CUDA; nếu không, chạy chậm trên CPU.
- Reference images: `DACS/Services/AI_Engine/KnowledgeBase/ReferenceImages` — tên file sẽ được dùng làm tag/label khi CLIP nhận diện.

---

## Lưu ý vận hành & những bước quan trọng (Quan trọng nhất)
1. SQL connection đúng và dữ liệu tồn kho/giá phải chính xác — AI sẽ đọc trực tiếp dữ liệu SQL để đưa báo giá/tồn kho (RẤT QUAN TRỌNG).
2. Firebase credential phải tồn tại và hợp lệ nếu bạn dùng các chức năng liên quan.
3. Đặt ảnh mẫu (ReferenceImages) để AI có thể nhận diện ảnh người dùng upload.
4. Tạo hoặc tải Faiss index (`faiss_db_local`) trước khi dùng AI — nếu không, RAG sẽ không hoạt động.
5. Kiểm tra LLM / ollama: nếu không có LLM local tương thích, chỉnh agent_server_local.py để gọi service LLM bạn có (OpenAI / llama.cpp wrapper / local model).
6. Bảo mật: KHÔNG commit các credential (firebase keys, email passwords, connection strings) vào repo công khai.

---

## Các script & file hữu ích
- `DACS/Services/AI_Engine/agent_server_local.py` — agent server (RAG + CLIP + SQL)
- `DACS/Services/PythonBridge.cs` — cầu nối nếu ASP.NET cần gọi python scripts nội bộ
- `DACS/Services/SocketServer.cs` + `DACS/Hubs/ChatHub` — real-time chat
- `DACS/Services/BlockchainService.cs` — tích hợp Nethereum
- `Mau_Import_SanPham.xlsx` — mẫu import sản phẩm
- `SQLQueryprovip.sql` — câu truy vấn / helper SQL

---

## Triển khai (gợi ý)
- Production: tách AI service lên máy riêng hoặc container (Docker) để dễ scale. Cần GPU cho hiệu năng CLIP/embeddings tốt hơn.
- Webapp (DACS) deploy như một App Service / VM / Container; cấu hình biến môi trường cho connection string & credentials.
- Nếu muốn chạy toàn bộ trong Docker, cần tạo Dockerfile cho cả .NET app và Python AI service, và docker-compose để link network.

---

## Debug & Troubleshooting
- AI server báo "AI chưa sẵn sàng": kiểm tra `faiss_db_local` tồn tại và `embedding_model` được load, kiểm tra logs của uvicorn.
- CLIP không detect ảnh: kiểm tra `DACS/Services/AI_Engine/KnowledgeBase/ReferenceImages` có ảnh hợp lệ hay không; xem output logs khi khởi động.
- Lỗi SQL: kiểm tra `DB_CONNECTION_STRING` trong agent_server_local.py và `DefaultConnection` trong appsettings.json.
- Firebase lỗi credential: kiểm tra đường dẫn `GOOGLE_APPLICATION_CREDENTIALS` và kiểm tra nội dung `firebase_config.json`.

---

## Chúc mừng — Checklist trước khi chạy lần đầu
- [ ] Đã đặt `DefaultConnection` tới SQL Server và chạy migrations
- [ ] `firebase_config.json` hợp lệ và có trong `DACS/`
- [ ] Đã cài Python deps và tạo `faiss_db_local` (hoặc có ready DB)
- [ ] Đã đặt ReferenceImages phù hợp
- [ ] Đã cấu hình Email/SMS nếu dùng
- [ ] Khởi chạy AI server (uvicorn) trước khi chạy webapp hoặc đảm bảo webapp biết endpoint AI

---

## Kết luận
README này tóm tắt chi tiết những gì quan trọng để khởi chạy và vận hành Saikahana Web.
