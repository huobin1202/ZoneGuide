using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZoneGuide.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPoiTranslationOutdatedFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAudioOutdated",
                table: "POITranslations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOutdated",
                table: "POITranslations",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAudioOutdated",
                table: "POITranslations");

            migrationBuilder.DropColumn(
                name: "IsOutdated",
                table: "POITranslations");
        }
    }
}
