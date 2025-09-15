using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodBot.Migrations
{
    /// <inheritdoc />
    public partial class profile_card : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActivityLevel",
                table: "PersonalCards",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DailyCalories",
                table: "PersonalCards",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Gender",
                table: "PersonalCards",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HeightCm",
                table: "PersonalCards",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightKg",
                table: "PersonalCards",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivityLevel",
                table: "PersonalCards");

            migrationBuilder.DropColumn(
                name: "DailyCalories",
                table: "PersonalCards");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "PersonalCards");

            migrationBuilder.DropColumn(
                name: "HeightCm",
                table: "PersonalCards");

            migrationBuilder.DropColumn(
                name: "WeightKg",
                table: "PersonalCards");
        }
    }
}
