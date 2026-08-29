using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    public partial class PayrollAndSalaryAdvances : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollRun",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    PayrollRunNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    PeriodMonth = table.Column<int>(type: "int", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    TotalGross = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    TotalNet = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    LineCount = table.Column<int>(type: "int", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollRun", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalaryAdvance",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    AdvanceNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RequestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    InstallmentCount = table.Column<int>(type: "int", nullable: false),
                    FirstDeductionYear = table.Column<int>(type: "int", nullable: false),
                    FirstDeductionMonth = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    DecisionAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisbursedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisbursementMethod = table.Column<short>(type: "smallint", nullable: true),
                    DisbursementRefNo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    SettledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryAdvance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryAdvance_Employee_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "ppl",
                        principalTable: "Employee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollRunLine",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    PayrollRunId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    ContractId = table.Column<int>(type: "int", nullable: true),
                    BasicSalary = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Allowances = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    AdditionsTotal = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    DeductionsTotal = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    AdvanceDeduction = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    GrossPay = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    NetPay = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollRunLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollRunLine_Contract_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "ppl",
                        principalTable: "Contract",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollRunLine_Employee_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "ppl",
                        principalTable: "Employee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollRunLine_PayrollRun_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalSchema: "ppl",
                        principalTable: "PayrollRun",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollLineAdjustment",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    PayrollRunLineId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollLineAdjustment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollLineAdjustment_PayrollRunLine_PayrollRunLineId",
                        column: x => x.PayrollRunLineId,
                        principalSchema: "ppl",
                        principalTable: "PayrollRunLine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalaryAdvanceInstallment",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    SalaryAdvanceId = table.Column<int>(type: "int", nullable: false),
                    SequenceNo = table.Column<int>(type: "int", nullable: false),
                    DueYear = table.Column<int>(type: "int", nullable: false),
                    DueMonth = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    PayrollRunLineId = table.Column<int>(type: "int", nullable: true),
                    DeductedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WaiverNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryAdvanceInstallment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryAdvanceInstallment_PayrollRunLine_PayrollRunLineId",
                        column: x => x.PayrollRunLineId,
                        principalSchema: "ppl",
                        principalTable: "PayrollRunLine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalaryAdvanceInstallment_SalaryAdvance_SalaryAdvanceId",
                        column: x => x.SalaryAdvanceId,
                        principalSchema: "ppl",
                        principalTable: "SalaryAdvance",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLineAdjustment_PayrollRunLineId",
                schema: "ppl",
                table: "PayrollLineAdjustment",
                column: "PayrollRunLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRun_SchoolId_PayrollRunNo",
                schema: "ppl",
                table: "PayrollRun",
                columns: new[] { "SchoolId", "PayrollRunNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRun_SchoolId_PeriodYear_PeriodMonth",
                schema: "ppl",
                table: "PayrollRun",
                columns: new[] { "SchoolId", "PeriodYear", "PeriodMonth" },
                unique: true,
                filter: "[Status] <> 4");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRunLine_ContractId",
                schema: "ppl",
                table: "PayrollRunLine",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRunLine_Employee",
                schema: "ppl",
                table: "PayrollRunLine",
                columns: new[] { "SchoolId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRunLine_EmployeeId",
                schema: "ppl",
                table: "PayrollRunLine",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRunLine_PayrollRunId_EmployeeId",
                schema: "ppl",
                table: "PayrollRunLine",
                columns: new[] { "PayrollRunId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryAdvance_Employee_Status",
                schema: "ppl",
                table: "SalaryAdvance",
                columns: new[] { "SchoolId", "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryAdvance_EmployeeId",
                schema: "ppl",
                table: "SalaryAdvance",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryAdvance_SchoolId_AdvanceNo",
                schema: "ppl",
                table: "SalaryAdvance",
                columns: new[] { "SchoolId", "AdvanceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryAdvanceInstallment_Due",
                schema: "ppl",
                table: "SalaryAdvanceInstallment",
                columns: new[] { "SchoolId", "DueYear", "DueMonth", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryAdvanceInstallment_PayrollRunLineId",
                schema: "ppl",
                table: "SalaryAdvanceInstallment",
                column: "PayrollRunLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryAdvanceInstallment_SalaryAdvanceId_SequenceNo",
                schema: "ppl",
                table: "SalaryAdvanceInstallment",
                columns: new[] { "SalaryAdvanceId", "SequenceNo" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollLineAdjustment",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "SalaryAdvanceInstallment",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "PayrollRunLine",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "SalaryAdvance",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "PayrollRun",
                schema: "ppl");
        }
    }
}
