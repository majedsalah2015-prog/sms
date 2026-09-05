using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    public partial class LearningHomeworkSubmissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HomeworkSubmission",
                schema: "lrn",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    HomeworkId = table.Column<int>(type: "int", nullable: false),
                    EnrollmentId = table.Column<int>(type: "int", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsLate = table.Column<bool>(type: "bit", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(7,2)", nullable: true),
                    Feedback = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MarkedByUserAccountId = table.Column<int>(type: "int", nullable: true),
                    MarkedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    VersionCount = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeworkSubmission", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeworkSubmission_Enrollment_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalSchema: "ppl",
                        principalTable: "Enrollment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HomeworkSubmission_Homework_HomeworkId",
                        column: x => x.HomeworkId,
                        principalSchema: "lrn",
                        principalTable: "Homework",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubmissionVersion",
                schema: "lrn",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    HomeworkSubmissionId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    TextResponse = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsLate = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionVersion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubmissionVersion_HomeworkSubmission_HomeworkSubmissionId",
                        column: x => x.HomeworkSubmissionId,
                        principalSchema: "lrn",
                        principalTable: "HomeworkSubmission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubmissionAttachment",
                schema: "lrn",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    SubmissionVersionId = table.Column<int>(type: "int", nullable: false),
                    AttachmentId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionAttachment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubmissionAttachment_Attachment_AttachmentId",
                        column: x => x.AttachmentId,
                        principalSchema: "doc",
                        principalTable: "Attachment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubmissionAttachment_SubmissionVersion_SubmissionVersionId",
                        column: x => x.SubmissionVersionId,
                        principalSchema: "lrn",
                        principalTable: "SubmissionVersion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkSubmission_EnrollmentId",
                schema: "lrn",
                table: "HomeworkSubmission",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkSubmission_HomeworkId_Status",
                schema: "lrn",
                table: "HomeworkSubmission",
                columns: new[] { "HomeworkId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UQ_HomeworkSubmission_Homework_Enrollment",
                schema: "lrn",
                table: "HomeworkSubmission",
                columns: new[] { "HomeworkId", "EnrollmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionAttachment_AttachmentId",
                schema: "lrn",
                table: "SubmissionAttachment",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "UQ_SubmissionAttachment_Version_Attachment",
                schema: "lrn",
                table: "SubmissionAttachment",
                columns: new[] { "SubmissionVersionId", "AttachmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_SubmissionVersion_Submission_Version",
                schema: "lrn",
                table: "SubmissionVersion",
                columns: new[] { "HomeworkSubmissionId", "VersionNumber" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubmissionAttachment",
                schema: "lrn");

            migrationBuilder.DropTable(
                name: "SubmissionVersion",
                schema: "lrn");

            migrationBuilder.DropTable(
                name: "HomeworkSubmission",
                schema: "lrn");
        }
    }
}
