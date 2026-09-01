using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// doc/Modules/21 §3 BR-PAY-002 — the school's own bank accounts and cash
    /// boxes, and the column on a receipt that says which one the money arrived
    /// in. Nullable, because receipts issued before the catalogue existed have
    /// no answer and are not going to acquire one.
    /// </summary>
    public partial class PaymentCollectionAccounts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CollectionAccountId",
                schema: "ppl",
                table: "Receipt",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CollectionAccount",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    BankLookupId = table.Column<int>(type: "int", nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    AccountNo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Iban = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    GlExportCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionAccount", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Receipt_CollectionAccount_IssuedAt",
                schema: "ppl",
                table: "Receipt",
                columns: new[] { "CollectionAccountId", "IssuedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionAccount_SchoolId_Code",
                schema: "ppl",
                table: "CollectionAccount",
                columns: new[] { "SchoolId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Receipt_CollectionAccount_CollectionAccountId",
                schema: "ppl",
                table: "Receipt",
                column: "CollectionAccountId",
                principalSchema: "ppl",
                principalTable: "CollectionAccount",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Receipt_CollectionAccount_CollectionAccountId",
                schema: "ppl",
                table: "Receipt");

            migrationBuilder.DropTable(
                name: "CollectionAccount",
                schema: "ppl");

            migrationBuilder.DropIndex(
                name: "IX_Receipt_CollectionAccount_IssuedAt",
                schema: "ppl",
                table: "Receipt");

            migrationBuilder.DropColumn(
                name: "CollectionAccountId",
                schema: "ppl",
                table: "Receipt");
        }
    }
}
