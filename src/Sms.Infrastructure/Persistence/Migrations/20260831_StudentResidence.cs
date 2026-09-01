using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    public partial class StudentResidence : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NeighbourhoodId",
                schema: "ppl",
                table: "Student",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResidenceAreaId",
                schema: "ppl",
                table: "Student",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Student_ResidenceArea",
                schema: "ppl",
                table: "Student",
                column: "ResidenceAreaId",
                filter: "[ResidenceAreaId] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Student_ResidenceArea",
                schema: "ppl",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "NeighbourhoodId",
                schema: "ppl",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "ResidenceAreaId",
                schema: "ppl",
                table: "Student");
        }
    }
}
