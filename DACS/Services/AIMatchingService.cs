using System.Net.Http.Json;
using System.Text.Json;
using DACS.Models.ViewModels;

namespace DACS.Services
{
    public class AIMatchingService
    {
        private const string AiServiceBaseUrl = "http://127.0.0.1:5000";
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AIMatchingService> _logger;

        public AIMatchingService(IHttpClientFactory httpClientFactory, ILogger<AIMatchingService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<Dictionary<string, AIKhoHangSuggestionViewModel>> GetWarehouseSuggestionsAsync(
            IEnumerable<object> yeuCauList,
            IEnumerable<object> khoHangList,
            CancellationToken cancellationToken = default)
        {
            var payload = new
            {
                yeu_cau_list = yeuCauList,
                kho_list = khoHangList
            };

            var results = await PostAsync("warehouse-matching", payload, cancellationToken);
            var suggestions = new Dictionary<string, AIKhoHangSuggestionViewModel>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in results)
            {
                var requestId = GetString(item, "m_yeucau");
                var maKho = GetString(item, "ma_kho");
                if (string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(maKho))
                {
                    continue;
                }

                suggestions[requestId] = new AIKhoHangSuggestionViewModel
                {
                    MaKho = maKho,
                    TenKho = GetString(item, "ten_kho"),
                    MatchScore = GetDouble(item, "match_score"),
                    SuggestedReason = GetString(item, "suggested_reason")
                };
            }

            return suggestions;
        }

        public async Task<List<AIGoiYThuMuaViewModel>> GetEnterpriseStockSuggestionsAsync(
            IEnumerable<object> stockLots,
            string nhuCau,
            string tinhDoanhNghiep,
            string? diaChiDoanhNghiep = null,
            double? enterpriseLat = null,
            double? enterpriseLng = null,
            CancellationToken cancellationToken = default)
        {
            var payload = new
            {
                stock_lots = stockLots,
                nhu_cau = nhuCau,
                tinh_doanh_nghiep = tinhDoanhNghiep,
                dia_chi_doanh_nghiep = diaChiDoanhNghiep,
                enterprise_lat = enterpriseLat,
                enterprise_lng = enterpriseLng
            };

            var results = await PostAsync("enterprise-stock-matching", payload, cancellationToken);
            var suggestions = new List<AIGoiYThuMuaViewModel>();

            foreach (var item in results)
            {
                suggestions.Add(new AIGoiYThuMuaViewModel
                {
                    M_YeuCau = GetString(item, "m_yeucau"),
                    MaSanPham = GetString(item, "ma_san_pham"),
                    MaLoTonKho = GetString(item, "ma_lo_ton_kho") ?? string.Empty,
                    MaKho = GetString(item, "ma_kho") ?? string.Empty,
                    TenKho = GetString(item, "ten_kho"),
                    TenSanPham = GetString(item, "ten_san_pham") ?? "Không rõ",
                    SoLuong = GetDouble(item, "khoi_luong_con_lai"),
                    DonViTinh = GetString(item, "don_vi_tinh"),
                    DiaChiKho = GetString(item, "dia_chi_kho"),
                    DistanceKm = GetNullableDouble(item, "distance_km"),
                    MatchScore = GetDouble(item, "match_score")
                });
            }

            return suggestions;
        }

        private async Task<List<JsonElement>> PostAsync(string endpoint, object payload, CancellationToken cancellationToken)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                using var response = await client.PostAsJsonAsync($"{AiServiceBaseUrl}/{endpoint}", payload, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("AI endpoint {Endpoint} trả về mã lỗi {StatusCode}.", endpoint, response.StatusCode);
                    return new List<JsonElement>();
                }

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (!json.RootElement.TryGetProperty("results", out var resultsNode) || resultsNode.ValueKind != JsonValueKind.Array)
                {
                    return new List<JsonElement>();
                }

                return resultsNode.EnumerateArray().Select(element => element.Clone()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không gọi được AI endpoint {Endpoint}.", endpoint);
                return new List<JsonElement>();
            }
        }

        private static string? GetString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
                ? property.GetString()
                : null;
        }

        private static double GetDouble(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
            {
                return 0;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var value))
            {
                return value;
            }

            return double.TryParse(property.ToString(), out var parsedValue) ? parsedValue : 0;
        }

        private static double? GetNullableDouble(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var value))
            {
                return value;
            }

            return double.TryParse(property.ToString(), out var parsedValue) ? parsedValue : null;
        }
    }
}