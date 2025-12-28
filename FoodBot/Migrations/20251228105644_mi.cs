using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodBot.Migrations
{
    /// <inheritdoc />
    public partial class mi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStartCodes_AppUsers_AppUserId",
                table: "AppStartCodes");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStartCodes_ExternalAccounts_ExternalAccountId",
                table: "AppStartCodes");

            migrationBuilder.DropForeignKey(
                name: "FK_ExternalAccounts_AppUsers_AppUserId",
                table: "ExternalAccounts");

            migrationBuilder.DropIndex(
                name: "IX_AnalysisReports2_AppUserId_Period_PeriodStartLocalDate",
                table: "AnalysisReports2");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppUsers",
                table: "AppUsers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppStartCodes",
                table: "AppStartCodes");

            migrationBuilder.RenameTable(
                name: "AppUsers",
                newName: "Users");

            migrationBuilder.RenameTable(
                name: "AppStartCodes",
                newName: "StartCodes");

            migrationBuilder.RenameIndex(
                name: "IX_AppStartCodes_ExternalAccountId",
                table: "StartCodes",
                newName: "IX_StartCodes_ExternalAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_AppStartCodes_Code",
                table: "StartCodes",
                newName: "IX_StartCodes_Code");

            migrationBuilder.RenameIndex(
                name: "IX_AppStartCodes_AppUserId_ExpiresAtUtc",
                table: "StartCodes",
                newName: "IX_StartCodes_AppUserId_ExpiresAtUtc");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StartCodes",
                table: "StartCodes",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalAccounts_Users_AppUserId",
                table: "ExternalAccounts",
                column: "AppUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StartCodes_ExternalAccounts_ExternalAccountId",
                table: "StartCodes",
                column: "ExternalAccountId",
                principalTable: "ExternalAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StartCodes_Users_AppUserId",
                table: "StartCodes",
                column: "AppUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExternalAccounts_Users_AppUserId",
                table: "ExternalAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_StartCodes_ExternalAccounts_ExternalAccountId",
                table: "StartCodes");

            migrationBuilder.DropForeignKey(
                name: "FK_StartCodes_Users_AppUserId",
                table: "StartCodes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StartCodes",
                table: "StartCodes");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "AppUsers");

            migrationBuilder.RenameTable(
                name: "StartCodes",
                newName: "AppStartCodes");

            migrationBuilder.RenameIndex(
                name: "IX_StartCodes_ExternalAccountId",
                table: "AppStartCodes",
                newName: "IX_AppStartCodes_ExternalAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_StartCodes_Code",
                table: "AppStartCodes",
                newName: "IX_AppStartCodes_Code");

            migrationBuilder.RenameIndex(
                name: "IX_StartCodes_AppUserId_ExpiresAtUtc",
                table: "AppStartCodes",
                newName: "IX_AppStartCodes_AppUserId_ExpiresAtUtc");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppUsers",
                table: "AppUsers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppStartCodes",
                table: "AppStartCodes",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisReports2_AppUserId_Period_PeriodStartLocalDate",
                table: "AnalysisReports2",
                columns: new[] { "AppUserId", "Period", "PeriodStartLocalDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_AppStartCodes_AppUsers_AppUserId",
                table: "AppStartCodes",
                column: "AppUserId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStartCodes_ExternalAccounts_ExternalAccountId",
                table: "AppStartCodes",
                column: "ExternalAccountId",
                principalTable: "ExternalAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalAccounts_AppUsers_AppUserId",
                table: "ExternalAccounts",
                column: "AppUserId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
