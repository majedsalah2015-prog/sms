using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    public partial class CafeteriaVat : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                schema: "svc",
                table: "Sale",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                schema: "svc",
                table: "CafeteriaItem",
                type: "decimal(5,4)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VatAmount",
                schema: "svc",
                table: "Sale");

            migrationBuilder.DropColumn(
                name: "VatRate",
                schema: "svc",
                table: "CafeteriaItem");
        }
    }
}
