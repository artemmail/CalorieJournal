using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using FoodBot.Data;

#nullable disable

namespace FoodBot.Migrations
{
    [DbContext(typeof(BotDbContext))]
    [Migration("20250915130000_sourcetype")]
    public partial class sourcetype : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceType",
                table: "Meals",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "Meals");
        }
    }
}
