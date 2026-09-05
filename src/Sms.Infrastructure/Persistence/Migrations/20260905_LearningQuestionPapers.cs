using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    public partial class LearningOnlinePaper : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OnlinePaper",
                schema: "lrn",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    QuestionBankId = table.Column<int>(type: "int", nullable: false),
                    BlueprintComponentId = table.Column<int>(type: "int", nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawnReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WithdrawnAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnlinePaper", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnlinePaper_BlueprintComponent_BlueprintComponentId",
                        column: x => x.BlueprintComponentId,
                        principalSchema: "core",
                        principalTable: "BlueprintComponent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnlinePaper_QuestionBank_QuestionBankId",
                        column: x => x.QuestionBankId,
                        principalSchema: "lrn",
                        principalTable: "QuestionBank",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaperItem",
                schema: "lrn",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    OnlinePaperId = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    Marks = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaperItem_OnlinePaper_OnlinePaperId",
                        column: x => x.OnlinePaperId,
                        principalSchema: "lrn",
                        principalTable: "OnlinePaper",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaperItem_Question_QuestionId",
                        column: x => x.QuestionId,
                        principalSchema: "lrn",
                        principalTable: "Question",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnlinePaper_Bank_Status",
                schema: "lrn",
                table: "OnlinePaper",
                columns: new[] { "QuestionBankId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OnlinePaper_Component",
                schema: "lrn",
                table: "OnlinePaper",
                column: "BlueprintComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaperItem_Paper_Order",
                schema: "lrn",
                table: "PaperItem",
                columns: new[] { "OnlinePaperId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PaperItem_QuestionId",
                schema: "lrn",
                table: "PaperItem",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "UQ_PaperItem_Paper_Question",
                schema: "lrn",
                table: "PaperItem",
                columns: new[] { "OnlinePaperId", "QuestionId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaperItem",
                schema: "lrn");

            migrationBuilder.DropTable(
                name: "OnlinePaper",
                schema: "lrn");
        }
    }
}
