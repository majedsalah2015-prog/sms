using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Common;
using Sms.Domain.Employees;
using Sms.Domain.Lookups;
using Sms.Domain.Numbering;
using Sms.Domain.Payroll;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Payroll;
using Sms.Infrastructure.Persistence;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// الكشوفات — the four statements, over a real Sqlite-backed AppDbContext (owner request,
    /// 2026-08-28). Every total here is one the register or the statement prints, so a decimal
    /// aggregate that only works on SQL Server would fail this file rather than a school.
    /// <para>
    /// Untagged by <c>[BusinessRule]</c> for the reason given in <c>PayrollAdminTests</c>.
    /// </para>
    /// </summary>
    public sealed class PayrollStatementsTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; } = 7;
        }

        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;

            public int AcademicYearId => 2027;
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();

        public PayrollStatementsTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            db.NumberingSeries.Add(new NumberingSeries
            {
                Code = "PAY", EntityName = "PayrollRun", FormatTemplate = "PAY-{SEQ:4}",
                ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Normal,
                EffectiveFromUtc = _clock.UtcNow, IsActive = true,
            });
            db.NumberingSeries.Add(new NumberingSeries
            {
                Code = "ADV", EntityName = "SalaryAdvance", FormatTemplate = "ADV-{SEQ:5}",
                ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Normal,
                EffectiveFromUtc = _clock.UtcNow, IsActive = true,
            });
            db.SaveChanges();
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private PayrollAdmin CreateAdmin(AppDbContext db) =>
            new(db, new NumberIssuer(db, _tenant, _tenant, _clock), _clock);

        private SalaryAdvanceAdmin CreateAdvances(AppDbContext db) =>
            new(db, new NumberIssuer(db, _tenant, _tenant, _clock), _clock);

        private int SeedEmployee(string suffix, decimal basic, decimal? allowances = null, string? bankAccountNo = null)
        {
            using var db = CreateContext();
            var employee = new Employee
            {
                EmployeeNo = "EMP-" + suffix,
                FirstNameAr = "أحمد", FatherNameAr = "محمد", GrandfatherNameAr = "علي", FamilyNameAr = "سالم",
                FirstNameEn = "Ahmad", FatherNameEn = "Mohammad", GrandfatherNameEn = "Ali", FamilyNameEn = "Salem",
                Gender = Gender.Male, DateOfBirth = new DateTime(1990, 1, 1), NationalityLookupId = 1,
                Status = EmployeeStatus.Active,
                BankName = bankAccountNo == null ? null : "بنك فلسطين",
                BankAccountNo = bankAccountNo,
            };
            db.Employees.Add(employee);
            db.SaveChanges();

            db.Contracts.Add(new Contract
            {
                EmployeeId = employee.Id,
                Type = ContractType.FullTime,
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2027, 12, 31),
                SalaryBasic = basic,
                SalaryAllowances = allowances,
                Status = ContractStatus.Active,
            });
            db.SaveChanges();
            return employee.Id;
        }

        // --- the monthly register ---------------------------------------------

        [Fact]
        public async Task The_register_lists_every_employee_and_totals_each_column()
        {
            SeedEmployee("0001", 3000m, 500m);
            SeedEmployee("0002", 2000m);
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));
            await admin.GenerateLinesAsync(run.Id);

            var first = await db.PayrollRunLines.OrderBy(l => l.Id).FirstAsync();
            await admin.AddAdjustmentAsync(first.Id, PayrollAdjustmentKind.Addition, "ساعات إضافية", 250m);
            await admin.AddAdjustmentAsync(first.Id, PayrollAdjustmentKind.Deduction, "خصم تأخير", 100m);

            var register = await new PayrollStatements(db).BuildRegisterAsync(run.Id);

            Assert.Equal("PAY-0001", register.RunNo);
            Assert.Equal(2, register.Lines.Count);
            Assert.Equal(5000m, register.TotalBasic);
            Assert.Equal(500m, register.TotalAllowances);
            Assert.Equal(250m, register.TotalAdditions);
            Assert.Equal(100m, register.TotalDeductions);
            Assert.Equal(0m, register.TotalAdvanceDeduction);
            Assert.Equal(5750m, register.TotalGross);
            Assert.Equal(5650m, register.TotalNet);

            // Both names travel, because the same register is printed in both directions.
            var row = register.Lines.First();
            Assert.Equal("أحمد محمد علي سالم", row.Employee.NameAr);
            Assert.Equal("Ahmad Mohammad Ali Salem", row.Employee.NameEn);
        }

        [Fact]
        public async Task The_register_of_an_empty_run_totals_zero_rather_than_throwing()
        {
            using var db = CreateContext();
            var run = await CreateAdmin(db).OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));

            var register = await new PayrollStatements(db).BuildRegisterAsync(run.Id);

            Assert.Empty(register.Lines);
            Assert.Equal(0m, register.TotalNet);
        }

        // --- the payslip ------------------------------------------------------

        [Fact]
        public async Task The_payslip_breaks_out_every_adjustment_and_the_advance_instalment()
        {
            var employeeId = SeedEmployee("0001", 3000m, 500m, bankAccountNo: "PS00-1234");
            using var db = CreateContext();

            var advances = CreateAdvances(db);
            var advance = await advances.RequestAsync(employeeId, new DateTime(2026, 8, 20), 1200m, 4, 2026, 9);
            await advances.ApproveAsync(advance.Id);
            await advances.DisburseAsync(advance.Id, new DateTime(2026, 8, 25), AdvanceDisbursementMethod.Cash);

            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));
            await admin.GenerateLinesAsync(run.Id);
            var line = await db.PayrollRunLines.SingleAsync(l => l.PayrollRunId == run.Id);
            await admin.AddAdjustmentAsync(line.Id, PayrollAdjustmentKind.Addition, "ساعات إضافية", 250m);
            await admin.AddAdjustmentAsync(line.Id, PayrollAdjustmentKind.Deduction, "خصم تأخير", 100m);

            var payslip = await new PayrollStatements(db).BuildPayslipAsync(line.Id);

            Assert.Equal("PAY-0001", payslip.RunNo);
            Assert.Equal(3000m, payslip.BasicSalary);
            Assert.Equal(500m, payslip.Allowances);
            Assert.Equal(3750m, payslip.GrossPay);
            Assert.Equal(300m, payslip.AdvanceDeduction);
            Assert.Equal(3350m, payslip.NetPay);
            Assert.Equal("PS00-1234", payslip.BankAccountNo);

            Assert.Equal(2, payslip.Adjustments.Count);
            Assert.Contains(payslip.Adjustments, a => a.Kind == PayrollAdjustmentKind.Addition && a.Amount == 250m);

            var instalment = Assert.Single(payslip.AdvanceInstallments);
            Assert.Equal(advance.AdvanceNo, instalment.AdvanceNo);
            Assert.Equal(1, instalment.SequenceNo);
            Assert.Equal(4, instalment.InstallmentCount);
            Assert.Equal(300m, instalment.Amount);

            // 1,200 lent, nothing recovered yet, 300 about to be — 900 will remain.
            Assert.Equal(900m, instalment.RemainingAfterThis);
        }

        [Fact]
        public async Task A_paid_payslip_still_names_the_instalment_it_recovered()
        {
            var employeeId = SeedEmployee("0001", 3000m);
            using var db = CreateContext();

            var advances = CreateAdvances(db);
            var advance = await advances.RequestAsync(employeeId, new DateTime(2026, 8, 20), 1200m, 4, 2026, 9);
            await advances.ApproveAsync(advance.Id);
            await advances.DisburseAsync(advance.Id, new DateTime(2026, 8, 25), AdvanceDisbursementMethod.Cash);

            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));
            await admin.GenerateLinesAsync(run.Id);
            await admin.ApproveRunAsync(run.Id);
            await admin.MarkRunPaidAsync(run.Id, new DateTime(2026, 9, 30));

            var line = await db.PayrollRunLines.SingleAsync(l => l.PayrollRunId == run.Id);
            var payslip = await new PayrollStatements(db).BuildPayslipAsync(line.Id);

            var instalment = Assert.Single(payslip.AdvanceInstallments);
            Assert.Equal(300m, instalment.Amount);
            Assert.Equal(900m, instalment.RemainingAfterThis);
            Assert.Equal(PayrollRunStatus.Paid, payslip.RunStatus);
        }

        // --- the bank transfer list -------------------------------------------

        [Fact]
        public async Task The_bank_list_counts_the_staff_who_have_nowhere_for_the_money_to_go()
        {
            SeedEmployee("0001", 3000m, bankAccountNo: "PS00-1111");
            SeedEmployee("0002", 2000m);
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));
            await admin.GenerateLinesAsync(run.Id);

            var list = await new PayrollStatements(db).BuildBankTransferListAsync(run.Id);

            Assert.Equal(2, list.Rows.Count);
            Assert.Equal(5000m, list.TotalNet);
            Assert.Equal(1, list.RowsWithoutDestination);

            var banked = list.Rows.Single(r => r.BankAccountNo == "PS00-1111");
            Assert.True(banked.HasDestination);
            Assert.False(list.Rows.Single(r => r.BankAccountNo == null).HasDestination);
        }

        [Fact]
        public async Task A_mobile_wallet_counts_as_a_destination()
        {
            var employeeId = SeedEmployee("0001", 3000m);
            using var db = CreateContext();
            var employee = await db.Employees.SingleAsync(e => e.Id == employeeId);
            employee.JawwalPayWalletNo = "0599123456";

            // A destination for money is T1 with a mandatory reason (ppl.Employee) — the rule holds
            // for a test writing the column directly, which is the point of it being central.
            _audit.Reason = "تسجيل محفظة الموظف";
            await db.SaveChangesAsync();

            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));
            await admin.GenerateLinesAsync(run.Id);

            var list = await new PayrollStatements(db).BuildBankTransferListAsync(run.Id);

            Assert.Equal(0, list.RowsWithoutDestination);
            Assert.True(list.Rows.Single().HasDestination);
        }

        [Fact]
        public async Task The_bank_transfer_list_names_the_catalogued_bank_in_both_languages_and_falls_back_to_the_typed_one()
        {
            var catalogued = SeedEmployee("0001", 3000m, bankAccountNo: "PS00-1111");
            SeedEmployee("0002", 2000m, bankAccountNo: "PS00-2222");

            using var db = CreateContext();

            // A "Bank" catalogue with one value in it, and one of the two employees pointing at it.
            // The other keeps the free text SeedEmployee wrote, which is the register-entered-before-
            // the-picker case the fallback exists for.
            var category = new LookupCategory { Code = "Bank", Tier = LookupCategoryTier.ProductSeeded, Name = new LocalizedName("البنك", "Bank") };
            db.LookupCategories.Add(category);
            await db.SaveChangesAsync();

            var value = new LookupValue { LookupCategoryId = category.Id, Code = "BOP", Name = new LocalizedName("بنك القدس", "Bank of Jerusalem"), SortOrder = 1 };
            db.LookupValues.Add(value);
            await db.SaveChangesAsync();

            var employee = await db.Employees.SingleAsync(e => e.Id == catalogued);
            employee.BankLookupId = value.Id;
            employee.BankName = null;
            _audit.Reason = "catalogued";
            await db.SaveChangesAsync();

            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));
            await admin.GenerateLinesAsync(run.Id);

            var list = await new PayrollStatements(db).BuildBankTransferListAsync(run.Id);

            var picked = list.Rows.Single(r => r.BankAccountNo == "PS00-1111");
            Assert.Equal("بنك القدس", picked.BankNameAr);
            Assert.Equal("Bank of Jerusalem", picked.BankNameEn);

            // Typed once, so it reads the same in both — the alternative would be inventing a
            // translation the school never wrote.
            var typed = list.Rows.Single(r => r.BankAccountNo == "PS00-2222");
            Assert.Equal("بنك فلسطين", typed.BankNameAr);
            Assert.Equal("بنك فلسطين", typed.BankNameEn);
        }

        [Fact]
        public async Task Retiring_a_bank_does_not_blank_it_off_the_payslips_of_everyone_paid_into_it()
        {
            var employeeId = SeedEmployee("0001", 3000m, bankAccountNo: "PS00-1111");
            using var db = CreateContext();

            var category = new LookupCategory { Code = "Bank", Tier = LookupCategoryTier.ProductSeeded, Name = new LocalizedName("البنك", "Bank") };
            db.LookupCategories.Add(category);
            await db.SaveChangesAsync();

            var value = new LookupValue { LookupCategoryId = category.Id, Code = "BOP", Name = new LocalizedName("بنك القدس", "Bank of Jerusalem"), SortOrder = 1 };
            db.LookupValues.Add(value);
            await db.SaveChangesAsync();

            var employee = await db.Employees.SingleAsync(e => e.Id == employeeId);
            employee.BankLookupId = value.Id;
            _audit.Reason = "catalogued";
            await db.SaveChangesAsync();

            // BR-SET-002: the bank is retired, never deleted. The statement reads it past the
            // soft-active filter for exactly this reason — a school tidying its catalogue must not
            // silently unname the account last month's salary went to.
            value.IsActive = false;
            await db.SaveChangesAsync();

            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));
            await admin.GenerateLinesAsync(run.Id);
            var line = await db.PayrollRunLines.SingleAsync(l => l.PayrollRunId == run.Id);

            var payslip = await new PayrollStatements(db).BuildPayslipAsync(line.Id);

            Assert.Equal("بنك القدس", payslip.BankNameAr);
            Assert.Equal("Bank of Jerusalem", payslip.BankNameEn);
        }

        // --- the advances statements ------------------------------------------

        [Fact]
        public async Task The_advance_statement_separates_what_was_recovered_from_what_is_owed()
        {
            var employeeId = SeedEmployee("0001", 3000m);
            using var db = CreateContext();

            var advances = CreateAdvances(db);
            var advance = await advances.RequestAsync(employeeId, new DateTime(2026, 8, 20), 1200m, 4, 2026, 9);
            await advances.ApproveAsync(advance.Id);
            await advances.DisburseAsync(advance.Id, new DateTime(2026, 8, 25), AdvanceDisbursementMethod.Cash);

            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));
            await admin.GenerateLinesAsync(run.Id);
            await admin.ApproveRunAsync(run.Id);
            await admin.MarkRunPaidAsync(run.Id, new DateTime(2026, 9, 30));

            var statement = await new PayrollStatements(db).BuildAdvanceStatementAsync(employeeId);

            Assert.Equal(1200m, statement.TotalAdvanced);
            Assert.Equal(300m, statement.TotalDeducted);
            Assert.Equal(0m, statement.TotalWaived);
            Assert.Equal(900m, statement.TotalOutstanding);

            var view = Assert.Single(statement.Advances);
            Assert.Equal(4, view.Installments.Count);

            // The recovered instalment names the run that took it — without that link "deducted"
            // is a claim rather than a fact.
            var recovered = view.Installments.Single(i => i.Status == SalaryAdvanceInstallmentStatus.Deducted);
            Assert.Equal("PAY-0001", recovered.PayrollRunNo);
        }

        [Fact]
        public async Task A_request_nobody_disbursed_owes_nothing_on_the_statement()
        {
            var employeeId = SeedEmployee("0001", 3000m);
            using var db = CreateContext();
            var advances = CreateAdvances(db);
            var advance = await advances.RequestAsync(employeeId, new DateTime(2026, 8, 20), 1200m, 4, 2026, 9);
            await advances.ApproveAsync(advance.Id);

            var statement = await new PayrollStatements(db).BuildAdvanceStatementAsync(employeeId);

            Assert.Equal(0m, statement.TotalAdvanced);
            Assert.Equal(0m, statement.TotalOutstanding);
            Assert.Equal(SalaryAdvanceStatus.Approved, Assert.Single(statement.Advances).Status);
        }

        [Fact]
        public async Task A_waived_instalment_reduces_the_balance_without_being_counted_as_recovered()
        {
            var employeeId = SeedEmployee("0001", 3000m);
            using var db = CreateContext();
            var advances = CreateAdvances(db);
            var advance = await advances.RequestAsync(employeeId, new DateTime(2026, 8, 20), 1200m, 4, 2026, 9);
            await advances.ApproveAsync(advance.Id);
            await advances.DisburseAsync(advance.Id, new DateTime(2026, 8, 25), AdvanceDisbursementMethod.Cash);

            var first = await db.SalaryAdvanceInstallments.OrderBy(i => i.SequenceNo).FirstAsync();
            await advances.WaiveInstallmentAsync(first.Id, "ظرف صحي");

            var statement = await new PayrollStatements(db).BuildAdvanceStatementAsync(employeeId);

            Assert.Equal(0m, statement.TotalDeducted);
            Assert.Equal(300m, statement.TotalWaived);
            Assert.Equal(900m, statement.TotalOutstanding);
        }

        [Fact]
        public async Task The_school_wide_report_shows_only_advances_still_running()
        {
            var owing = SeedEmployee("0001", 3000m);
            var settled = SeedEmployee("0002", 3000m);
            using var db = CreateContext();
            var advances = CreateAdvances(db);

            var live = await advances.RequestAsync(owing, new DateTime(2026, 8, 20), 1200m, 4, 2026, 9);
            await advances.ApproveAsync(live.Id);
            await advances.DisburseAsync(live.Id, new DateTime(2026, 8, 25), AdvanceDisbursementMethod.Cash);

            var closed = await advances.RequestAsync(settled, new DateTime(2026, 8, 20), 400m, 2, 2026, 9);
            await advances.ApproveAsync(closed.Id);
            await advances.DisburseAsync(closed.Id, new DateTime(2026, 8, 25), AdvanceDisbursementMethod.Cash);
            await advances.WaiveRemainingAsync(closed.Id, "إعفاء");

            var report = await new PayrollStatements(db).BuildOutstandingAdvancesAsync();

            var row = Assert.Single(report.Rows);
            Assert.Equal(live.AdvanceNo, row.AdvanceNo);
            Assert.Equal(1200m, row.Amount);
            Assert.Equal(1200m, row.Outstanding);
            Assert.Equal(4, row.RemainingInstallments);
            Assert.Equal(2026, row.NextDueYear);
            Assert.Equal(9, row.NextDueMonth);
            Assert.Equal(1200m, report.TotalOutstanding);
        }
    }
}
