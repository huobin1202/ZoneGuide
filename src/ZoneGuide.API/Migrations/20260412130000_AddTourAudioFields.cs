using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ZoneGuide.API.Data;

#nullable disable

namespace ZoneGuide.API.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260412130000_AddTourAudioFields")]
    public partial class AddTourAudioFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AudioUrl",
                table: "Tours",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AudioUrl",
                table: "TourTranslations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAudioOutdated",
                table: "TourTranslations",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioUrl",
                table: "Tours");

            migrationBuilder.DropColumn(
                name: "AudioUrl",
                table: "TourTranslations");

            migrationBuilder.DropColumn(
                name: "IsAudioOutdated",
                table: "TourTranslations");
        }
    }
}
