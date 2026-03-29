using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZoneGuide.API.Migrations
{
    public partial class RemoveUnusedPoiColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioDurationSeconds",
                table: "POIs");

            migrationBuilder.DropColumn(
                name: "MapDeepLink",
                table: "POIs");

            migrationBuilder.DropColumn(
                name: "ThumbnailUrl",
                table: "POIs");

            migrationBuilder.DropColumn(
                name: "AudioFilePath",
                table: "POITranslations");

            migrationBuilder.DropColumn(
                name: "ThumbnailPath",
                table: "Tours");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AudioDurationSeconds",
                table: "POIs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MapDeepLink",
                table: "POIs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailUrl",
                table: "POIs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AudioFilePath",
                table: "POITranslations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailPath",
                table: "Tours",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
