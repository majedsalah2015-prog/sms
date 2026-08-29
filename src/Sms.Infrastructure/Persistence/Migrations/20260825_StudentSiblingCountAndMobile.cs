using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    public partial class StudentSiblingCountAndMobile : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Mobile",
                schema: "ppl",
                table: "Student",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SiblingCount",
                schema: "ppl",
                table: "Student",
                type: "int",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Mobile",
                schema: "ppl",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "SiblingCount",
                schema: "ppl",
                table: "Student");
        }
    }
}
