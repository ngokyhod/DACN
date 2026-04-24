using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using Google.Cloud.Storage.V1; // Cần cài thêm gói Google.Cloud.Firestore nếu dùng Firestore

public class FirebaseSyncService
{
    private readonly FirestoreDb _firestoreDb;
    private readonly string _projectId;
    private readonly string _bucketName; // Tên kho chứa ảnh
    private readonly IWebHostEnvironment _env; // Môi trường web
    public FirebaseSyncService(IConfiguration configuration, IWebHostEnvironment env)
    {
        _env = env;
        _projectId = configuration["Firebase:ProjectId"] ?? "ppnongnghiep";
        // Tên bucket: thường là project-id.appspot.com
        _bucketName = configuration["Firebase:StorageBucket"] ?? $"{_projectId}.appspot.com";

        try
        {
            // Biến môi trường GOOGLE_APPLICATION_CREDENTIALS phải được set trước đó
            // hoặc server đã được cấp quyền.
            _firestoreDb = FirestoreDb.Create(_projectId);
            Console.WriteLine($"[FirebaseSyncService] Kết nối Firestore thành công tới: {_projectId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Lỗi khởi tạo Firestore]: {ex.Message}");
            // _firestoreDb sẽ là null nếu khởi tạo thất bại
        }
    }
    public async Task<string> CreateFirebaseUserAsync(string email, string password, string displayName, string phoneNumber)
    {
        try
        {
            var userArgs = new UserRecordArgs()
            {
                Email = email,
                EmailVerified = false,
                Password = password,
                DisplayName = displayName,
                PhoneNumber = phoneNumber, // Lưu ý: SĐT phải chuẩn E.164 (+84...)
                Disabled = false,
            };

            UserRecord userRecord = await FirebaseAuth.DefaultInstance.CreateUserAsync(userArgs);
            return userRecord.Uid; // Trả về UID để lưu vào SQL
        }
        catch (FirebaseAuthException ex)
        {
            // Xử lý lỗi (ví dụ: Email đã tồn tại trên Firebase)
            return null;
        }
    }

