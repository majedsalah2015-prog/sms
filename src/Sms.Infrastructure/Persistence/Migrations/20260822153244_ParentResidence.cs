using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    public partial class ParentResidence : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NeighbourhoodId",
                schema: "ppl",
                table: "Parent",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResidenceAreaId",
                schema: "ppl",
                table: "Parent",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parent_ResidenceArea",
                schema: "ppl",
                table: "Parent",
                column: "ResidenceAreaId",
                filter: "[ResidenceAreaId] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Parent_ResidenceArea",
                schema: "ppl",
                table: "Parent");

            migrationBuilder.DropColumn(
                name: "NeighbourhoodId",
                schema: "ppl",
                table: "Parent");

            migrationBuilder.DropColumn(
                name: "ResidenceAreaId",
                schema: "ppl",
                table: "Parent");
        }
    }
}
