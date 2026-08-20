# 📹 Demo & Screenshots

## Video giới thiệu

[![Saikahana Web Demo](https://img.youtube.com/vi/brVFc_q7lI0/maxresdefault.jpg)](https://www.youtube.com/watch?v=brVFc_q7lI0)

**Xem demo đầy đủ về các tính năng chính của Saikahana Web:**
- 🛒 Mua / Bán sản phẩm phụ phẩm nông nghiệp
- 🤖 Tư vấn AI với RAG (Retrieval-Augmented Generation)
- 💬 Chat real-time với SignalR
- 📸 Nhận diện ảnh sản phẩm bằng CLIP
- 📱 API cho ứng dụng mobile

---

## 🎬 Cách up video vào GitHub

### **Cách 1: Upload video lên YouTube (Khuyến nghị nhất)** ✅ *Đã thực hiện*

**Bước 1: Upload video lên YouTube**
1. Vào https://www.youtube.com/
2. Click vào avatar → "Tạo video hoặc bài đăng" → "Tải lên video"
3. Chọn file video của bạn
4. Điền tiêu đề, mô tả (ví dụ: "Saikahana Web - Demo")
5. Chọn mức riêng tư: Public / Unlisted (tùy bạn)
6. Publish

**Bước 2: Lấy Video ID**
- URL YouTube sẽ như thế này: `https://www.youtube.com/watch?v=**brVFc_q7lI0**`
- `brVFc_q7lI0` là Video ID

**Bước 3: Thêm vào DEMO.md**
```markdown
[![Saikahana Web Demo](https://img.youtube.com/vi/brVFc_q7lI0/maxresdefault.jpg)](https://www.youtube.com/watch?v=brVFc_q7lI0)
```

---

### **Cách 2: Upload video nhỏ trực tiếp lên GitHub (cho video < 10MB)**

**Bước 1: Tạo folder `assets` (nếu chưa có)**
```bash
mkdir assets
```

**Bước 2: Upload video vào GitHub**

**Trên Web (GitHub.com):**
1. Vào repo → folder `assets`
2. Click "Add file" → "Upload files"
3. Kéo thả file video hoặc chọn từ máy
4. Commit (thêm message "Add demo video")

**Hoặc dùng Git CLI:**
```bash
# Copy video vào folder assets
cp /path/to/your/video.mp4 assets/

# Add & commit
git add assets/demo-video.mp4
git commit -m "Add demo video"
git push
```

**Bước 3: Thêm link video vào DEMO.md**
```markdown
![Saikahana Web Demo](./assets/demo-video.mp4)
```

⚠️ **Lưu ý:** GitHub không hỗ trợ play video trực tiếp, nhưng sẽ hiển thị như attachment.

---

### **Cách 3: Dùng GitHub Releases (cho file lớn)**

**Bước 1: Tạo Release**
1. Vào repo → "Releases" → "Create a new release"
2. Điền tag version (ví dụ: `v1.0-demo`)
3. Tiêu đề: "Demo Video v1.0"
4. Tải file video lên phần "Attach binaries"
5. Publish Release

**Bước 2: Copy link download từ Release**
```markdown
[Download Demo Video](https://github.com/ngokyhod/saikahana-web/releases/download/v1.0-demo/demo-video.mp4)
```

---

## 📊 So sánh 3 cách:

| Cách | Ưu điểm | Nhược điểm |
|------|--------|-----------|
| **YouTube** | ✅ Chất lượng HD, dễ xem, miễn phí | ❌ Phải up lên nền tảng khác |
| **GitHub Assets** | ✅ Tất cả ở 1 chỗ, offline | ❌ Giới hạn dung lượng, không hỗ trợ play trực tiếp |
| **GitHub Releases** | ✅ Chuyên nghiệp, versioning | ❌ Download thay vì xem trực tiếp |

---

## 📝 Hướng dẫn cơ bản nhanh gọn

Với video YouTube của bạn:
1. ✅ Video ID: `brVFc_q7lI0`
2. ✅ Thumbnail tự động: `https://img.youtube.com/vi/brVFc_q7lI0/maxresdefault.jpg`
3. ✅ Link: `https://www.youtube.com/watch?v=brVFc_q7lI0`

Mọi thứ đã được thiết lập sẵn trong file DEMO.md này! 🎉