    // 2. Đồng bộ thông tin sang Firestore (Tùy chọn)
    public async Task SaveToFirestoreAsync(string uid, string fullName, string email, string phone)
    {
        try
        {
            // 1. KẾT NỐI: Chỗ này phải đúng Project ID trong file JSON của bạn
            // Trong file json bạn gửi, project_id là "ppnongnghiep"
            FirestoreDb db = FirestoreDb.Create("ppnongnghiep");

            // 2. CHỈ ĐỊNH NGĂN KÉO: Tìm đúng ngăn có mã UID này
            DocumentReference docRef = db.Collection("users").Document(uid);

            // 3. CHUẨN BỊ ĐỒ ĐẠC: Dữ liệu cần lưu
            Dictionary<string, object> users = new Dictionary<string, object>
        {
            { "uid", uid },
            { "fullName", fullName },
            { "email", email },
            { "phone", phone },
            { "role", "KhachHang" },
            { "createdAt", Timestamp.GetCurrentTimestamp() }, // Timestamp của Firestore
            { "loginMethod", "web_sync" }
        };

            // 4. LƯU: Ghi đè vào ngăn đó
            await docRef.SetAsync(users);

            Console.WriteLine($"[Firestore] Đã lưu thành công cho UID: {uid}");
        }
        catch (Exception ex)
        {
            // Nếu lỗi, nó sẽ hiện ra ở đây (Ví dụ: Chưa cài Google.Cloud.Firestore)
            Console.WriteLine($"[Firestore LỖI]: {ex.Message}");
        }
    }
    public async Task UpdateProductInFirestore(string id, string title, long price, string description)
    {
        // Giả sử collection của bạn tên là "products"
        DocumentReference docRef = _firestoreDb.Collection("SanPham").Document(id);

        Dictionary<string, object> updates = new Dictionary<string, object>
    {
        { "title", title },
        { "price", price },
        { "description", description },
        { "lastUpdated", Timestamp.GetCurrentTimestamp() }
    };

        // UpdateAsync chỉ cập nhật các trường được chỉ định, không ghi đè toàn bộ document
        await docRef.UpdateAsync(updates);
    }
    public async Task<string> UploadImageToStorageAsync(string localRelativePath)
    {
        try
        {
            if (string.IsNullOrEmpty(localRelativePath)) return "";

            // Xóa dấu / đầu dòng để tìm file
            string cleanPath = localRelativePath.TrimStart('/', '\\');
            // Đường dẫn tuyệt đối trên ổ cứng máy tính
            string physicalPath = Path.Combine(_env.WebRootPath, cleanPath);

            if (!File.Exists(physicalPath)) return ""; // Không có file thì bỏ qua

            // Upload
            var storage = await StorageClient.CreateAsync();
            using (var fileStream = File.OpenRead(physicalPath))
            {
                await storage.UploadObjectAsync(_bucketName, cleanPath, null, fileStream);
            }

            // Tạo link Public
            string objectNameEncoded = Uri.EscapeDataString(cleanPath);
            return $"https://firebasestorage.googleapis.com/v0/b/{_bucketName}/o/{objectNameEncoded}?alt=media";
        }
        catch
        {
            return ""; // Lỗi thì trả về rỗng
        }
    }
   public async Task SyncProductsToFirestoreAsync(List<Dictionary<string, object>> productList)
        {
            if (_firestoreDb == null) return;
            try
            {
                CollectionReference productsCol = _firestoreDb.Collection("SanPham");
                WriteBatch batch = _firestoreDb.StartBatch();
                int count = 0;

                foreach (var product in productList)
                {
                    if (!product.ContainsKey("id")) continue;
                    
                    string productId = product["id"].ToString();

                    // === LOGIC MỚI: TỰ ĐỘNG UPLOAD ẢNH ===
                    if (product.ContainsKey("imageUrls"))
                    {
                        string currentImg = product["imageUrls"]?.ToString();
                        // Nếu là ảnh local (không chứa http), thì upload
                        if (!string.IsNullOrEmpty(currentImg) && !currentImg.StartsWith("http"))
                        {
                            Console.WriteLine($"[Storage] Đang upload ảnh: {currentImg}...");
                            string cloudUrl = await UploadImageToStorageAsync(currentImg);
                            
                            if (!string.IsNullOrEmpty(cloudUrl))
                            {
                                product["imageUrls"] = cloudUrl; // Thay thế bằng link online
                            }
                        }
                    }
                    // ======================================

                    DocumentReference docRef = productsCol.Document(productId);
                    batch.Set(docRef, product, SetOptions.MergeAll);
                    count++;

                    if (count >= 400)
                    {
                        await batch.CommitAsync();
                        batch = _firestoreDb.StartBatch();
                        count = 0;
                    }
                }

                if (count > 0) await batch.CommitAsync();
                Console.WriteLine($"[Success] Đã đồng bộ {productList.Count} sản phẩm kèm ảnh Cloud.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error]: {ex.Message}");
                throw;
            }
        }

    public async Task AddThuGomToFirestoreAsync(Dictionary<string, object> data, string sqlRequestId)
    {
        try
        {
            FirestoreDb db = FirestoreDb.Create("ppnongnghiep");

            // Thay vì AddAsync (tự sinh ID), ta dùng Doc(ID).SetAsync
            DocumentReference docRef = db.Collection("ThuGom").Document(sqlRequestId);

            // Thêm trường m_YeuCau vào data để chắc chắn Firestore cũng có mã này
            if (!data.ContainsKey("m_YeuCau"))
            {
                data.Add("m_YeuCau", sqlRequestId);
            }

            await docRef.SetAsync(data);

            Console.WriteLine($"[Firestore] Đã đồng bộ yêu cầu {sqlRequestId} thành công.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Firestore LỖI]: {ex.Message}");
            // Không throw lỗi ở đây để tránh rollback SQL nếu Firestore lỗi (chấp nhận lệch tạm thời)
        }
    }
    public async Task AddDonHangToFirestoreAsync(Dictionary<string, object> data)
    {
        try
        {
            // 1. Kết nối Firestore (Dùng ProjectID của bạn: ppnongnghiep)
            FirestoreDb db = FirestoreDb.Create("ppnongnghiep");

            // 2. Tham chiếu Collection "DonHang"
            CollectionReference colRef = db.Collection("DonHang");

            // 3. Thêm document mới
            await colRef.AddAsync(data);

            Console.WriteLine($"[Firestore] Đã đồng bộ Đơn Hàng {data["maDonHang"]} thành công.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Firestore LỖI]: {ex.Message}");
            // Chỉ log lỗi, không ném exception để tránh làm crash luồng chính của Web
        }
    }
    
}