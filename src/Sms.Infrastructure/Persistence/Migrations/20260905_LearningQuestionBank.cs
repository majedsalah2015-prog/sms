using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    public partial class LearningQuestionBank : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuestionBank",
                schema: "lrn",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    CurriculumOfferingId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ShareScope = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionBank", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionBank_CurriculumOffering_CurriculumOfferingId",
                        column: x => x.CurriculumOfferingId,
                        principalSchema: "core",
                        principalTable: "CurriculumOffering",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Question",
                schema: "lrn",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    QuestionBankId = table.Column<int>(type: "int", nullable: false),
                    RootQuestionId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsCurrentVersion = table.Column<bool>(type: "bit", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    StemAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StemEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Marks = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    Difficulty = table.Column<int>(type: "int", nullable: false),
                    LessonId = table.Column<int>(type: "int", nullable: true),
                    NumericTolerance = table.Column<decimal>(type: "decimal(12,4)", nullable: true),
                    ExplanationAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExplanationEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeprecated = table.Column<bool>(type: "bit", nullable: false),
                    DeprecatedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Question", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Question_Lesson_LessonId",
                        column: x => x.LessonId,
                        principalSchema: "lrn",
                        principalTable: "Lesson",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Question_QuestionBank_QuestionBankId",
                        column: x => x.QuestionBankId,
                        principalSchema: "lrn",
                        principalTable: "QuestionBank",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestionAcceptedAnswer",
                schema: "lrn",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionAcceptedAnswer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionAcceptedAnswer_Question_QuestionId",
                        column: x => x.QuestionId,
                        principalSchema: "lrn",
                        principalTable: "Question",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestionOption",
                schema: "lrn",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<int>(type: "int", nullable: false),
                    TextAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TextEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionOption", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionOption_Question_QuestionId",
                        column: x => x.QuestionId,
                        principalSchema: "lrn",
                        principalTable: "Question",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Question_Bank_Live",
                schema: "lrn",
                table: "Question",
                columns: new[] { "QuestionBankId", "IsCurrentVersion", "IsDeprecated" });

            migrationBuilder.CreateIndex(
                name: "IX_Question_LessonId",
                schema: "lrn",
                table: "Question",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "UQ_Question_Root_Version",
                schema: "lrn",
                table: "Question",
                columns: new[] { "RootQuestionId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionAcceptedAnswer_Question",
                schema: "lrn",
                table: "QuestionAcceptedAnswer",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBank_Offering",
                schema: "lrn",
                table: "QuestionBank",
                column: "CurriculumOfferingId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionOption_Question_Order",
                schema: "lrn",
                table: "QuestionOption",
                columns: new[] { "QuestionId", "DisplayOrder" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionAcceptedAnswer",
                schema: "lrn");

            migrationBuilder.DropTable(
                name: "QuestionOption",
                schema: "lrn");

            migrationBuilder.DropTable(
                name: "Question",
                schema: "lrn");

            migrationBuilder.DropTable(
                name: "QuestionBank",
                schema: "lrn");
        }
    }
}
