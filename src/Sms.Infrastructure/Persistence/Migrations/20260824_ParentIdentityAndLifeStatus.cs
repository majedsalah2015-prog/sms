using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    public partial class ParentIdentityAndLifeStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "LifeStatus",
                schema: "ppl",
                table: "Parent",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<string>(
                name: "LifeStatusNote",
                schema: "ppl",
                table: "Parent",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryIdNo",
                schema: "ppl",
                table: "Parent",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrimaryIdTypeLookupId",
                schema: "ppl",
                table: "Parent",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parent_PrimaryIdNo",
                schema: "ppl",
                table: "Parent",
                columns: new[] { "SchoolId", "PrimaryIdNo" },
                filter: "[PrimaryIdNo] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Parent_PrimaryIdNo",
                schema: "ppl",
                table: "Parent");

            migrationBuilder.DropColumn(
                name: "LifeStatus",
                schema: "ppl",
                table: "Parent");

            migrationBuilder.DropColumn(
                name: "LifeStatusNote",
                schema: "ppl",
                table: "Parent");

            migrationBuilder.DropColumn(
                name: "PrimaryIdNo",
                schema: "ppl",
                table: "Parent");

            migrationBuilder.DropColumn(
                name: "PrimaryIdTypeLookupId",
                schema: "ppl",
                table: "Parent");
        }
    }
}
