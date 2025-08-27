using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodBot.Migrations
{
    /// <inheritdoc />
    public partial class initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Meals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FileId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileMime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImageBytes = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    DishName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IngredientsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProteinsG = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    FatsG = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    CarbsG = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    CaloriesKcal = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Confidence = table.Column<decimal>(type: "decimal(5,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Meals_ChatId_CreatedAtUtc",
                table: "Meals",
                columns: new[] { "ChatId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Meals");
        }
    }
}
