using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    public partial class EmployeeBankLookup : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BankLookupId",
                schema: "ppl",
                table: "Employee",
                type: "int",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankLookupId",
                schema: "ppl",
                table: "Employee");
        }
    }
}
