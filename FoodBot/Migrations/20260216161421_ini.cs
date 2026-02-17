using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodBot.Migrations
{
    /// <inheritdoc />
    public partial class ini : Migration
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
                name: "AppUsers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StorageChatId = table.Column<long>(type: "bigint", nullable: false),
                    IsAnonymous = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "active"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUsers", x => x.Id);
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
                name: "AppIdentities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppUserId = table.Column<long>(type: "bigint", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExternalUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExternalUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    VerifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppIdentities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppIdentities_AppUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppUserDevices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppUserId = table.Column<long>(type: "bigint", nullable: false),
                    InstallId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DeviceName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppUserDevices_AppUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StartCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ChatId = table.Column<long>(type: "bigint", nullable: true),
                    AppUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StartCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StartCodes_AppUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AppRefreshTokens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppUserId = table.Column<long>(type: "bigint", nullable: false),
                    AppUserDeviceId = table.Column<long>(type: "bigint", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppRefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppRefreshTokens_AppUserDevices_AppUserDeviceId",
                        column: x => x.AppUserDeviceId,
                        principalTable: "AppUserDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppRefreshTokens_AppUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisPdfJobs_ChatId_CreatedAtUtc",
                table: "AnalysisPdfJobs",
                columns: new[] { "ChatId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AppIdentities_AppUserId",
                table: "AppIdentities",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppIdentities_Provider_ExternalUserId",
                table: "AppIdentities",
                columns: new[] { "Provider", "ExternalUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppRefreshTokens_AppUserDeviceId",
                table: "AppRefreshTokens",
                column: "AppUserDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppRefreshTokens_AppUserId_ExpiresAtUtc",
                table: "AppRefreshTokens",
                columns: new[] { "AppUserId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AppRefreshTokens_TokenHash",
                table: "AppRefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUserDevices_AppUserId",
                table: "AppUserDevices",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserDevices_InstallId",
                table: "AppUserDevices",
                column: "InstallId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_StorageChatId",
                table: "AppUsers",
                column: "StorageChatId",
                unique: true);

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
                name: "IX_StartCodes_AppUserId_ExpiresAtUtc",
                table: "StartCodes",
                columns: new[] { "AppUserId", "ExpiresAtUtc" });

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
                name: "AppIdentities");

            migrationBuilder.DropTable(
                name: "AppRefreshTokens");

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

            migrationBuilder.DropTable(
                name: "AppUserDevices");

            migrationBuilder.DropTable(
                name: "AppUsers");
        }
    }
}
