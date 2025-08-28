using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodBot.Migrations
{
    /// <inheritdoc />
    public partial class card : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PersonalCards",
                columns: table => new
                {
                    ChatId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    BirthYear = table.Column<int>(type: "int", nullable: true),
                    DietGoals = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    MedicalRestrictions = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalCards", x => x.ChatId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonalCards");
        }
    }
}
