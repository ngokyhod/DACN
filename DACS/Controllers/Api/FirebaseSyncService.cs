using FirebaseAdmin.Auth;
using Google.Cloud.Firestore; // Cần cài thêm gói Google.Cloud.Firestore nếu dùng Firestore

public class FirebaseSyncService
{
    // 1. Đồng bộ tài khoản sang Firebase Auth
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
    public async Task AddThuGomToFirestoreAsync(Dictionary<string, object> data)
    {
        try
        {
            // 1. Kết nối Firestore
            FirestoreDb db = FirestoreDb.Create("ppnongnghiep"); // Đảm bảo ProjectID đúng

            // 2. Tham chiếu Collection "ThuGom"
            CollectionReference colRef = db.Collection("ThuGom");

            // 3. Thêm document mới (Để Firestore tự sinh ID document)
            await colRef.AddAsync(data);

            Console.WriteLine("[Firestore] Đã đẩy yêu cầu thu gom lên thành công.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Firestore LỖI]: {ex.Message}");
            // Ném lỗi để bên Controller bắt được và log lại
            throw;
        }
    }
}