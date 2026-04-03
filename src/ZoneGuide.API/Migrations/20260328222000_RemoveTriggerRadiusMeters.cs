using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZoneGuide.API.Migrations
{
    public partial class RemoveTriggerRadiusMeters : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE POIs
                SET TriggerRadius = TriggerRadiusMeters
                WHERE TriggerRadius <> TriggerRadiusMeters
            """);

            migrationBuilder.DropColumn(
                name: "TriggerRadiusMeters",
                table: "POIs");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "TriggerRadiusMeters",
                table: "POIs",
                type: "float",
                nullable: false,
                defaultValue: 50.0);

            migrationBuilder.Sql("""
                UPDATE POIs
                SET TriggerRadiusMeters = TriggerRadius
            """);
        }
    }
}
