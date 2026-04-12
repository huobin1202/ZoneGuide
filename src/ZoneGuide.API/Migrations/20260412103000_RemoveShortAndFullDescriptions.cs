using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ZoneGuide.API.Data;

#nullable disable

namespace ZoneGuide.API.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260412103000_RemoveShortAndFullDescriptions")]
    public partial class RemoveShortAndFullDescriptions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShortDescription",
                table: "POIs");

            migrationBuilder.DropColumn(
                name: "FullDescription",
                table: "POIs");

            migrationBuilder.DropColumn(
                name: "ShortDescription",
                table: "POITranslations");

            migrationBuilder.DropColumn(
                name: "FullDescription",
                table: "POITranslations");

            migrationBuilder.DropColumn(
                name: "ShortDescription",
                table: "POIContributions");

            migrationBuilder.DropColumn(
                name: "FullDescription",
                table: "POIContributions");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShortDescription",
                table: "POIs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FullDescription",
                table: "POIs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortDescription",
                table: "POITranslations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FullDescription",
                table: "POITranslations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ShortDescription",
                table: "POIContributions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FullDescription",
                table: "POIContributions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
