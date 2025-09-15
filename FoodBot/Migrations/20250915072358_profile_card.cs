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
            // Columns may already exist if the database was manually altered or
            // a previous migration partially succeeded. Guard each addition with
            // a check to avoid duplicate column errors.
            migrationBuilder.Sql(
                "IF COL_LENGTH('PersonalCards','ActivityLevel') IS NULL " +
                "ALTER TABLE PersonalCards ADD ActivityLevel int NULL;");

            migrationBuilder.Sql(
                "IF COL_LENGTH('PersonalCards','DailyCalories') IS NULL " +
                "ALTER TABLE PersonalCards ADD DailyCalories int NULL;");

            migrationBuilder.Sql(
                "IF COL_LENGTH('PersonalCards','Gender') IS NULL " +
                "ALTER TABLE PersonalCards ADD Gender int NULL;");

            migrationBuilder.Sql(
                "IF COL_LENGTH('PersonalCards','HeightCm') IS NULL " +
                "ALTER TABLE PersonalCards ADD HeightCm int NULL;");

            migrationBuilder.Sql(
                "IF COL_LENGTH('PersonalCards','WeightKg') IS NULL " +
                "ALTER TABLE PersonalCards ADD WeightKg decimal(18,2) NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF COL_LENGTH('PersonalCards','ActivityLevel') IS NOT NULL " +
                "ALTER TABLE PersonalCards DROP COLUMN ActivityLevel;");

            migrationBuilder.Sql(
                "IF COL_LENGTH('PersonalCards','DailyCalories') IS NOT NULL " +
                "ALTER TABLE PersonalCards DROP COLUMN DailyCalories;");

            migrationBuilder.Sql(
                "IF COL_LENGTH('PersonalCards','Gender') IS NOT NULL " +
                "ALTER TABLE PersonalCards DROP COLUMN Gender;");

            migrationBuilder.Sql(
                "IF COL_LENGTH('PersonalCards','HeightCm') IS NOT NULL " +
                "ALTER TABLE PersonalCards DROP COLUMN HeightCm;");

            migrationBuilder.Sql(
                "IF COL_LENGTH('PersonalCards','WeightKg') IS NOT NULL " +
                "ALTER TABLE PersonalCards DROP COLUMN WeightKg;");
        }
    }
}
