using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    public partial class EmployeeBankAndMaritalStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankAccountNo",
                schema: "ppl",
                table: "Employee",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                schema: "ppl",
                table: "Employee",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "MaritalStatus",
                schema: "ppl",
                table: "Employee",
                type: "smallint",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankAccountNo",
                schema: "ppl",
                table: "Employee");

            migrationBuilder.DropColumn(
                name: "BankName",
                schema: "ppl",
                table: "Employee");

            migrationBuilder.DropColumn(
                name: "MaritalStatus",
                schema: "ppl",
                table: "Employee");
        }
    }
}
