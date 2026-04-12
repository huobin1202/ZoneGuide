using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ZoneGuide.API.Data;

#nullable disable

namespace ZoneGuide.API.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260412112000_RemoveTourDifficultyColumns")]
    public partial class RemoveTourDifficultyColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "Tours");

            migrationBuilder.DropColumn(
                name: "DifficultyLevel",
                table: "Tours");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Difficulty",
                table: "Tours",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Easy");

            migrationBuilder.AddColumn<int>(
                name: "DifficultyLevel",
                table: "Tours",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }
    }
}
