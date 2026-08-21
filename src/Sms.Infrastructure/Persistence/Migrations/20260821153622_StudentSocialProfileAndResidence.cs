using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    public partial class StudentSocialProfileAndResidence : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BirthOrder",
                schema: "ppl",
                table: "Student",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FamilySize",
                schema: "ppl",
                table: "Student",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "FatherStatus",
                schema: "ppl",
                table: "Student",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "FinancialStatus",
                schema: "ppl",
                table: "Student",
                type: "smallint",
                nullable: true);

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

            migrationBuilder.AddColumn<short>(
                name: "MotherStatus",
                schema: "ppl",
                table: "Student",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NeighbourhoodId",
                schema: "ppl",
                table: "Student",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlaceOfBirth",
                schema: "ppl",
                table: "Student",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RationCardNo",
                schema: "ppl",
                table: "Student",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "Religion",
                schema: "ppl",
                table: "Student",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "ResidencyStatus",
                schema: "ppl",
                table: "Student",
                type: "smallint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Governorate",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Governorate", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResidenceArea",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    GovernorateId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResidenceArea", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResidenceArea_Governorate_GovernorateId",
                        column: x => x.GovernorateId,
                        principalSchema: "core",
                        principalTable: "Governorate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Neighbourhood",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ResidenceAreaId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Neighbourhood", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Neighbourhood_ResidenceArea_ResidenceAreaId",
                        column: x => x.ResidenceAreaId,
                        principalSchema: "core",
                        principalTable: "ResidenceArea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Student_Neighbourhood",
                schema: "ppl",
                table: "Student",
                column: "NeighbourhoodId",
                filter: "[NeighbourhoodId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Governorate_SchoolId_Code",
                schema: "core",
                table: "Governorate",
                columns: new[] { "SchoolId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Neighbourhood_ResidenceAreaId_Code",
                schema: "core",
                table: "Neighbourhood",
                columns: new[] { "ResidenceAreaId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResidenceArea_GovernorateId_Code",
                schema: "core",
                table: "ResidenceArea",
                columns: new[] { "GovernorateId", "Code" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Neighbourhood",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ResidenceArea",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Governorate",
                schema: "core");

            migrationBuilder.DropIndex(
                name: "IX_Student_Neighbourhood",
                schema: "ppl",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "BirthOrder",
                schema: "ppl",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "FamilySize",
                schema: "ppl",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "FatherStatus",
                schema: "ppl",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "FinancialStatus",
                schema: "ppl",
                table: "Student");

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

            migrationBuilder.DropColumn(
                name: "MotherStatus",
                schema: "ppl",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "NeighbourhoodId",
                schema: "ppl",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "PlaceOfBirth",
                schema: "ppl",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "RationCardNo",
                schema: "ppl",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "Religion",
                schema: "ppl",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "ResidencyStatus",
                schema: "ppl",
                table: "Student");
        }
    }
}
