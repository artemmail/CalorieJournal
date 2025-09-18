using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodBot.Migrations
{
    /// <inheritdoc />
    public partial class pendingmeal_time : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DesiredMealTimeUtc",
                table: "PendingMeals",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.Sql("UPDATE PendingMeals SET DesiredMealTimeUtc = CreatedAtUtc WHERE DesiredMealTimeUtc IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DesiredMealTimeUtc",
                table: "PendingMeals");
        }
    }
}
