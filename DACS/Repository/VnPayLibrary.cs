using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace DACS.PTTT
{
    public class VnPayLibrary
    {
        private SortedList<string, string> _requestData = new SortedList<string, string>(new VnPayCompare());
        private SortedList<string, string> _responseData = new SortedList<string, string>(new VnPayCompare());

        // Thêm dữ liệu gửi đi (Checkout)
        public void AddRequestData(string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _requestData.Add(key, value);
            }
        }

        // Thêm dữ liệu nhận lại (Return)
        public void AddResponseData(string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _responseData.Add(key, value);
            }
        }

        // Lấy dữ liệu trả về
        public string GetResponseData(string key)
        {
            return _responseData.TryGetValue(key, out string value) ? value : string.Empty;
        }

        // Tạo URL
        public string CreateRequestUrl(string baseUrl, string vnp_HashSecret)
        {
            StringBuilder data = new StringBuilder();

            foreach (var kv in _requestData)
            {
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    data.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
                }
            }

            string rawQuery = data.ToString().TrimEnd('&');
            string secureHash = HmacSHA512(vnp_HashSecret, rawQuery);

            string paymentUrl = $"{baseUrl}?{rawQuery}&vnp_SecureHash={secureHash}";
            return paymentUrl;
        }

        // KIỂM TRA CHỮ KÝ – BẢN CHUẨN NHẤT
        public bool ValidateSignature(string inputHash, string secretKey)
        {
            // Lấy danh sách tham số (bỏ hash)
            StringBuilder data = new StringBuilder();

            foreach (var kv in _responseData)
            {
                if (kv.Key == "vnp_SecureHash" || kv.Key == "vnp_SecureHashType")
                    continue; // bỏ qua

                data.Append(kv.Key + "=" + kv.Value + "&");
            }

            // Xóa dấu & cuối
            if (data.Length > 0)
                data.Length -= 1;

            // Hash lại
            string myHash = HmacSHA512(secretKey, data.ToString());

            // So sánh không phân biệt hoa/thường
            return myHash.Equals(inputHash, StringComparison.InvariantCultureIgnoreCase);
        }

        // SHA512
        private string HmacSHA512(string key, string inputData)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] inputBytes = Encoding.UTF8.GetBytes(inputData);

            using (var hmac = new HMACSHA512(keyBytes))
            {
                byte[] hashValue = hmac.ComputeHash(inputBytes);
                StringBuilder sb = new StringBuilder();

                foreach (var b in hashValue)
                {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        }
    }

    // So sánh từ a-z
    public class VnPayCompare : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            var compare = CompareInfo.GetCompareInfo("en-US");
            return compare.Compare(x, y, CompareOptions.Ordinal);
        }
    }
}
