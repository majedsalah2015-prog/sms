using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    public partial class GlBatchLedgerLink : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PostedJournalNo",
                schema: "fin",
                table: "GlExportBatch",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalJournalNo",
                schema: "fin",
                table: "GlExportBatch",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PostedJournalNo",
                schema: "fin",
                table: "GlExportBatch");

            migrationBuilder.DropColumn(
                name: "ReversalJournalNo",
                schema: "fin",
                table: "GlExportBatch");
        }
    }
}
