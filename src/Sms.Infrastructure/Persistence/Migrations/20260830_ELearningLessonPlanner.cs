using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    public partial class ELearningLessonPlanner : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "lrn");

            migrationBuilder.CreateTable(
                name: "Lesson",
                schema: "lrn",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    CurriculumOfferingId = table.Column<int>(type: "int", nullable: false),
                    SessionId = table.Column<int>(type: "int", nullable: true),
                    WeekNumber = table.Column<int>(type: "int", nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ObjectivesAr = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ObjectivesEn = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetiredReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RetiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lesson", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lesson_CurriculumOffering_CurriculumOfferingId",
                        column: x => x.CurriculumOfferingId,
                        principalSchema: "core",
                        principalTable: "CurriculumOffering",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lesson_Session_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "core",
                        principalTable: "Session",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LessonResource",
                schema: "lrn",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    LessonId = table.Column<int>(type: "int", nullable: false),
                    AttachmentId = table.Column<int>(type: "int", nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonResource", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonResource_Attachment_AttachmentId",
                        column: x => x.AttachmentId,
                        principalSchema: "doc",
                        principalTable: "Attachment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonResource_Lesson_LessonId",
                        column: x => x.LessonId,
                        principalSchema: "lrn",
                        principalTable: "Lesson",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Lesson_CurriculumOfferingId_WeekNumber",
                schema: "lrn",
                table: "Lesson",
                columns: new[] { "CurriculumOfferingId", "WeekNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Lesson_SessionId",
                schema: "lrn",
                table: "Lesson",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonResource_AttachmentId",
                schema: "lrn",
                table: "LessonResource",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonResource_LessonId_DisplayOrder",
                schema: "lrn",
                table: "LessonResource",
                columns: new[] { "LessonId", "DisplayOrder" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LessonResource",
                schema: "lrn");

            migrationBuilder.DropTable(
                name: "Lesson",
                schema: "lrn");
        }
    }
}
