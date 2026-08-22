using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    public partial class StudentResidenceMovedToParent : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Student_Neighbourhood",
                schema: "ppl",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "NeighbourhoodId",
                schema: "ppl",
                table: "Student");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NeighbourhoodId",
                schema: "ppl",
                table: "Student",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Student_Neighbourhood",
                schema: "ppl",
                table: "Student",
                column: "NeighbourhoodId",
                filter: "[NeighbourhoodId] IS NOT NULL");
        }
    }
}
