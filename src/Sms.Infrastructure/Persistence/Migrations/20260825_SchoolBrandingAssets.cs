using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    public partial class SchoolBrandingAssets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LogoAttachmentId",
                schema: "core",
                table: "School",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SealAttachmentId",
                schema: "core",
                table: "School",
                type: "int",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoAttachmentId",
                schema: "core",
                table: "School");

            migrationBuilder.DropColumn(
                name: "SealAttachmentId",
                schema: "core",
                table: "School");
        }
    }
}
