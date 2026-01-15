using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenCvSharp;
using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;

namespace DACS.Services
{
    public class ImageResult
    {
        public string FileName { get; set; }
        public double Score { get; set; } // Điểm giống nhau (0.0 -> 1.0)
    }

    public class ImageService
    {
        // 1. So sánh Hash (Dấu vân tay ảnh)
        public double CompareHash(string path1, string path2)
        {
            try
            {
                var algo = new AverageHash();
                // CoenM.ImageHash cần Stream để đọc
                using var stream1 = File.OpenRead(path1);
                using var stream2 = File.OpenRead(path2);

                var hash1 = algo.Hash(stream1);
                var hash2 = algo.Hash(stream2);

                // SỬA Ở ĐÂY: Gọi đầy đủ namespace
                return CoenM.ImageHash.CompareHash.Similarity(hash1, hash2) / 100.0;
            }
            catch { return 0.0; }
        }

        // 2. So sánh Histogram (Màu sắc)
        public double CompareHistogram(string path1, string path2)
        {
            try
            {
                using var img1 = Cv2.ImRead(path1);
                using var img2 = Cv2.ImRead(path2);
                if (img1.Empty() || img2.Empty()) return 0.0;

                using var hsv1 = new Mat();
                using var hsv2 = new Mat();

                // Chuyển sang HSV để tách biệt độ sáng
                Cv2.CvtColor(img1, hsv1, ColorConversionCodes.BGR2HSV);
                Cv2.CvtColor(img2, hsv2, ColorConversionCodes.BGR2HSV);

                // Chỉ lấy kênh H (Màu - 0) và S (Độ đậm - 1), bỏ qua V (Sáng tối - 2)
                int[] channels = { 0, 1 };
                int[] h_bins = { 50, 60 }; // Chia nhỏ dải màu
                Rangef[] ranges = { new Rangef(0, 180), new Rangef(0, 256) };

                using var hist1 = new Mat();
                using var hist2 = new Mat();

                Cv2.CalcHist(new[] { hsv1 }, channels, null, hist1, 2, h_bins, ranges);
                Cv2.CalcHist(new[] { hsv2 }, channels, null, hist2, 2, h_bins, ranges);

                Cv2.Normalize(hist1, hist1, 0, 1, NormTypes.MinMax);
                Cv2.Normalize(hist2, hist2, 0, 1, NormTypes.MinMax);

                // Dùng phương pháp Intersection hoặc Bhattacharyya sẽ tốt hơn Correl cho màu sắc
                return Cv2.CompareHist(hist1, hist2, HistCompMethods.Correl);
            }
            catch { return 0.0; }
        }

        // 3. So sánh Keypoints (ORB - Đặc điểm ảnh)
        public double CompareFeatures(string path1, string path2)
        {
            try
            {
                using var img1 = Cv2.ImRead(path1, ImreadModes.Grayscale);
                using var img2 = Cv2.ImRead(path2, ImreadModes.Grayscale);
                if (img1.Empty() || img2.Empty()) return 0.0;

                // Dùng ORB (Thay cho SIFT vì nhanh và free)
                using var orb = ORB.Create(1000);
                using var descriptors1 = new Mat();
                using var descriptors2 = new Mat();
                KeyPoint[] keypoints1, keypoints2;

                orb.DetectAndCompute(img1, null, out keypoints1, descriptors1);
                orb.DetectAndCompute(img2, null, out keypoints2, descriptors2);

                if (descriptors1.Empty() || descriptors2.Empty()) return 0.0;

                // So khớp
                using var matcher = new BFMatcher(NormTypes.Hamming, crossCheck: true);
                var matches = matcher.Match(descriptors1, descriptors2);

                if (matches.Length == 0) return 0.0;

                double maxKeypoints = Math.Max(keypoints1.Length, keypoints2.Length);
                if (maxKeypoints == 0) return 0.0;

                // Tỷ lệ điểm trùng khớp
                return (double)matches.Length / maxKeypoints;
            }
            catch { return 0.0; }
        }
        public double CompareTexture(string path1, string path2)
        {
            try
            {
                // Hàm phụ tính độ thô (Variance of Laplacian)
                double GetRoughness(string path)
                {
                    using var img = Cv2.ImRead(path, ImreadModes.Grayscale);
                    using var laplacian = new Mat();
                    // Tính đạo hàm bậc 2 để tìm cạnh/hạt
                    Cv2.Laplacian(img, laplacian, MatType.CV_64F);

                    // Tính độ lệch chuẩn (Standard Deviation) -> càng cao càng thô
                    Cv2.MeanStdDev(laplacian, out var mean, out var stddev);
                    return stddev.Val0 * stddev.Val0; // Variance
                }

                double roughness1 = GetRoughness(path1);
                double roughness2 = GetRoughness(path2);

                // So sánh tỉ lệ độ thô (Càng gần 1 càng giống)
                double ratio = Math.Min(roughness1, roughness2) / Math.Max(roughness1, roughness2);
                return ratio; // Trả về 0.0 -> 1.0
            }
            catch { return 0.0; }
        }
        // --- HÀM TỔNG HỢP: CHẠY HẾT CÁC THUẬT TOÁN ---
        public ImageResult? FindBestMatch(string uploadedPath, string datasetFolder)
        {
            var results = new List<ImageResult>();

            // Kiểm tra thư mục có tồn tại không
            if (!Directory.Exists(datasetFolder)) return null;

            var files = Directory.GetFiles(datasetFolder, "*.*")
                .Where(s => s.EndsWith(".jpg") || s.EndsWith(".png") || s.EndsWith(".jpeg"));

            foreach (var file in files)
            {
                // Trọng số (Bạn có thể điều chỉnh lại)
                double wHist = 0.35; // Màu sắc
                double wText = 0.35; // Kết cấu (sần sùi, mịn...)
                double wFeat = 0.20; // Chi tiết đặc trưng (góc cạnh)
                double wHash = 0.10; // Hình dáng tổng quát

                double d1_Hash = CompareHash(uploadedPath, file);
                double d2_Hist = CompareHistogram(uploadedPath, file);
                double d3_Feat = CompareFeatures(uploadedPath, file);
                double d4_Text = CompareTexture(uploadedPath, file);

                // Tính điểm tổng hợp
                double finalScore = (d1_Hash * wHash) +
                                    (d2_Hist * wHist) +
                                    (d3_Feat * wFeat) +
                                    (d4_Text * wText);

                results.Add(new ImageResult
                {
                    FileName = Path.GetFileName(file),
                    Score = finalScore
                });
            }

            // --- LOGIC MỚI: LỌC THEO NGƯỠNG ĐIỂM ---

            // 1. Sắp xếp giảm dần (điểm cao nhất lên đầu)
            var bestMatch = results.OrderByDescending(r => r.Score).FirstOrDefault();

            // 2. Đặt ngưỡng tối thiểu (Ví dụ: 0.65 tức là giống 65%)
            // Bạn nên test thử:
            // - Nếu ảnh lung tung vẫn lọt qua -> Tăng lên 0.7 hoặc 0.75
            // - Nếu ảnh đúng mà bị loại -> Giảm xuống 0.55 hoặc 0.6
            double minThreshold = 0.55;

            if (bestMatch != null && bestMatch.Score >= minThreshold)
            {
                return bestMatch; // Tìm thấy ảnh giống
            }

            return null; // Không có ảnh nào đủ giống (trả về null)
        }
    }
}