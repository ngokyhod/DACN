using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DACS.Migrations
{
    /// <inheritdoc />
    public partial class AddEnterpriseCoordinatesAndAddressStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiaChiDuongDoanhNghiep",
                table: "KhachHangs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EnterpriseLat",
                table: "KhachHangs",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EnterpriseLng",
                table: "KhachHangs",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaQuanDoanhNghiep",
                table: "KhachHangs",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaTinhDoanhNghiep",
                table: "KhachHangs",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaXaDoanhNghiep",
                table: "KhachHangs",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiaChiDuongDoanhNghiep",
                table: "KhachHangs");

            migrationBuilder.DropColumn(
                name: "EnterpriseLat",
                table: "KhachHangs");

            migrationBuilder.DropColumn(
                name: "EnterpriseLng",
                table: "KhachHangs");

            migrationBuilder.DropColumn(
                name: "MaQuanDoanhNghiep",
                table: "KhachHangs");

            migrationBuilder.DropColumn(
                name: "MaTinhDoanhNghiep",
                table: "KhachHangs");

            migrationBuilder.DropColumn(
                name: "MaXaDoanhNghiep",
                table: "KhachHangs");
        }
    }
}
