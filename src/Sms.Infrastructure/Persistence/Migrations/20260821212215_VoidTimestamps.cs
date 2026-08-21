using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    public partial class VoidTimestamps : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAtUtc",
                schema: "svc",
                table: "StoreSale",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAtUtc",
                schema: "svc",
                table: "Sale",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAtUtc",
                schema: "ppl",
                table: "Charge",
                type: "datetime2",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VoidedAtUtc",
                schema: "svc",
                table: "StoreSale");

            migrationBuilder.DropColumn(
                name: "VoidedAtUtc",
                schema: "svc",
                table: "Sale");

            migrationBuilder.DropColumn(
                name: "VoidedAtUtc",
                schema: "ppl",
                table: "Charge");
        }
    }
}
