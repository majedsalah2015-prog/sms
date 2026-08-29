using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The mother stops being five columns on her children and becomes a guardian like the father
    /// (owner request, 2026-08-24): ppl.Parent gains the qualification it lacked, and ppl.Student
    /// loses the copy-per-child that made a corrected mobile a correction per sibling.
    /// <para>
    /// <strong>This drops data.</strong> Any mother's name, ID number, mobile, occupation or
    /// qualification already recorded on a student is gone with the columns — there is nowhere to
    /// move it to automatically, because the same five values on four siblings may be one woman or
    /// four different ones and only the school knows which. Re-enter them on her parent file, or
    /// bring them in through the Access import, which creates the guardian files as it goes.
    /// </para>
    /// </summary>
    public partial class ParentQualificationAndMotherAsGuardian : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MotherEducationLookupId",
                schema: "ppl",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "MotherMobile",
                schema: "ppl",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "MotherName",
                schema: "ppl",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "MotherNationalId",
                schema: "ppl",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "MotherOccupation",
                schema: "ppl",
                table: "Student");

            migrationBuilder.AddColumn<int>(
                name: "EducationLookupId",
                schema: "ppl",
                table: "Parent",
                type: "int",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EducationLookupId",
                schema: "ppl",
                table: "Parent");

            migrationBuilder.AddColumn<int>(
                name: "MotherEducationLookupId",
                schema: "ppl",
                table: "Student",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherMobile",
                schema: "ppl",
                table: "Student",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherName",
                schema: "ppl",
                table: "Student",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherNationalId",
                schema: "ppl",
                table: "Student",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherOccupation",
                schema: "ppl",
                table: "Student",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
