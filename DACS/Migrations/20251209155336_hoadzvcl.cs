using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DACS.Migrations
{
    /// <inheritdoc />
    public partial class hoadzvcl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Age = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BlockchainTransaction",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaDonHang = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DiaChiGiaoHang = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Metadata = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TxHash = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockchainTransaction", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CauHoiThuongGap",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Intent = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TrainingSentence = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Response = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CauHoiThuongGap", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DonViTinhs",
                columns: table => new
                {
                    M_DonViTinh = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TenLoaiTinh = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonViTinhs", x => x.M_DonViTinh);
                });

            migrationBuilder.CreateTable(
                name: "GiamGias",
                columns: table => new
                {
                    M_GiamGia = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TenGiamGia = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SoLuong = table.Column<int>(type: "int", nullable: false),
                    Tien = table.Column<long>(type: "bigint", nullable: false),
                    LoaiGiamGia = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MoTa = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NgayBatDau = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NgayKetThuc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    GiaTriDonHangToiThieu = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiamGias", x => x.M_GiamGia);
                });

            migrationBuilder.CreateTable(
                name: "HoanTras",
                columns: table => new
                {
                    M_HoanTra = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    NgayYeuCau = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LyDo = table.Column<string>(type: "nvarchar(MAX)", nullable: false),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HoanTras", x => x.M_HoanTra);
                });

            migrationBuilder.CreateTable(
                name: "KhoHangs",
                columns: table => new
                {
                    MaKho = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TenKho = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DiaChi = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    SucChuaTomTat = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TrangThai = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TenLoaiKho = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Lat = table.Column<double>(type: "float", nullable: false),
                    Lng = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KhoHangs", x => x.MaKho);
                });

            migrationBuilder.CreateTable(
                name: "LoaiSanPhams",
                columns: table => new
                {
                    M_LoaiSP = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TenLoai = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoaiSanPhams", x => x.M_LoaiSP);
                });

            migrationBuilder.CreateTable(
                name: "Owner",
                columns: table => new
                {
                    M_QuanLy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    XacNhan = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Owner", x => x.M_QuanLy);
                });

            migrationBuilder.CreateTable(
                name: "PhuongThucThanhToans",
                columns: table => new
                {
                    M_PhuongThuc = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TenPhuongThuc = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhuongThucThanhToans", x => x.M_PhuongThuc);
                });

            migrationBuilder.CreateTable(
                name: "TinhThanhPhos",
                columns: table => new
                {
                    MaTinh = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TenTinh = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TinhThanhPhos", x => x.MaTinh);
                });

            migrationBuilder.CreateTable(
                name: "VanChuyens",
                columns: table => new
                {
                    M_VanDon = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DonViVanChuyen = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VanChuyens", x => x.M_VanDon);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhieuXuats",
                columns: table => new
                {
                    MaPhieuXuat = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NgayXuat = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LyDoXuat = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MaKho = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NguoiNhan = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhieuXuats", x => x.MaPhieuXuat);
                    table.ForeignKey(
                        name: "FK_PhieuXuats_KhoHangs_MaKho",
                        column: x => x.MaKho,
                        principalTable: "KhoHangs",
                        principalColumn: "MaKho",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoaiSanPhamGiamGias",
                columns: table => new
                {
                    M_LoaiSP = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    M_GiamGia = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoaiSanPhamGiamGias", x => new { x.M_LoaiSP, x.M_GiamGia });
                    table.ForeignKey(
                        name: "FK_LoaiSanPhamGiamGias_GiamGias_M_GiamGia",
                        column: x => x.M_GiamGia,
                        principalTable: "GiamGias",
                        principalColumn: "M_GiamGia",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LoaiSanPhamGiamGias_LoaiSanPhams_M_LoaiSP",
                        column: x => x.M_LoaiSP,
                        principalTable: "LoaiSanPhams",
                        principalColumn: "M_LoaiSP",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SanPhams",
                columns: table => new
                {
                    M_SanPham = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    M_LoaiSP = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    M_DonViTinh = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TenSanPham = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Gia = table.Column<long>(type: "bigint", nullable: false),
                    MoTa = table.Column<string>(type: "nvarchar(MAX)", nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AnhSanPham = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HanSuDung = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ThangBatDauVu = table.Column<int>(type: "int", nullable: false),
                    ThangKetThucVu = table.Column<int>(type: "int", nullable: false),
                    HeSoGiaTrongMua = table.Column<double>(type: "float", nullable: false),
                    HeSoGiaTraiMua = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SanPhams", x => x.M_SanPham);
                    table.ForeignKey(
                        name: "FK_SanPhams_DonViTinhs_M_DonViTinh",
                        column: x => x.M_DonViTinh,
                        principalTable: "DonViTinhs",
                        principalColumn: "M_DonViTinh",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SanPhams_LoaiSanPhams_M_LoaiSP",
                        column: x => x.M_LoaiSP,
                        principalTable: "LoaiSanPhams",
                        principalColumn: "M_LoaiSP",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuanHuyens",
                columns: table => new
                {
                    MaQuan = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TenQuan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MaTinh = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuanHuyens", x => x.MaQuan);
                    table.ForeignKey(
                        name: "FK_QuanHuyens_TinhThanhPhos_MaTinh",
                        column: x => x.MaTinh,
                        principalTable: "TinhThanhPhos",
                        principalColumn: "MaTinh",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChiTietPhieuXuats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaPhieuXuat = table.Column<int>(type: "int", nullable: false),
                    M_SanPham = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    M_DonViTinh = table.Column<string>(type: "nvarchar(10)", nullable: false),
                    SoLuong = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiTietPhieuXuats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChiTietPhieuXuats_DonViTinhs_M_DonViTinh",
                        column: x => x.M_DonViTinh,
                        principalTable: "DonViTinhs",
                        principalColumn: "M_DonViTinh",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChiTietPhieuXuats_PhieuXuats_MaPhieuXuat",
                        column: x => x.MaPhieuXuat,
                        principalTable: "PhieuXuats",
                        principalColumn: "MaPhieuXuat",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChiTietPhieuXuats_SanPhams_M_SanPham",
                        column: x => x.M_SanPham,
                        principalTable: "SanPhams",
                        principalColumn: "M_SanPham",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoTonKhos",
                columns: table => new
                {
                    MaLoTonKho = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    M_SanPham = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MaKho = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    NgayNhapKho = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HanSuDung = table.Column<DateTime>(type: "datetime2", nullable: true),
                    KhoiLuongBanDau = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    KhoiLuongConLai = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    M_DonViTinh = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoTonKhos", x => x.MaLoTonKho);
                    table.ForeignKey(
                        name: "FK_LoTonKhos_DonViTinhs_M_DonViTinh",
                        column: x => x.M_DonViTinh,
                        principalTable: "DonViTinhs",
                        principalColumn: "M_DonViTinh",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoTonKhos_KhoHangs_MaKho",
                        column: x => x.MaKho,
                        principalTable: "KhoHangs",
                        principalColumn: "MaKho",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoTonKhos_SanPhams_M_SanPham",
                        column: x => x.M_SanPham,
                        principalTable: "SanPhams",
                        principalColumn: "M_SanPham",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "SanPhamGiamGias",
                columns: table => new
                {
                    M_SanPham = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    M_GiamGia = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SanPhamGiamGias", x => new { x.M_SanPham, x.M_GiamGia });
                    table.ForeignKey(
                        name: "FK_SanPhamGiamGias_GiamGias_M_GiamGia",
                        column: x => x.M_GiamGia,
                        principalTable: "GiamGias",
                        principalColumn: "M_GiamGia",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SanPhamGiamGias_SanPhams_M_SanPham",
                        column: x => x.M_SanPham,
                        principalTable: "SanPhams",
                        principalColumn: "M_SanPham",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SanPhamYeuThichs",
                columns: table => new
                {
                    M_YeuThich = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    M_SanPham = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    NgayThem = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SanPhamYeuThichs", x => x.M_YeuThich);
                    table.ForeignKey(
                        name: "FK_SanPhamYeuThichs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SanPhamYeuThichs_SanPhams_M_SanPham",
                        column: x => x.M_SanPham,
                        principalTable: "SanPhams",
                        principalColumn: "M_SanPham",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "XaPhuongs",
                columns: table => new
                {
                    MaXa = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TenXa = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MaQuan = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XaPhuongs", x => x.MaXa);
                    table.ForeignKey(
                        name: "FK_XaPhuongs_QuanHuyens_MaQuan",
                        column: x => x.MaQuan,
                        principalTable: "QuanHuyens",
                        principalColumn: "MaQuan",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChatHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CauHoi = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CauTraLoi = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    M_KhachHang = table.Column<string>(type: "nvarchar(10)", nullable: true),
                    NgayChat = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SenderId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceiverId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SenderName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SentTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    M_KhachHang = table.Column<string>(type: "nvarchar(10)", nullable: true),
                    IsFromAdmin = table.Column<bool>(type: "bit", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChiTietDanhGias",
                columns: table => new
                {
                    M_KhachHang = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    M_SanPham = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MucDoHaiLong = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    MoTa_DanhGia = table.Column<string>(type: "nvarchar(MAX)", nullable: true),
                    NgayDanhGia = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiTietDanhGias", x => new { x.M_KhachHang, x.M_SanPham });
                    table.ForeignKey(
                        name: "FK_ChiTietDanhGias_SanPhams_M_SanPham",
                        column: x => x.M_SanPham,
                        principalTable: "SanPhams",
                        principalColumn: "M_SanPham",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChiTietDatHangs",
                columns: table => new
                {
                    M_SanPham = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    M_DonHang = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    M_CTDatHang = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GiaDatHang = table.Column<long>(type: "bigint", nullable: false),
                    TongTien = table.Column<long>(type: "bigint", nullable: false),
                    Khoiluong = table.Column<float>(type: "real", nullable: false),
                    M_KhachHang = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TrangThaiDonHang = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiTietDatHangs", x => new { x.M_SanPham, x.M_DonHang });
                    table.ForeignKey(
                        name: "FK_ChiTietDatHangs_SanPhams_M_SanPham",
                        column: x => x.M_SanPham,
                        principalTable: "SanPhams",
                        principalColumn: "M_SanPham",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChiTietHoanTras",
                columns: table => new
                {
                    M_HoanTra = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    M_DonHang = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SoLuong = table.Column<int>(type: "int", nullable: false),
                    TrangThaiSanPham = table.Column<string>(type: "nvarchar(MAX)", nullable: false),
                    SoTienHoan = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiTietHoanTras", x => new { x.M_HoanTra, x.M_DonHang });
                    table.ForeignKey(
                        name: "FK_ChiTietHoanTras_HoanTras_M_HoanTra",
                        column: x => x.M_HoanTra,
                        principalTable: "HoanTras",
                        principalColumn: "M_HoanTra",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChiTietLienHe",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    HoTen = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NoiDung = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NgayGui = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    M_KhachHang = table.Column<string>(type: "nvarchar(10)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiTietLienHe", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KhachHangs",
                columns: table => new
                {
                    M_KhachHang = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Ten_KhachHang = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email_KhachHang = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SDT_KhachHang = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MaTinh = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MaQuan = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MaXa = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DiaChi_DuongApThon = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AvatarUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KhachHangs", x => x.M_KhachHang);
                    table.ForeignKey(
                        name: "FK_KhachHangs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KhachHangs_ChiTietLienHe_Id",
                        column: x => x.Id,
                        principalTable: "ChiTietLienHe",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KhachHangs_QuanHuyens_MaQuan",
                        column: x => x.MaQuan,
                        principalTable: "QuanHuyens",
                        principalColumn: "MaQuan",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KhachHangs_TinhThanhPhos_MaTinh",
                        column: x => x.MaTinh,
                        principalTable: "TinhThanhPhos",
                        principalColumn: "MaTinh",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KhachHangs_XaPhuongs_MaXa",
                        column: x => x.MaXa,
                        principalTable: "XaPhuongs",
                        principalColumn: "MaXa",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DonHangs",
                columns: table => new
                {
                    M_DonHang = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    NgayDat = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NgayGiao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TrangThai = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TrangThaiThanhToan = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LyDoHoanTra = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NgayHoanTra = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TrangThaiHoanTra = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DaTruTonKho = table.Column<bool>(type: "bit", nullable: false),
                    M_VanDon = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    M_PhuongThuc = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ShippingAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    M_KhachHang = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    TotalPrice = table.Column<float>(type: "real", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tendathang = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SoDienThoaidathang = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonHangs", x => x.M_DonHang);
                    table.ForeignKey(
                        name: "FK_DonHangs_KhachHangs_M_KhachHang",
                        column: x => x.M_KhachHang,
                        principalTable: "KhachHangs",
                        principalColumn: "M_KhachHang",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DonHangs_PhuongThucThanhToans_M_PhuongThuc",
                        column: x => x.M_PhuongThuc,
                        principalTable: "PhuongThucThanhToans",
                        principalColumn: "M_PhuongThuc",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DonHangs_VanChuyens_M_VanDon",
                        column: x => x.M_VanDon,
                        principalTable: "VanChuyens",
                        principalColumn: "M_VanDon",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuanLys",
                columns: table => new
                {
                    M_QuanLy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    YeuCau = table.Column<string>(type: "nvarchar(MAX)", nullable: true),
                    M_HoanTra = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    M_DonHang = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuanLys", x => x.M_QuanLy);
                    table.ForeignKey(
                        name: "FK_QuanLys_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuanLys_DonHangs_M_DonHang",
                        column: x => x.M_DonHang,
                        principalTable: "DonHangs",
                        principalColumn: "M_DonHang",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuanLys_HoanTras_M_HoanTra",
                        column: x => x.M_HoanTra,
                        principalTable: "HoanTras",
                        principalColumn: "M_HoanTra",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuanLys_Owner_M_QuanLy",
                        column: x => x.M_QuanLy,
                        principalTable: "Owner",
                        principalColumn: "M_QuanLy",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YeuCauThuGoms",
                columns: table => new
                {
                    M_YeuCau = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    NgayYeuCau = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    M_KhachHang = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MaTinh = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MaQuan = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MaXa = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DiaChi_DuongApThon = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ThoiGianSanSang = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Thoi_Gian_HT = table.Column<DateTime>(type: "datetime2", nullable: false),
                    M_QuanLy = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    BlockchainTransactionHash = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DonHangM_DonHang = table.Column<string>(type: "nvarchar(10)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YeuCauThuGoms", x => x.M_YeuCau);
                    table.ForeignKey(
                        name: "FK_YeuCauThuGoms_AspNetUsers_M_QuanLy",
                        column: x => x.M_QuanLy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_YeuCauThuGoms_DonHangs_DonHangM_DonHang",
                        column: x => x.DonHangM_DonHang,
                        principalTable: "DonHangs",
                        principalColumn: "M_DonHang");
                    table.ForeignKey(
                        name: "FK_YeuCauThuGoms_KhachHangs_M_KhachHang",
                        column: x => x.M_KhachHang,
                        principalTable: "KhachHangs",
                        principalColumn: "M_KhachHang",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_YeuCauThuGoms_QuanHuyens_MaQuan",
                        column: x => x.MaQuan,
                        principalTable: "QuanHuyens",
                        principalColumn: "MaQuan",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_YeuCauThuGoms_TinhThanhPhos_MaTinh",
                        column: x => x.MaTinh,
                        principalTable: "TinhThanhPhos",
                        principalColumn: "MaTinh",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_YeuCauThuGoms_XaPhuongs_MaXa",
                        column: x => x.MaXa,
                        principalTable: "XaPhuongs",
                        principalColumn: "MaXa",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChiTietThuGoms",
                columns: table => new
                {
                    M_ChiTiet = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    M_YeuCau = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    M_SanPham = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    M_LoaiSP = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SoLuong = table.Column<int>(type: "int", nullable: false),
                    M_DonViTinh = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    GiaTriMongMuon = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MoTa = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DacTinh_CongKenh = table.Column<bool>(type: "bit", nullable: false),
                    DacTinh_DoAmCao = table.Column<bool>(type: "bit", nullable: false),
                    DacTinh_AmUot = table.Column<bool>(type: "bit", nullable: false),
                    DacTinh_Kho = table.Column<bool>(type: "bit", nullable: false),
                    DacTinh_TapChat = table.Column<bool>(type: "bit", nullable: false),
                    DacTinh_DaXuLy = table.Column<bool>(type: "bit", nullable: false),
                    DanhSachHinhAnh = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaLoTonKho = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TrangThaiXuLy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DoAmThucTe = table.Column<double>(type: "float", nullable: false),
                    HeSoDoAm = table.Column<double>(type: "float", nullable: false),
                    HeSoMuaVu = table.Column<double>(type: "float", nullable: false),
                    PhiVanChuyen = table.Column<double>(type: "float", nullable: false),
                    DonGiaThuMua = table.Column<double>(type: "float", nullable: false),
                    DonHangM_DonHang = table.Column<string>(type: "nvarchar(10)", nullable: true),
                    QuanLyM_QuanLy = table.Column<string>(type: "nvarchar(10)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiTietThuGoms", x => x.M_ChiTiet);
                    table.ForeignKey(
                        name: "FK_ChiTietThuGoms_DonHangs_DonHangM_DonHang",
                        column: x => x.DonHangM_DonHang,
                        principalTable: "DonHangs",
                        principalColumn: "M_DonHang");
                    table.ForeignKey(
                        name: "FK_ChiTietThuGoms_DonViTinhs_M_DonViTinh",
                        column: x => x.M_DonViTinh,
                        principalTable: "DonViTinhs",
                        principalColumn: "M_DonViTinh",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChiTietThuGoms_LoTonKhos_MaLoTonKho",
                        column: x => x.MaLoTonKho,
                        principalTable: "LoTonKhos",
                        principalColumn: "MaLoTonKho");
                    table.ForeignKey(
                        name: "FK_ChiTietThuGoms_LoaiSanPhams_M_LoaiSP",
                        column: x => x.M_LoaiSP,
                        principalTable: "LoaiSanPhams",
                        principalColumn: "M_LoaiSP",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChiTietThuGoms_QuanLys_QuanLyM_QuanLy",
                        column: x => x.QuanLyM_QuanLy,
                        principalTable: "QuanLys",
                        principalColumn: "M_QuanLy");
                    table.ForeignKey(
                        name: "FK_ChiTietThuGoms_SanPhams_M_SanPham",
                        column: x => x.M_SanPham,
                        principalTable: "SanPhams",
                        principalColumn: "M_SanPham",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChiTietThuGoms_YeuCauThuGoms_M_YeuCau",
                        column: x => x.M_YeuCau,
                        principalTable: "YeuCauThuGoms",
                        principalColumn: "M_YeuCau",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChatHistory_M_KhachHang",
                table: "ChatHistory",
                column: "M_KhachHang");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_M_KhachHang",
                table: "ChatMessages",
                column: "M_KhachHang");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietDanhGias_M_SanPham",
                table: "ChiTietDanhGias",
                column: "M_SanPham");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietDatHangs_M_DonHang",
                table: "ChiTietDatHangs",
                column: "M_DonHang");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietDatHangs_M_KhachHang",
                table: "ChiTietDatHangs",
                column: "M_KhachHang");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietHoanTras_M_DonHang",
                table: "ChiTietHoanTras",
                column: "M_DonHang");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietLienHe_M_KhachHang",
                table: "ChiTietLienHe",
                column: "M_KhachHang");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietPhieuXuats_M_DonViTinh",
                table: "ChiTietPhieuXuats",
                column: "M_DonViTinh");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietPhieuXuats_M_SanPham",
                table: "ChiTietPhieuXuats",
                column: "M_SanPham");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietPhieuXuats_MaPhieuXuat",
                table: "ChiTietPhieuXuats",
                column: "MaPhieuXuat");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietThuGoms_DonHangM_DonHang",
                table: "ChiTietThuGoms",
                column: "DonHangM_DonHang");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietThuGoms_M_DonViTinh",
                table: "ChiTietThuGoms",
                column: "M_DonViTinh");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietThuGoms_M_LoaiSP",
                table: "ChiTietThuGoms",
                column: "M_LoaiSP");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietThuGoms_M_SanPham",
                table: "ChiTietThuGoms",
                column: "M_SanPham");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietThuGoms_M_YeuCau",
                table: "ChiTietThuGoms",
                column: "M_YeuCau");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietThuGoms_MaLoTonKho",
                table: "ChiTietThuGoms",
                column: "MaLoTonKho");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietThuGoms_QuanLyM_QuanLy",
                table: "ChiTietThuGoms",
                column: "QuanLyM_QuanLy");

            migrationBuilder.CreateIndex(
                name: "IX_DonHangs_M_KhachHang",
                table: "DonHangs",
                column: "M_KhachHang");

            migrationBuilder.CreateIndex(
                name: "IX_DonHangs_M_PhuongThuc",
                table: "DonHangs",
                column: "M_PhuongThuc");

            migrationBuilder.CreateIndex(
                name: "IX_DonHangs_M_VanDon",
                table: "DonHangs",
                column: "M_VanDon");

            migrationBuilder.CreateIndex(
                name: "IX_KhachHangs_Id",
                table: "KhachHangs",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_KhachHangs_MaQuan",
                table: "KhachHangs",
                column: "MaQuan");

            migrationBuilder.CreateIndex(
                name: "IX_KhachHangs_MaTinh",
                table: "KhachHangs",
                column: "MaTinh");

            migrationBuilder.CreateIndex(
                name: "IX_KhachHangs_MaXa",
                table: "KhachHangs",
                column: "MaXa");

            migrationBuilder.CreateIndex(
                name: "IX_KhachHangs_UserId",
                table: "KhachHangs",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoaiSanPhamGiamGias_M_GiamGia",
                table: "LoaiSanPhamGiamGias",
                column: "M_GiamGia");

            migrationBuilder.CreateIndex(
                name: "IX_LoTonKhos_M_DonViTinh",
                table: "LoTonKhos",
                column: "M_DonViTinh");

            migrationBuilder.CreateIndex(
                name: "IX_LoTonKhos_M_SanPham",
                table: "LoTonKhos",
                column: "M_SanPham");

            migrationBuilder.CreateIndex(
                name: "IX_LoTonKhos_MaKho",
                table: "LoTonKhos",
                column: "MaKho");

            migrationBuilder.CreateIndex(
                name: "IX_PhieuXuats_MaKho",
                table: "PhieuXuats",
                column: "MaKho");

            migrationBuilder.CreateIndex(
                name: "IX_QuanHuyens_MaTinh",
                table: "QuanHuyens",
                column: "MaTinh");

            migrationBuilder.CreateIndex(
                name: "IX_QuanLys_M_DonHang",
                table: "QuanLys",
                column: "M_DonHang");

            migrationBuilder.CreateIndex(
                name: "IX_QuanLys_M_HoanTra",
                table: "QuanLys",
                column: "M_HoanTra",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuanLys_UserId",
                table: "QuanLys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SanPhamGiamGias_M_GiamGia",
                table: "SanPhamGiamGias",
                column: "M_GiamGia");

            migrationBuilder.CreateIndex(
                name: "IX_SanPhams_M_DonViTinh",
                table: "SanPhams",
                column: "M_DonViTinh");

            migrationBuilder.CreateIndex(
                name: "IX_SanPhams_M_LoaiSP",
                table: "SanPhams",
                column: "M_LoaiSP");

            migrationBuilder.CreateIndex(
                name: "IX_SanPhamYeuThichs_M_SanPham",
                table: "SanPhamYeuThichs",
                column: "M_SanPham");

            migrationBuilder.CreateIndex(
                name: "IX_SanPhamYeuThichs_UserId",
                table: "SanPhamYeuThichs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_XaPhuongs_MaQuan",
                table: "XaPhuongs",
                column: "MaQuan");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauThuGoms_DonHangM_DonHang",
                table: "YeuCauThuGoms",
                column: "DonHangM_DonHang");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauThuGoms_M_KhachHang",
                table: "YeuCauThuGoms",
                column: "M_KhachHang");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauThuGoms_M_QuanLy",
                table: "YeuCauThuGoms",
                column: "M_QuanLy");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauThuGoms_MaQuan",
                table: "YeuCauThuGoms",
                column: "MaQuan");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauThuGoms_MaTinh",
                table: "YeuCauThuGoms",
                column: "MaTinh");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauThuGoms_MaXa",
                table: "YeuCauThuGoms",
                column: "MaXa");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatHistory_KhachHangs_M_KhachHang",
                table: "ChatHistory",
                column: "M_KhachHang",
                principalTable: "KhachHangs",
                principalColumn: "M_KhachHang");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_KhachHangs_M_KhachHang",
                table: "ChatMessages",
                column: "M_KhachHang",
                principalTable: "KhachHangs",
                principalColumn: "M_KhachHang");

            migrationBuilder.AddForeignKey(
                name: "FK_ChiTietDanhGias_KhachHangs_M_KhachHang",
                table: "ChiTietDanhGias",
                column: "M_KhachHang",
                principalTable: "KhachHangs",
                principalColumn: "M_KhachHang",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChiTietDatHangs_DonHangs_M_DonHang",
                table: "ChiTietDatHangs",
                column: "M_DonHang",
                principalTable: "DonHangs",
                principalColumn: "M_DonHang",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChiTietDatHangs_KhachHangs_M_KhachHang",
                table: "ChiTietDatHangs",
                column: "M_KhachHang",
                principalTable: "KhachHangs",
                principalColumn: "M_KhachHang");

            migrationBuilder.AddForeignKey(
                name: "FK_ChiTietHoanTras_DonHangs_M_DonHang",
                table: "ChiTietHoanTras",
                column: "M_DonHang",
                principalTable: "DonHangs",
                principalColumn: "M_DonHang",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChiTietLienHe_KhachHangs_M_KhachHang",
                table: "ChiTietLienHe",
                column: "M_KhachHang",
                principalTable: "KhachHangs",
                principalColumn: "M_KhachHang");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KhachHangs_AspNetUsers_UserId",
                table: "KhachHangs");

            migrationBuilder.DropForeignKey(
                name: "FK_ChiTietLienHe_KhachHangs_M_KhachHang",
                table: "ChiTietLienHe");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "BlockchainTransaction");

            migrationBuilder.DropTable(
                name: "CauHoiThuongGap");

            migrationBuilder.DropTable(
                name: "ChatHistory");

            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "ChiTietDanhGias");

            migrationBuilder.DropTable(
                name: "ChiTietDatHangs");

            migrationBuilder.DropTable(
                name: "ChiTietHoanTras");

            migrationBuilder.DropTable(
                name: "ChiTietPhieuXuats");

            migrationBuilder.DropTable(
                name: "ChiTietThuGoms");

            migrationBuilder.DropTable(
                name: "LoaiSanPhamGiamGias");

            migrationBuilder.DropTable(
                name: "SanPhamGiamGias");

            migrationBuilder.DropTable(
                name: "SanPhamYeuThichs");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "PhieuXuats");

            migrationBuilder.DropTable(
                name: "LoTonKhos");

            migrationBuilder.DropTable(
                name: "QuanLys");

            migrationBuilder.DropTable(
                name: "YeuCauThuGoms");

            migrationBuilder.DropTable(
                name: "GiamGias");

            migrationBuilder.DropTable(
                name: "KhoHangs");

            migrationBuilder.DropTable(
                name: "SanPhams");

            migrationBuilder.DropTable(
                name: "HoanTras");

            migrationBuilder.DropTable(
                name: "Owner");

            migrationBuilder.DropTable(
                name: "DonHangs");

            migrationBuilder.DropTable(
                name: "DonViTinhs");

            migrationBuilder.DropTable(
                name: "LoaiSanPhams");

            migrationBuilder.DropTable(
                name: "PhuongThucThanhToans");

            migrationBuilder.DropTable(
                name: "VanChuyens");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "KhachHangs");

            migrationBuilder.DropTable(
                name: "ChiTietLienHe");

            migrationBuilder.DropTable(
                name: "XaPhuongs");

            migrationBuilder.DropTable(
                name: "QuanHuyens");

            migrationBuilder.DropTable(
                name: "TinhThanhPhos");
        }
    }
}
