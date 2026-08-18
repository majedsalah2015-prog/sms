using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    public partial class SystemSetup : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CountryPackId",
                schema: "core",
                table: "School",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SetupCompletedAtUtc",
                schema: "core",
                table: "School",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CountryPack",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CountryIsoCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    DefaultCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DefaultTimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DefaultVatRate = table.Column<decimal>(type: "decimal(9,6)", nullable: false),
                    HijriDisplayDefault = table.Column<bool>(type: "bit", nullable: false),
                    RequiredIdTypeCodes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AuditRetentionYearsMinimum = table.Column<int>(type: "int", nullable: false),
                    StatutoryReportCodes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DefaultWorkingDays = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountryPack", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeatureToggle",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    FeatureCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureToggle", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchoolSetting",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: true),
                    Value = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ValueType = table.Column<short>(type: "smallint", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolSetting", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchoolSetting_AcademicYear_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalSchema: "core",
                        principalTable: "AcademicYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SetupChecklist",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StepCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedByUserId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetupChecklist", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_School_CountryPackId",
                schema: "core",
                table: "School",
                column: "CountryPackId");

            migrationBuilder.CreateIndex(
                name: "IX_CountryPack_Code_Version",
                schema: "core",
                table: "CountryPack",
                columns: new[] { "Code", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeatureToggle_SchoolId_FeatureCode",
                schema: "core",
                table: "FeatureToggle",
                columns: new[] { "SchoolId", "FeatureCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolSetting_AcademicYearId",
                schema: "core",
                table: "SchoolSetting",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolSetting_SchoolId_Key_AcademicYearId",
                schema: "core",
                table: "SchoolSetting",
                columns: new[] { "SchoolId", "Key", "AcademicYearId" },
                unique: true,
                filter: "[AcademicYearId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SetupChecklist_SchoolId_StepCode",
                schema: "core",
                table: "SetupChecklist",
                columns: new[] { "SchoolId", "StepCode" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_School_CountryPack_CountryPackId",
                schema: "core",
                table: "School",
                column: "CountryPackId",
                principalSchema: "core",
                principalTable: "CountryPack",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_School_CountryPack_CountryPackId",
                schema: "core",
                table: "School");

            migrationBuilder.DropTable(
                name: "CountryPack",
                schema: "core");

            migrationBuilder.DropTable(
                name: "FeatureToggle",
                schema: "core");

            migrationBuilder.DropTable(
                name: "SchoolSetting",
                schema: "core");

            migrationBuilder.DropTable(
                name: "SetupChecklist",
                schema: "core");

            migrationBuilder.DropIndex(
                name: "IX_School_CountryPackId",
                schema: "core",
                table: "School");

            migrationBuilder.DropColumn(
                name: "CountryPackId",
                schema: "core",
                table: "School");

            migrationBuilder.DropColumn(
                name: "SetupCompletedAtUtc",
                schema: "core",
                table: "School");
        }
    }
}
