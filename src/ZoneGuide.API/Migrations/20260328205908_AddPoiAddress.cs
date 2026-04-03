using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZoneGuide.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPoiAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "POIs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "POIs");
        }
    }
}
