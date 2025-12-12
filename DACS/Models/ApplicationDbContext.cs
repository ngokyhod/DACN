using DACS.Extention;
using DACS.Models;
using DACS.Models.AI;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;





public class ApplicationDbContext : IdentityDbContext<ApplicationUser>

{

    // Constructor required for dependency injection

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)

    : base(options)

    {

    }

    public DbSet<KhachHang> KhachHangs { get; set; }

    public DbSet<LoaiSanPham> LoaiSanPhams { get; set; }

    public DbSet<DonViTinh> DonViTinhs { get; set; }

    public DbSet<KhoHang> KhoHangs { get; set; }

    public DbSet<LoTonKho> LoTonKhos { get; set; } 

    public DbSet<SanPham> SanPhams { get; set; }

    public DbSet<DonHang> DonHangs { get; set; }

    public DbSet<ChiTietDatHang> ChiTietDatHangs { get; set; }

    public DbSet<PhuongThucThanhToan> PhuongThucThanhToans { get; set; }

    public DbSet<VanChuyen> VanChuyens { get; set; }

    public DbSet<ChiTietDanhGia> ChiTietDanhGias { get; set; }

    public DbSet<GiamGia> GiamGias { get; set; }

    public DbSet<SanPhamGiamGia> SanPhamGiamGias { get; set; } // Bảng nối Sản phẩm - Giảm giá

    public DbSet<LoaiSanPhamGiamGia> LoaiSanPhamGiamGias { get; set; } // Bảng nối Loại SP - Giảm giá

    public DbSet<YeuCauThuGom> YeuCauThuGoms { get; set; }

    public DbSet<ChiTietThuGom> ChiTietThuGoms { get; set; }

    public DbSet<HoanTra> HoanTras { get; set; }

    public DbSet<ChiTietHoanTra> ChiTietHoanTras { get; set; }

    public DbSet<Owner> Owner { get; set; }

    public DbSet<QuanLy> QuanLys { get; set; }

    public DbSet<TinhThanhPho> TinhThanhPhos { get; set; }

    public DbSet<QuanHuyen> QuanHuyens { get; set; }

    public DbSet<XaPhuong> XaPhuongs { get; set; }
    public DbSet<ChiTietPhieuXuat> ChiTietPhieuXuats { get; set; }
    public DbSet<PhieuXuat> PhieuXuats { get; set; }

    public DbSet<ChiTietLienHe> ChiTietLienHe { get; set; }
    public DbSet<CauHoiThuongGap> CauHoiThuongGap { get; set; }
    public DbSet<ChatHistory> ChatHistory { get; set; }
    public DbSet<SanPhamYeuThich> SanPhamYeuThichs { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<BlockchainTransaction> BlockchainTransaction { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)

    {

        base.OnModelCreating(builder); // Rất quan trọng! Cấu hình các bảng Identity trước.



        builder.Entity<QuanHuyen>()

   .HasOne(q => q.TinhThanhPho)

   .WithMany(t => t.QuanHuyens)

   .HasForeignKey(q => q.MaTinh)

   .OnDelete(DeleteBehavior.Restrict); // Hoặc Cascade nếu muốn



        // Cấu hình XaPhuong

        builder.Entity<XaPhuong>()

      .HasOne(x => x.QuanHuyen)

      .WithMany(q => q.XaPhuongs)

      .HasForeignKey(x => x.MaQuan)

      .OnDelete(DeleteBehavior.Restrict); // Hoặc Cascade



        // --- Cấu hình Khóa chính phức hợp ---

        builder.Entity<ChiTietDatHang>()

      .HasKey(cdh => new { cdh.M_SanPham, cdh.M_DonHang }); // Composite key



        builder.Entity<ChiTietDanhGia>()

      .HasKey(cdg => new { cdg.M_KhachHang, cdg.M_SanPham }); // Composite key for ReviewDetail // <<< CONFIGURATION IS PRESENT



        builder.Entity<ChiTietHoanTra>()

      .HasKey(ctht => new { ctht.M_HoanTra, ctht.M_DonHang }); // Composite key (Lưu ý: M_DonHang có thể không đủ chi tiết)





        builder.Entity<SanPhamGiamGia>()

      .HasKey(spg => new { spg.M_SanPham, spg.M_GiamGia }); // Composite key



        builder.Entity<LoaiSanPhamGiamGia>()

      .HasKey(lsg => new { lsg.M_LoaiSP, lsg.M_GiamGia }); // Composite key



        builder.Entity<GiamGia>() // Configuration for GiamGia entity

           .Property(gg => gg.GiaTriDonHangToiThieu).HasColumnType("decimal(18, 2)");

        // MISSING CONFIGURATION FOR GiaTriDonHangToiThieu HERE <<<<<<



        builder.Entity<ChiTietHoanTra>()

     .Property(ctht => ctht.SoTienHoan)

     .HasColumnType("decimal(18, 2)");


        builder.Entity<LoTonKho>(entity =>
        {
            // 1. Sửa lỗi cảnh báo (warning) về decimal
            // Chỉ định rõ ràng kiểu dữ liệu SQL, ví dụ: decimal(18, 2)
            entity.Property(lo => lo.KhoiLuongBanDau).HasColumnType("decimal(18, 2)");
            entity.Property(lo => lo.KhoiLuongConLai).HasColumnType("decimal(18, 2)");

            // 2. Sửa lỗi ERROR (Multiple Cascade Paths)
            // Báo cho EF Core KHÔNG được phép xóa cascade

            // Khóa ngoại từ LoTonKho -> SanPham
            entity.HasOne(lo => lo.SanPham)
                  .WithMany() // Giả sử SanPham không có ICollection<LoTonKho>
                  .HasForeignKey(lo => lo.M_SanPham)
                  .OnDelete(DeleteBehavior.Restrict); // <-- Quan trọng

            // Khóa ngoại từ LoTonKho -> KhoHang
            entity.HasOne(lo => lo.KhoHang)
                  .WithMany() // Giả sử KhoHang không có ICollection<LoTonKho>
                  .HasForeignKey(lo => lo.MaKho)
                  .OnDelete(DeleteBehavior.Restrict); // <-- Quan trọng

            // Khóa ngoại từ LoTonKho -> DonViTinh
            entity.HasOne(lo => lo.DonViTinh)
                  .WithMany() // Giả sử DonViTinh không có ICollection<LoTonKho>
                  .HasForeignKey(lo => lo.M_DonViTinh)
                  .OnDelete(DeleteBehavior.Restrict); // <-- Quan trọng
        });
        builder.Entity<ChiTietPhieuXuat>(entity =>
        {
            // 1. Sửa lỗi chính (SanPham):
            // Quan hệ ChiTietPhieuXuat (Nhiều) -> SanPham (Một)
            entity.HasOne(ctpx => ctpx.SanPham)
                  .WithMany() // Giả sử SanPham không có ICollection<ChiTietPhieuXuat>
                  .HasForeignKey(ctpx => ctpx.M_SanPham)
                  .OnDelete(DeleteBehavior.Restrict); // <-- THE FIX: Cấm xóa SanPham nếu còn ChiTietPhieuXuat

            // 2. Cài đặt cho các FK khác (để an toàn):
            // Quan hệ ChiTietPhieuXuat (Nhiều) -> DonViTinh (Một)
            entity.HasOne(ctpx => ctpx.DonViTinh)
                  .WithMany() // Giả sử DonViTinh không có ICollection
                  .HasForeignKey(ctpx => ctpx.M_DonViTinh)
                  .OnDelete(DeleteBehavior.Restrict); // <-- THE FIX: Cấm xóa DonViTinh nếu còn ChiTietPhieuXuat

            // 3. Quan hệ với PhieuXuat (Cha-Con)
            // (Khóa ngoại này nên để Cascade, là mặc định, nên không cần ghi)
            // entity.HasOne(ctpx => ctpx.PhieuXuat)
            //       .WithMany(px => px.ChiTietPhieuXuats) // Giả sử PhieuXuat có ICollection
            //       .HasForeignKey(ctpx => ctpx.MaPhieuXuat)
            //       .OnDelete(DeleteBehavior.Cascade); 
        });
        builder.Entity<ChiTietThuGom>(entity =>

        {


            // 1. Define the NEW Primary Key (e.g., M_ChiTiet)

            entity.HasKey(ctg => ctg.M_ChiTiet); // <<< CORRECT! Defines M_ChiTiet as PK.

            entity.HasOne(ctg => ctg.YeuCauThuGom)
         .WithMany(yc => yc.ChiTietThuGoms)
         .HasForeignKey(ctg => ctg.M_YeuCau)
         .IsRequired() // Consistent with [Required] in model
         .OnDelete(DeleteBehavior.Cascade); // <<< EXPLICITLY SET TO CASCADE

            // 3. Configure Relationship with LoaiSanPham (using M_LoaiSP FK) - THÊM MỚI
            entity.HasOne(ctg => ctg.LoaiSanPham) // ĐÚNG: Dùng navigation property LoaiSanPham
                  .WithMany()
                  .HasForeignKey(ctg => ctg.M_LoaiSP) // Khóa ngoại vẫn là M_LoaiSP
                  .IsRequired()
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ctg => ctg.YeuCauThuGom)
         .WithMany(yc => yc.ChiTietThuGoms)
         .HasForeignKey(ctg => ctg.M_YeuCau)
         .IsRequired()
         // .OnDelete(DeleteBehavior.Cascade); // <<< OLD/CURRENT
         .OnDelete(DeleteBehavior.Restrict);  // <<< NEW: Change to Restrict


            // 4. Configure Relationship with DonViTinh (using M_DonViTinh FK)

            entity.HasOne(ctg => ctg.DonViTinh)

         .WithMany(dvt => dvt.ChiTietThuGoms) // Assumes DonViTinh has collection

                  .HasForeignKey(ctg => ctg.M_DonViTinh)

         .IsRequired() // OK

                  .OnDelete(DeleteBehavior.Restrict); // OK

            // Configure other properties if needed (e.g., decimal precision for GiaTriMongMuon)

            entity.Property(ctg => ctg.GiaTriMongMuon).HasColumnType("decimal(18, 2)"); // OK


            // Cấu hình User <-> KhachHang

            builder.Entity<KhachHang>()

        .HasOne(kh => kh.User)

        .WithOne() // Hoặc .WithOne(u => u.KhachHangProfile) nếu bạn thêm nav prop vào ApplicationUser

                .HasForeignKey<KhachHang>(kh => kh.UserId)

        .IsRequired();



            // Cấu hình KhachHang -> YeuCauThuGom (One-to-Many)

            // Giả sử YeuCauThuGom có khóa ngoại là M_KhachHang và navigation property là KhachHang

            builder.Entity<KhachHang>()

         .HasMany(kh => kh.YeuCauThuGoms)

         .WithOne(yctg => yctg.KhachHang) // Tên navigation property trong YeuCauThuGom

                   .HasForeignKey(yctg => yctg.M_KhachHang); // Tên khóa ngoại trong YeuCauThuGom



            // Cấu hình KhachHang -> ChiTietDatHang (One-to-Many)

            builder.Entity<KhachHang>()

         .HasMany(kh => kh.ChiTietDatHangs)

         .WithOne(ctdh => ctdh.KhachHang) // Tên navigation property trong ChiTietDatHang

                  .HasForeignKey(ctdh => ctdh.M_KhachHang); // Tên khóa ngoại trong ChiTietDatHang





            builder.Entity<KhachHang>(entity =>

            {

                entity.HasOne(kh => kh.TinhThanhPho)

                   .WithMany() // Assuming TinhThanhPho doesn't need collection of KhachHang

                              .HasForeignKey(kh => kh.MaTinh)

                   .IsRequired()

                   .OnDelete(DeleteBehavior.Restrict);



                entity.HasOne(kh => kh.QuanHuyen)

                   .WithMany() // Assuming QuanHuyen doesn't need collection of KhachHang

                              .HasForeignKey(kh => kh.MaQuan)

                   .IsRequired()

                   .OnDelete(DeleteBehavior.Restrict);



                entity.HasOne(kh => kh.XaPhuong)

                   .WithMany() // Assuming XaPhuong doesn't need collection of KhachHang

                              .HasForeignKey(kh => kh.MaXa)

                   .IsRequired()

                   .OnDelete(DeleteBehavior.Restrict);


                builder.Entity<DonHang>() // Chọn thực thể DonHang để cấu hình

                        .HasOne(dh => dh.KhachHang) // Chỉ định mối quan hệ một (một KhachHang...)

                        .WithMany() // (...có nhiều DonHangs) - Bỏ trống nếu không cần navigation ngược lại trong KhachHang

                        // .WithMany(kh => kh.DonHangs) // <<< Dùng dòng này nếu bạn thêm ICollection<DonHang> vào KhachHang

                        .HasForeignKey(dh => dh.M_KhachHang) // Chỉ định khóa ngoại là M_KhachHang

                        .IsRequired(false) // <<< Quan trọng: Đặt là false nếu M_KhachHang cho phép NULL (string?)

                        // .IsRequired(true) // <<< Quan trọng: Đặt là true nếu M_KhachHang KHÔNG cho phép NULL (string)

                        .OnDelete(DeleteBehavior.Restrict); // <<< QUAN TRỌNG: Ngăn chặn xóa xếp tầng (tương đương NO ACTION)





            });

        });

    }

}