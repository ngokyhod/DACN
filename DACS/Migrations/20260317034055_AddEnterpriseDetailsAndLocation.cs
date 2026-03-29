using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DACS.Migrations
{
    /// <inheritdoc />
    public partial class AddEnterpriseDetailsAndLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AI_PriorityScore",
                table: "YeuCauThuGoms");

            migrationBuilder.DropColumn(
                name: "AI_SuggestedAction",
                table: "YeuCauThuGoms");

            migrationBuilder.DropColumn(
                name: "ThoiGianTon_ToiDa",
                table: "YeuCauThuGoms");

            migrationBuilder.AddColumn<string>(
                name: "DiaChiDoanhNghiep",
                table: "KhachHangs",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GiayPhepKinhDoanhUrl",
                table: "KhachHangs",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TinhThanhDoanhNghiep",
                table: "KhachHangs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiaChiDoanhNghiep",
                table: "KhachHangs");

            migrationBuilder.DropColumn(
                name: "GiayPhepKinhDoanhUrl",
                table: "KhachHangs");

            migrationBuilder.DropColumn(
                name: "TinhThanhDoanhNghiep",
                table: "KhachHangs");

            migrationBuilder.AddColumn<double>(
                name: "AI_PriorityScore",
                table: "YeuCauThuGoms",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AI_SuggestedAction",
                table: "YeuCauThuGoms",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ThoiGianTon_ToiDa",
                table: "YeuCauThuGoms",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
