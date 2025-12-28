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
                name: "AppUsers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisPdfJobs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppUserId = table.Column<long>(type: "bigint", nullable: false),
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
                    AppUserId = table.Column<long>(type: "bigint", nullable: false),
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
                    AppUserId = table.Column<long>(type: "bigint", nullable: false),
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
                    AppUserId = table.Column<long>(type: "bigint", nullable: false),
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
                    AppUserId = table.Column<long>(type: "bigint", nullable: false),
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
                name: "PersonalCards",
                columns: table => new
                {
                    AppUserId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_PersonalCards", x => x.AppUserId);
                });

            migrationBuilder.CreateTable(
                name: "AppStartCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AppUserId = table.Column<long>(type: "bigint", nullable: true),
                    ExternalAccountId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStartCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStartCodes_AppUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PeriodPdfJobs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppUserId = table.Column<long>(type: "bigint", nullable: false),
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
                name: "ExternalAccounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Username = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AppUserId = table.Column<long>(type: "bigint", nullable: false),
                    LinkedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalAccounts_AppUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisPdfJobs_AppUserId_CreatedAtUtc",
                table: "AnalysisPdfJobs",
                columns: new[] { "AppUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisReports2_AppUserId_Period_PeriodStartLocalDate",
                table: "AnalysisReports2",
                columns: new[] { "AppUserId", "Period", "PeriodStartLocalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStartCodes_AppUserId_ExpiresAtUtc",
                table: "AppStartCodes",
                columns: new[] { "AppUserId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStartCodes_Code",
                table: "AppStartCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStartCodes_ExternalAccountId",
                table: "AppStartCodes",
                column: "ExternalAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAccounts_AppUserId",
                table: "ExternalAccounts",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAccounts_Provider_ExternalId",
                table: "ExternalAccounts",
                columns: new[] { "Provider", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meals_AppUserId_CreatedAtUtc",
                table: "Meals",
                columns: new[] { "AppUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingClarifies_AppUserId_CreatedAtUtc",
                table: "PendingClarifies",
                columns: new[] { "AppUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingMeals_AppUserId_CreatedAtUtc",
                table: "PendingMeals",
                columns: new[] { "AppUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PeriodPdfJobs_AppUserId_CreatedAtUtc",
                table: "PeriodPdfJobs",
                columns: new[] { "AppUserId", "CreatedAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_AppStartCodes_ExternalAccounts_ExternalAccountId",
                table: "AppStartCodes",
                column: "ExternalAccountId",
                principalTable: "ExternalAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStartCodes_ExternalAccounts_ExternalAccountId",
                table: "AppStartCodes");

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
                name: "PersonalCards");

            migrationBuilder.DropTable(
                name: "PeriodPdfJobs");

            migrationBuilder.DropTable(
                name: "AppStartCodes");

            migrationBuilder.DropTable(
                name: "ExternalAccounts");

            migrationBuilder.DropTable(
                name: "AppUsers");
        }
    }
}
