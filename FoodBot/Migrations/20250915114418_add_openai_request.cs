using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodBot.Migrations
{
    /// <inheritdoc />
    public partial class add_openai_request : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequestJson",
                table: "AnalysisReports2",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestJson",
                table: "AnalysisReports2");
        }
    }
}
