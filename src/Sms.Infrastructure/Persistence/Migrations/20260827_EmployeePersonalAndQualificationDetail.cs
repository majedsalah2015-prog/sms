using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    public partial class EmployeePersonalAndQualificationDetail : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AcademicGradeLookupId",
                schema: "ppl",
                table: "Qualification",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EducationLookupId",
                schema: "ppl",
                table: "Qualification",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Gpa",
                schema: "ppl",
                table: "Qualification",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpecializationLookupId",
                schema: "ppl",
                table: "Qualification",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UniversityLookupId",
                schema: "ppl",
                table: "Qualification",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                schema: "ppl",
                table: "Employee",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JawwalPayWalletNo",
                schema: "ppl",
                table: "Employee",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginTown",
                schema: "ppl",
                table: "Employee",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PalPayWalletNo",
                schema: "ppl",
                table: "Employee",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpouseIdNo",
                schema: "ppl",
                table: "Employee",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpouseIdTypeLookupId",
                schema: "ppl",
                table: "Employee",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppNumber",
                schema: "ppl",
                table: "Employee",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcademicGradeLookupId",
                schema: "ppl",
                table: "Qualification");

            migrationBuilder.DropColumn(
                name: "EducationLookupId",
                schema: "ppl",
                table: "Qualification");

            migrationBuilder.DropColumn(
                name: "Gpa",
                schema: "ppl",
                table: "Qualification");

            migrationBuilder.DropColumn(
                name: "SpecializationLookupId",
                schema: "ppl",
                table: "Qualification");

            migrationBuilder.DropColumn(
                name: "UniversityLookupId",
                schema: "ppl",
                table: "Qualification");

            migrationBuilder.DropColumn(
                name: "Address",
                schema: "ppl",
                table: "Employee");

            migrationBuilder.DropColumn(
                name: "JawwalPayWalletNo",
                schema: "ppl",
                table: "Employee");

            migrationBuilder.DropColumn(
                name: "OriginTown",
                schema: "ppl",
                table: "Employee");

            migrationBuilder.DropColumn(
                name: "PalPayWalletNo",
                schema: "ppl",
                table: "Employee");

            migrationBuilder.DropColumn(
                name: "SpouseIdNo",
                schema: "ppl",
                table: "Employee");

            migrationBuilder.DropColumn(
                name: "SpouseIdTypeLookupId",
                schema: "ppl",
                table: "Employee");

            migrationBuilder.DropColumn(
                name: "WhatsAppNumber",
                schema: "ppl",
                table: "Employee");
        }
    }
}
