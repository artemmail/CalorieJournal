using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodBot.Migrations
{
    /// <inheritdoc />
    public partial class pdfjob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalysisPdfJobs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    ReportId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisPdfJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PeriodPdfJobs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    From = table.Column<DateTime>(type: "datetime2", nullable: false),
                    To = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodPdfJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisPdfJobs_ChatId_CreatedAtUtc",
                table: "AnalysisPdfJobs",
                columns: new[] { "ChatId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PeriodPdfJobs_ChatId_CreatedAtUtc",
                table: "PeriodPdfJobs",
                columns: new[] { "ChatId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisPdfJobs");

            migrationBuilder.DropTable(
                name: "PeriodPdfJobs");
        }
    }
}
