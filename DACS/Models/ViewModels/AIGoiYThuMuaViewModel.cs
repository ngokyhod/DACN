using System;
using System.Collections.Generic;

namespace DACS.Models.ViewModels
{
    public class AIGoiYThuMuaViewModel
    {
        public string? M_YeuCau { get; set; }
        public string? MaSanPham { get; set; }
        public string MaLoTonKho { get; set; }
        public string MaKho { get; set; }
        public string? TenKho { get; set; }
        public string TenSanPham { get; set; }
        public double SoLuong { get; set; }
        public double KhoiLuongMuaDeXuat { get; set; }
        public string? DonViTinh { get; set; }
        public string? DiaChiKho { get; set; }
        public long Gia { get; set; }
        public double? DistanceKm { get; set; }
        public string? SuggestedReason { get; set; }
        public string? ProximityLabel { get; set; }
        public double MatchScore { get; set; }
    }
}