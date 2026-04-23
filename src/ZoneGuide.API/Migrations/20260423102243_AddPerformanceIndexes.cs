using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZoneGuide.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "POIs",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_POITranslations_UpdatedAt",
                table: "POITranslations",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_POIs_IsActive_Category",
                table: "POIs",
                columns: new[] { "IsActive", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_POIs_Latitude_Longitude",
                table: "POIs",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_POIs_UpdatedAt",
                table: "POIs",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_POITranslations_UpdatedAt",
                table: "POITranslations");

            migrationBuilder.DropIndex(
                name: "IX_POIs_IsActive_Category",
                table: "POIs");

            migrationBuilder.DropIndex(
                name: "IX_POIs_Latitude_Longitude",
                table: "POIs");

            migrationBuilder.DropIndex(
                name: "IX_POIs_UpdatedAt",
                table: "POIs");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "POIs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}
