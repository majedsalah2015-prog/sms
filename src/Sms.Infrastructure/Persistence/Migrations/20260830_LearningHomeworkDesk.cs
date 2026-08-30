using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    public partial class LearningHomeworkDesk : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Homework",
                schema: "lrn",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    CurriculumOfferingId = table.Column<int>(type: "int", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InstructionsAr = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    InstructionsEn = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MaxMarks = table.Column<decimal>(type: "decimal(7,2)", nullable: true),
                    BlueprintComponentId = table.Column<int>(type: "int", nullable: true),
                    LatenessPolicy = table.Column<int>(type: "int", nullable: false),
                    LatePenaltyPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawnReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WithdrawnAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Homework", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Homework_BlueprintComponent_BlueprintComponentId",
                        column: x => x.BlueprintComponentId,
                        principalSchema: "core",
                        principalTable: "BlueprintComponent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Homework_CurriculumOffering_CurriculumOfferingId",
                        column: x => x.CurriculumOfferingId,
                        principalSchema: "core",
                        principalTable: "CurriculumOffering",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Homework_Section_SectionId",
                        column: x => x.SectionId,
                        principalSchema: "core",
                        principalTable: "Section",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Homework_BlueprintComponentId",
                schema: "lrn",
                table: "Homework",
                column: "BlueprintComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_Homework_CurriculumOfferingId",
                schema: "lrn",
                table: "Homework",
                column: "CurriculumOfferingId");

            migrationBuilder.CreateIndex(
                name: "IX_Homework_SectionId_DueDate",
                schema: "lrn",
                table: "Homework",
                columns: new[] { "SectionId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Homework_SectionId_Status",
                schema: "lrn",
                table: "Homework",
                columns: new[] { "SectionId", "Status" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Homework",
                schema: "lrn");
        }
    }
}
