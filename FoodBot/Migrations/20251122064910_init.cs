using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodBot.Migrations
{
    /// <inheritdoc />
    public partial class init : Migration
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
                name: "AnalysisReports2",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    Period = table.Column<int>(type: "int", nullable: false),
                    RequestJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PeriodStartLocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Markdown = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CaloriesChecksum = table.Column<int>(type: "int", nullable: false),
                    IsProcessing = table.Column<bool>(type: "bit", nullable: false),
                    ProcessingStartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisReports2", x => x.Id);
                });

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
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    FileId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileMime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImageBytes = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    DishName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IngredientsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WeightG = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    ProteinsG = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    FatsG = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    CarbsG = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    CaloriesKcal = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Confidence = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    Step1Json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReasoningPrompt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClarifyNote = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingClarifies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    MealId = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NewTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingClarifies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingMeals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FileMime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImageBytes = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GenerateImage = table.Column<bool>(type: "bit", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    DesiredMealTimeUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClarifyNote = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingMeals", x => x.Id);
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
                    Format = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodPdfJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PersonalCards",
                columns: table => new
                {
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    BirthYear = table.Column<int>(type: "int", nullable: true),
                    Gender = table.Column<int>(type: "int", nullable: true),
                    HeightCm = table.Column<int>(type: "int", nullable: true),
                    WeightKg = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ActivityLevel = table.Column<int>(type: "int", nullable: true),
                    DailyCalories = table.Column<int>(type: "int", nullable: true),
                    DietGoals = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    MedicalRestrictions = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalCards", x => x.ChatId);
                });

            migrationBuilder.CreateTable(
                name: "StartCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ChatId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StartCodes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisPdfJobs_ChatId_CreatedAtUtc",
                table: "AnalysisPdfJobs",
                columns: new[] { "ChatId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Meals_ChatId_CreatedAtUtc",
                table: "Meals",
                columns: new[] { "ChatId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingClarifies_ChatId_CreatedAtUtc",
                table: "PendingClarifies",
                columns: new[] { "ChatId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingMeals_ChatId_CreatedAtUtc",
                table: "PendingMeals",
                columns: new[] { "ChatId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PeriodPdfJobs_ChatId_CreatedAtUtc",
                table: "PeriodPdfJobs",
                columns: new[] { "ChatId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StartCodes_ChatId_ExpiresAtUtc",
                table: "StartCodes",
                columns: new[] { "ChatId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StartCodes_Code",
                table: "StartCodes",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisPdfJobs");

            migrationBuilder.DropTable(
                name: "AnalysisReports2");

            migrationBuilder.DropTable(
                name: "Meals");

            migrationBuilder.DropTable(
                name: "PendingClarifies");

            migrationBuilder.DropTable(
                name: "PendingMeals");

            migrationBuilder.DropTable(
                name: "PeriodPdfJobs");

            migrationBuilder.DropTable(
                name: "PersonalCards");

            migrationBuilder.DropTable(
                name: "StartCodes");
        }
    }
}
