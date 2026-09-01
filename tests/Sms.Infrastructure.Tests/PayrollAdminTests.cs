using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Common;
using Sms.Domain.Employees;
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
    /// مسير الرواتب and its four statements over a real Sqlite-backed AppDbContext, including the
    /// real INumberIssuer. Owner request, 2026-08-28.
    /// <para>
    /// Deliberately untagged by <c>[BusinessRule]</c>: doc/Modules/12 §2 scopes payroll calculation
    /// out and BR-EMP-007 says the SMS never computes a net salary, so these rules have no numbered
    /// id to cite. See <c>Sms.Domain.Payroll.PayrollRun</c> for the standing deviation.
    /// </para>
    /// </summary>
    public sealed class PayrollAdminTests : IDisposable
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

        public PayrollAdminTests()
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

        /// <summary>An active employee holding an active contract over September 2026.</summary>
        private int SeedPayableEmployee(
            string suffix, decimal basic, decimal? allowances = null,
            EmployeeStatus employeeStatus = EmployeeStatus.Active,
            ContractStatus contractStatus = ContractStatus.Active,
            DateTime? start = null, DateTime? end = null,
            string? bankAccountNo = null)
        {
            using var db = CreateContext();
            var employee = new Employee
            {
                EmployeeNo = "EMP-" + suffix,
                FirstNameAr = "موظف", FatherNameAr = "أب", GrandfatherNameAr = "جد", FamilyNameAr = "عائلة",
                FirstNameEn = "Employee", FatherNameEn = "Father", GrandfatherNameEn = "Grand", FamilyNameEn = "Family",
                Gender = Gender.Male, DateOfBirth = new DateTime(1990, 1, 1), NationalityLookupId = 1,
                Status = employeeStatus,
                BankName = bankAccountNo == null ? null : "بنك فلسطين",
                BankAccountNo = bankAccountNo,
            };
            db.Employees.Add(employee);
            db.SaveChanges();

            db.Contracts.Add(new Contract
            {
                EmployeeId = employee.Id,
                Type = ContractType.FullTime,
                StartDate = start ?? new DateTime(2026, 1, 1),
                EndDate = end ?? new DateTime(2027, 12, 31),
                SalaryBasic = basic,
                SalaryAllowances = allowances,
                Status = contractStatus,
            });
            db.SaveChanges();
            return employee.Id;
        }

        // --- opening a month --------------------------------------------------

        [Fact]
        public async Task Opening_a_run_issues_a_number_and_starts_as_a_draft()
        {
            using var db = CreateContext();

            var run = await CreateAdmin(db).OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));

            Assert.Equal("PAY-0001", run.PayrollRunNo);
            Assert.Equal(PayrollRunStatus.Draft, run.Status);
            Assert.Equal(0, run.LineCount);
            Assert.Equal(1, run.SchoolId);
        }

        [Fact]
        public async Task A_second_run_for_the_same_month_is_refused()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));

            var refusal = await Assert.ThrowsAsync<DuplicatePayrollRunException>(
                () => admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 30)));

            Assert.Equal("PAY-0001", refusal.ExistingRunNo);
        }

        [Fact]
        public async Task Cancelling_a_run_frees_its_month_for_a_correct_one()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var first = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));
            await admin.CancelRunAsync(first.Id, "فُتح بالخطأ");

            var second = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 30));

            Assert.Equal("PAY-0002", second.PayrollRunNo);
            Assert.Equal(PayrollRunStatus.Draft, second.Status);
        }

        [Fact]
        public async Task A_month_that_is_not_a_month_is_refused()
        {
            using var db = CreateContext();

            await Assert.ThrowsAsync<InvalidPayrollPeriodException>(
                () => CreateAdmin(db).OpenRunAsync(2026, 13, new DateTime(2026, 9, 28)));
        }

        // --- generating the lines --------------------------------------------

        [Fact]
        public async Task Generation_snapshots_pay_from_the_active_contract()
        {
            SeedPayableEmployee("0001", 3000m, 500m);
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));

            await admin.GenerateLinesAsync(run.Id);

            var line = await db.PayrollRunLines.SingleAsync(l => l.PayrollRunId == run.Id);
            Assert.Equal(3000m, line.BasicSalary);
            Assert.Equal(500m, line.Allowances);
            Assert.Equal(3500m, line.GrossPay);
            Assert.Equal(3500m, line.NetPay);
            Assert.Equal(1, line.SchoolId);

            var reloaded = await db.PayrollRuns.SingleAsync(r => r.Id == run.Id);
            Assert.Equal(1, reloaded.LineCount);
            Assert.Equal(3500m, reloaded.TotalGross);
            Assert.Equal(3500m, reloaded.TotalNet);
        }

        [Fact]
        public async Task Generation_skips_inactive_employees_draft_contracts_and_contracts_outside_the_month()
        {
            SeedPayableEmployee("0001", 3000m);
            SeedPayableEmployee("0002", 3000m, employeeStatus: EmployeeStatus.Terminated);
            SeedPayableEmployee("0003", 3000m, contractStatus: ContractStatus.Draft);
            SeedPayableEmployee("0004", 3000m, start: new DateTime(2026, 10, 1), end: new DateTime(2027, 6, 30));
            SeedPayableEmployee("0005", 3000m, start: new DateTime(2025, 1, 1), end: new DateTime(2026, 8, 31));

            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));

            await admin.GenerateLinesAsync(run.Id);

            var line = await db.PayrollRunLines.SingleAsync(l => l.PayrollRunId == run.Id);
            var employee = await db.Employees.SingleAsync(e => e.Id == line.EmployeeId);
            Assert.Equal("EMP-0001", employee.EmployeeNo);
        }

        [Fact]
        public async Task A_contract_that_starts_mid_month_is_still_paid_in_full()
        {
            // Proration is not a rule this product has been given; inventing one would be a
            // substitution rather than an implementation.
            SeedPayableEmployee("0001", 3000m, start: new DateTime(2026, 9, 15), end: new DateTime(2027, 6, 30));
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));

            await admin.GenerateLinesAsync(run.Id);

            var line = await db.PayrollRunLines.SingleAsync(l => l.PayrollRunId == run.Id);
            Assert.Equal(3000m, line.NetPay);
        }

        [Fact]
        public async Task Generation_attaches_the_advance_instalment_falling_due_that_month()
        {
            var employeeId = SeedPayableEmployee("0001", 3000m);
            using var db = CreateContext();
            var advances = CreateAdvances(db);
            var advance = await advances.RequestAsync(employeeId, new DateTime(2026, 8, 20), 1200m, 4, 2026, 9);
            await advances.ApproveAsync(advance.Id);
            await advances.DisburseAsync(advance.Id, new DateTime(2026, 8, 25), AdvanceDisbursementMethod.Cash);

            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));
            await admin.GenerateLinesAsync(run.Id);

            var line = await db.PayrollRunLines.SingleAsync(l => l.PayrollRunId == run.Id);
            Assert.Equal(300m, line.AdvanceDeduction);
            Assert.Equal(3000m, line.GrossPay);
            Assert.Equal(2700m, line.NetPay);

            // Still only a plan — the instalment is consumed when the run is paid, not when it is
            // generated.
            var installment = await db.SalaryAdvanceInstallments
                .OrderBy(i => i.SequenceNo).FirstAsync();
            Assert.Equal(SalaryAdvanceInstallmentStatus.Scheduled, installment.Status);
            Assert.Null(installment.PayrollRunLineId);
        }

        [Fact]
        public async Task An_advance_still_awaiting_disbursement_is_not_deducted()
        {
            var employeeId = SeedPayableEmployee("0001", 3000m);
            using var db = CreateContext();
            var advances = CreateAdvances(db);
            var advance = await advances.RequestAsync(employeeId, new DateTime(2026, 8, 20), 1200m, 4, 2026, 9);
            await advances.ApproveAsync(advance.Id);

            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));
            await admin.GenerateLinesAsync(run.Id);

            var line = await db.PayrollRunLines.SingleAsync(l => l.PayrollRunId == run.Id);
            Assert.Equal(0m, line.AdvanceDeduction);
        }

        [Fact]
        public async Task Regenerating_replaces_the_lines_and_drops_their_adjustments()
        {
            SeedPayableEmployee("0001", 3000m);
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));
            await admin.GenerateLinesAsync(run.Id);

            var line = await db.PayrollRunLines.SingleAsync(l => l.PayrollRunId == run.Id);
            await admin.AddAdjustmentAsync(line.Id, PayrollAdjustmentKind.Addition, "ساعات إضافية", 200m);

            await admin.GenerateLinesAsync(run.Id);

            Assert.Empty(await db.PayrollLineAdjustments.ToListAsync());
            var rebuilt = await db.PayrollRunLines.SingleAsync(l => l.PayrollRunId == run.Id);
            Assert.Equal(0m, rebuilt.AdditionsTotal);
            Assert.Equal(3000m, rebuilt.NetPay);
        }

        // --- adjustments ------------------------------------------------------

        [Fact]
        public async Task Adjustments_restate_the_line_and_the_run_totals()
        {
            SeedPayableEmployee("0001", 3000m);
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));
            await admin.GenerateLinesAsync(run.Id);
            var line = await db.PayrollRunLines.SingleAsync(l => l.PayrollRunId == run.Id);

            await admin.AddAdjustmentAsync(line.Id, PayrollAdjustmentKind.Addition, "ساعات إضافية", 200m);
            var deduction = await admin.AddAdjustmentAsync(line.Id, PayrollAdjustmentKind.Deduction, "خصم تأخير", 50m);

            var afterBoth = await db.PayrollRunLines.SingleAsync(l => l.Id == line.Id);
            Assert.Equal(200m, afterBoth.AdditionsTotal);
            Assert.Equal(50m, afterBoth.DeductionsTotal);
            Assert.Equal(3200m, afterBoth.GrossPay);
            Assert.Equal(3150m, afterBoth.NetPay);

            var runAfterBoth = await db.PayrollRuns.SingleAsync(r => r.Id == run.Id);
            Assert.Equal(3200m, runAfterBoth.TotalGross);
            Assert.Equal(50m, runAfterBoth.TotalDeductions);
            Assert.Equal(3150m, runAfterBoth.TotalNet);

            await admin.RemoveAdjustmentAsync(deduction.Id);

            var afterRemoval = await db.PayrollRunLines.SingleAsync(l => l.Id == line.Id);
            Assert.Equal(0m, afterRemoval.DeductionsTotal);
            Assert.Equal(3200m, afterRemoval.NetPay);
        }

        [Fact]
        public async Task An_adjustment_for_nothing_is_refused()
        {
            SeedPayableEmployee("0001", 3000m);
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));
            await admin.GenerateLinesAsync(run.Id);
            var line = await db.PayrollRunLines.SingleAsync(l => l.PayrollRunId == run.Id);

            await Assert.ThrowsAsync<NegativePayComponentException>(
                () => admin.AddAdjustmentAsync(line.Id, PayrollAdjustmentKind.Deduction, "خصم", 0m));
        }

        [Fact]
        public async Task Adding_the_same_employee_twice_is_refused()
        {
            var employeeId = SeedPayableEmployee("0001", 3000m);
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));
            await admin.GenerateLinesAsync(run.Id);

            await Assert.ThrowsAsync<DuplicatePayrollLineException>(() => admin.AddLineAsync(run.Id, employeeId));
        }

        [Fact]
        public async Task An_employee_with_no_contract_can_be_added_only_with_pay_figures()
        {
            using var db = CreateContext();
            var employee = new Employee
            {
                EmployeeNo = "EMP-9999",
                FirstNameAr = "موظف", FatherNameAr = "أب", GrandfatherNameAr = "جد", FamilyNameAr = "عائلة",
                FirstNameEn = "Employee", FatherNameEn = "Father", GrandfatherNameEn = "Grand", FamilyNameEn = "Family",
                Gender = Gender.Male, DateOfBirth = new DateTime(1990, 1, 1), NationalityLookupId = 1,
                Status = EmployeeStatus.Active,
            };
            db.Employees.Add(employee);
            await db.SaveChangesAsync();

            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));

            await Assert.ThrowsAsync<NoActiveContractException>(() => admin.AddLineAsync(run.Id, employee.Id));

            var line = await admin.AddLineAsync(run.Id, employee.Id, basicSalary: 1500m, allowances: 100m);
            Assert.Equal(1600m, line.NetPay);
            Assert.Null(line.ContractId);
        }

        [Fact]
        public async Task Removing_a_line_takes_its_adjustments_and_restates_the_run()
        {
            var leaving = SeedPayableEmployee("0001", 3000m);
            SeedPayableEmployee("0002", 2000m);
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));
            await admin.GenerateLinesAsync(run.Id);

            // By employee, not by row id: EF does not promise the insert order of a batch, so
            // "the first line" is not reliably the first employee.
            var line = await db.PayrollRunLines.SingleAsync(l => l.EmployeeId == leaving);
            await admin.AddAdjustmentAsync(line.Id, PayrollAdjustmentKind.Addition, "مكافأة", 100m);
            await admin.RemoveLineAsync(line.Id);

            Assert.Empty(await db.PayrollLineAdjustments.ToListAsync());
            var reloaded = await db.PayrollRuns.SingleAsync(r => r.Id == run.Id);
            Assert.Equal(1, reloaded.LineCount);
            Assert.Equal(2000m, reloaded.TotalNet);
        }

        // --- approval ---------------------------------------------------------

        [Fact]
        public async Task An_empty_run_cannot_be_approved()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));

            await Assert.ThrowsAsync<EmptyPayrollRunException>(() => admin.ApproveRunAsync(run.Id));
        }

        [Fact]
        public async Task Approval_names_every_employee_whose_deductions_exceed_their_pay()
        {
            var over = SeedPayableEmployee("0001", 1000m);
            SeedPayableEmployee("0002", 3000m);
            var alsoOver = SeedPayableEmployee("0003", 500m);

            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));
            await admin.GenerateLinesAsync(run.Id);

            foreach (var employeeId in new[] { over, alsoOver })
            {
                var line = await db.PayrollRunLines.SingleAsync(l => l.PayrollRunId == run.Id && l.EmployeeId == employeeId);
                await admin.AddAdjustmentAsync(line.Id, PayrollAdjustmentKind.Deduction, "خصم كبير", 5000m);
            }

            var refusal = await Assert.ThrowsAsync<NegativeNetPayException>(() => admin.ApproveRunAsync(run.Id));

            Assert.Equal(2, refusal.EmployeeIds.Count);
            Assert.Contains(over, refusal.EmployeeIds);
            Assert.Contains(alsoOver, refusal.EmployeeIds);
        }

        [Fact]
        public async Task An_approved_run_can_no_longer_be_edited_but_can_be_reopened()
        {
            SeedPayableEmployee("0001", 3000m);
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));
            await admin.GenerateLinesAsync(run.Id);
            await admin.ApproveRunAsync(run.Id);

            var line = await db.PayrollRunLines.SingleAsync(l => l.PayrollRunId == run.Id);
            var refusal = await Assert.ThrowsAsync<PayrollRunNotEditableException>(
                () => admin.AddAdjustmentAsync(line.Id, PayrollAdjustmentKind.Addition, "مكافأة", 100m));
            Assert.Equal(PayrollRunStatus.Approved, refusal.Status);

            await admin.ReopenRunAsync(run.Id);
            await admin.AddAdjustmentAsync(line.Id, PayrollAdjustmentKind.Addition, "مكافأة", 100m);

            Assert.Equal(3100m, (await db.PayrollRunLines.SingleAsync(l => l.Id == line.Id)).NetPay);
        }

        // --- payment ----------------------------------------------------------

        [Fact]
        public async Task Paying_the_run_consumes_the_advance_instalment_and_names_the_payslip()
        {
            var employeeId = SeedPayableEmployee("0001", 3000m);
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
            var first = await db.SalaryAdvanceInstallments.OrderBy(i => i.SequenceNo).FirstAsync();

            Assert.Equal(SalaryAdvanceInstallmentStatus.Deducted, first.Status);
            Assert.Equal(line.Id, first.PayrollRunLineId);
            Assert.Equal(_clock.UtcNow, first.DeductedAtUtc);

            var reloaded = await db.PayrollRuns.SingleAsync(r => r.Id == run.Id);
            Assert.Equal(PayrollRunStatus.Paid, reloaded.Status);
            Assert.Equal(new DateTime(2026, 9, 30), reloaded.PaymentDate);

            // Only this month's instalment: the rest of the schedule is untouched.
            var rest = await db.SalaryAdvanceInstallments.Where(i => i.SequenceNo > 1).ToListAsync();
            Assert.All(rest, i => Assert.Equal(SalaryAdvanceInstallmentStatus.Scheduled, i.Status));
            Assert.Equal(SalaryAdvanceStatus.Disbursed, (await db.SalaryAdvances.SingleAsync()).Status);
        }

        [Fact]
        public async Task The_run_that_takes_the_last_instalment_settles_the_advance()
        {
            var employeeId = SeedPayableEmployee("0001", 3000m);
            using var db = CreateContext();
            var advances = CreateAdvances(db);
            var advance = await advances.RequestAsync(employeeId, new DateTime(2026, 8, 20), 500m, 1, 2026, 9);
            await advances.ApproveAsync(advance.Id);
            await advances.DisburseAsync(advance.Id, new DateTime(2026, 8, 25), AdvanceDisbursementMethod.Cash);

            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));
            await admin.GenerateLinesAsync(run.Id);
            await admin.ApproveRunAsync(run.Id);
            await admin.MarkRunPaidAsync(run.Id, new DateTime(2026, 9, 30));

            var settled = await db.SalaryAdvances.SingleAsync(a => a.Id == advance.Id);
            Assert.Equal(SalaryAdvanceStatus.Settled, settled.Status);
            Assert.Equal(_clock.UtcNow, settled.SettledAtUtc);
        }

        [Fact]
        public async Task A_paid_run_is_terminal()
        {
            SeedPayableEmployee("0001", 3000m);
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));
            await admin.GenerateLinesAsync(run.Id);
            await admin.ApproveRunAsync(run.Id);
            await admin.MarkRunPaidAsync(run.Id, new DateTime(2026, 9, 30));

            await Assert.ThrowsAsync<InvalidPayrollRunStatusTransitionException>(() => admin.ReopenRunAsync(run.Id));
            await Assert.ThrowsAsync<InvalidPayrollRunStatusTransitionException>(() => admin.CancelRunAsync(run.Id, "خطأ"));
        }

        [Fact]
        public async Task A_draft_cannot_jump_straight_to_paid()
        {
            SeedPayableEmployee("0001", 3000m);
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));
            await admin.GenerateLinesAsync(run.Id);

            await Assert.ThrowsAsync<InvalidPayrollRunStatusTransitionException>(
                () => admin.MarkRunPaidAsync(run.Id, new DateTime(2026, 9, 30)));
        }

        // --- the database's own guarantees ------------------------------------

        [Fact]
        public async Task The_database_refuses_two_lines_for_one_employee_on_one_run()
        {
            var employeeId = SeedPayableEmployee("0001", 3000m);
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var run = await admin.OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));
            await admin.GenerateLinesAsync(run.Id);

            // Bypasses the service on purpose — a uniqueness rule only the service enforces is not
            // a guarantee.
            db.PayrollRunLines.Add(new PayrollRunLine
            {
                PayrollRunId = run.Id,
                EmployeeId = employeeId,
                BasicSalary = 1m,
                GrossPay = 1m,
                NetPay = 1m,
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task The_database_refuses_a_second_live_run_for_one_month()
        {
            using var db = CreateContext();
            await CreateAdmin(db).OpenRunAsync(2026, 9, new DateTime(2026, 9, 28));

            db.PayrollRuns.Add(new PayrollRun
            {
                PayrollRunNo = "PAY-9999",
                PeriodYear = 2026,
                PeriodMonth = 9,
                PaymentDate = new DateTime(2026, 9, 30),
                Status = PayrollRunStatus.Draft,
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }
}
