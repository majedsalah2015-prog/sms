using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ppl.CollectionNotice — doc/Modules/20 §8.5's human-issued arrears notices.
    /// <para>
    /// Hand-trimmed. EF generated this against a working tree that also held
    /// another epic's unmigrated <c>CollectionAccount</c>, so the scaffolded file
    /// carried both, as did that epic's own migration a few seconds later. Two
    /// migrations creating one table is a failed deployment, so each keeps only
    /// its own: this one creates <c>CollectionNotice</c> and nothing else.
    /// </para>
    /// </summary>
    public partial class CollectionNotices : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollectionNotice",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    NoticeNo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    PayerId = table.Column<int>(type: "int", nullable: true),
                    Channel = table.Column<short>(type: "smallint", nullable: false),
                    WindowFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WindowTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AmountDue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionNotice", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionNotice_Payer_PayerId",
                        column: x => x.PayerId,
                        principalSchema: "ppl",
                        principalTable: "Payer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionNotice_Student_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "ppl",
                        principalTable: "Student",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionNotice_PayerId",
                schema: "ppl",
                table: "CollectionNotice",
                column: "PayerId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionNotice_School_Student_Issued",
                schema: "ppl",
                table: "CollectionNotice",
                columns: new[] { "SchoolId", "StudentId", "IssuedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionNotice_SchoolId_NoticeNo",
                schema: "ppl",
                table: "CollectionNotice",
                columns: new[] { "SchoolId", "NoticeNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionNotice_StudentId",
                schema: "ppl",
                table: "CollectionNotice",
                column: "StudentId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollectionNotice",
                schema: "ppl");
        }
    }
}
