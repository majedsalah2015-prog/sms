using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    public partial class MessagingWhatsAppChannel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountIdentifier",
                schema: "msg",
                table: "Provider",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiBaseUrl",
                schema: "msg",
                table: "Provider",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                schema: "msg",
                table: "Provider",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // 1, not EF's 0: the entity's own default is 1, and a column whose backfilled
            // value differs from what new rows get is a difference nothing explains later.
            migrationBuilder.AddColumn<int>(
                name: "FailoverOrder",
                schema: "msg",
                table: "Provider",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "LastTestDetail",
                schema: "msg",
                table: "Provider",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            // ProviderTestOutcome starts at 1 (NeverTested) per the SMALLINT convention —
            // EF's 0 is not a value the enum defines, and a row carrying it would render as
            // a blank cell in the console rather than as "never tested".
            migrationBuilder.AddColumn<short>(
                name: "LastTestOutcome",
                schema: "msg",
                table: "Provider",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastTestedAtUtc",
                schema: "msg",
                table: "Provider",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecretCipher",
                schema: "msg",
                table: "Provider",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenderId",
                schema: "msg",
                table: "Provider",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TemplateVersionId",
                schema: "msg",
                table: "Delivery",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "AnnouncementId",
                schema: "msg",
                table: "Delivery",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientAddress",
                schema: "msg",
                table: "Delivery",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AudienceTargetId",
                schema: "msg",
                table: "Announcement",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChannelMask",
                schema: "msg",
                table: "Announcement",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Every existing row was just backfilled with FailoverOrder = 1, so a school that
            // already had two gateways registered on one channel would break the unique index
            // below the moment it is created. Rank them by id first — arbitrary but stable,
            // and the console can reorder them afterwards. No-op on the empty tables this
            // ships to, which is exactly why it is cheap to be right about.
            migrationBuilder.Sql(@"
WITH ranked AS (
    SELECT Id, ROW_NUMBER() OVER (PARTITION BY SchoolId, Channel ORDER BY Id) AS Rank
    FROM msg.Provider
)
UPDATE p SET p.FailoverOrder = r.Rank
FROM msg.Provider p INNER JOIN ranked r ON r.Id = p.Id;");

            migrationBuilder.CreateIndex(
                name: "UX_Provider_Channel_Failover",
                schema: "msg",
                table: "Provider",
                columns: new[] { "SchoolId", "Channel", "FailoverOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Delivery_AnnouncementId",
                schema: "msg",
                table: "Delivery",
                column: "AnnouncementId");

            migrationBuilder.AddForeignKey(
                name: "FK_Delivery_Announcement_AnnouncementId",
                schema: "msg",
                table: "Delivery",
                column: "AnnouncementId",
                principalSchema: "msg",
                principalTable: "Announcement",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Delivery_Announcement_AnnouncementId",
                schema: "msg",
                table: "Delivery");

            migrationBuilder.DropIndex(
                name: "UX_Provider_Channel_Failover",
                schema: "msg",
                table: "Provider");

            migrationBuilder.DropIndex(
                name: "IX_Delivery_AnnouncementId",
                schema: "msg",
                table: "Delivery");

            migrationBuilder.DropColumn(
                name: "AccountIdentifier",
                schema: "msg",
                table: "Provider");

            migrationBuilder.DropColumn(
                name: "ApiBaseUrl",
                schema: "msg",
                table: "Provider");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                schema: "msg",
                table: "Provider");

            migrationBuilder.DropColumn(
                name: "FailoverOrder",
                schema: "msg",
                table: "Provider");

            migrationBuilder.DropColumn(
                name: "LastTestDetail",
                schema: "msg",
                table: "Provider");

            migrationBuilder.DropColumn(
                name: "LastTestOutcome",
                schema: "msg",
                table: "Provider");

            migrationBuilder.DropColumn(
                name: "LastTestedAtUtc",
                schema: "msg",
                table: "Provider");

            migrationBuilder.DropColumn(
                name: "SecretCipher",
                schema: "msg",
                table: "Provider");

            migrationBuilder.DropColumn(
                name: "SenderId",
                schema: "msg",
                table: "Provider");

            migrationBuilder.DropColumn(
                name: "AnnouncementId",
                schema: "msg",
                table: "Delivery");

            migrationBuilder.DropColumn(
                name: "RecipientAddress",
                schema: "msg",
                table: "Delivery");

            migrationBuilder.DropColumn(
                name: "AudienceTargetId",
                schema: "msg",
                table: "Announcement");

            migrationBuilder.DropColumn(
                name: "ChannelMask",
                schema: "msg",
                table: "Announcement");

            migrationBuilder.AlterColumn<int>(
                name: "TemplateVersionId",
                schema: "msg",
                table: "Delivery",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
