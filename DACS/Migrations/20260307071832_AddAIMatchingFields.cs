using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DACS.Migrations
{
    /// <inheritdoc />
    public partial class AddAIMatchingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<bool>(
                name: "IsEnterpriseVerified",
                table: "KhachHangs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LinhVucHoatDong",
                table: "KhachHangs",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NhuCauChinh",
                table: "KhachHangs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenDoanhNghiep",
                table: "KhachHangs",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.DropColumn(
                name: "IsEnterpriseVerified",
                table: "KhachHangs");

            migrationBuilder.DropColumn(
                name: "LinhVucHoatDong",
                table: "KhachHangs");

            migrationBuilder.DropColumn(
                name: "NhuCauChinh",
                table: "KhachHangs");

            migrationBuilder.DropColumn(
                name: "TenDoanhNghiep",
                table: "KhachHangs");
        }
    }
}
