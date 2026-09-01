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
    /// سلف الموظفين over a real Sqlite-backed AppDbContext, including the real INumberIssuer
    /// (the "ADV" series). Owner request, 2026-08-28.
    /// <para>
    /// Deliberately untagged by <c>[BusinessRule]</c>: doc/Modules/12 describes no staff advances,
    /// so there is no numbered rule to cite and inventing one would put a fabricated reference into
    /// the CI coverage gate. See <c>Sms.Domain.Payroll.SalaryAdvance</c>.
    /// </para>
    /// </summary>
    public sealed class SalaryAdvanceAdminTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 8, 28, 8, 0, 0, DateTimeKind.Utc);
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

        public SalaryAdvanceAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            db.NumberingSeries.Add(new NumberingSeries
            {
                Code = "ADV", EntityName = "SalaryAdvance", FormatTemplate = "ADV-{SEQ:5}",
                ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Normal,
                EffectiveFromUtc = _clock.UtcNow, IsActive = true,
            });
            db.NumberingSeries.Add(new NumberingSeries
            {
                Code = "PAY", EntityName = "PayrollRun", FormatTemplate = "PAY-{SEQ:4}",
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

        private SalaryAdvanceAdmin CreateAdmin(AppDbContext db) =>
            new(db, new NumberIssuer(db, _tenant, _tenant, _clock), _clock);

        private int SeedEmployee(string suffix = "1")
        {
            using var db = CreateContext();
            var employee = new Employee
            {
                EmployeeNo = "EMP-0000" + suffix,
                FirstNameAr = "موظف", FatherNameAr = "أب", GrandfatherNameAr = "جد", FamilyNameAr = "عائلة",
                FirstNameEn = "Employee", FatherNameEn = "Father", GrandfatherNameEn = "Grand", FamilyNameEn = "Family",
                Gender = Gender.Male, DateOfBirth = new DateTime(1990, 1, 1), NationalityLookupId = 1,
                Status = EmployeeStatus.Active,
            };
            db.Employees.Add(employee);
            db.SaveChanges();
            return employee.Id;
        }

        // --- request ----------------------------------------------------------

        [Fact]
        public async Task Requesting_issues_a_number_and_owes_nothing_yet()
        {
            var employeeId = SeedEmployee();
            using var db = CreateContext();

            var advance = await CreateAdmin(db).RequestAsync(
                employeeId, new DateTime(2026, 8, 20), 3000m, 6, 2026, 9, "علاج");

            Assert.Equal("ADV-00001", advance.AdvanceNo);
            Assert.Equal(SalaryAdvanceStatus.Requested, advance.Status);
            Assert.Equal(1, advance.SchoolId);
            Assert.Empty(await db.SalaryAdvanceInstallments.ToListAsync());
        }

        [Fact]
        public async Task A_second_advance_is_refused_while_the_first_is_outstanding()
        {
            var employeeId = SeedEmployee();
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            var first = await admin.RequestAsync(employeeId, new DateTime(2026, 8, 20), 3000m, 6, 2026, 9);

            var refusal = await Assert.ThrowsAsync<OutstandingAdvanceException>(
                () => admin.RequestAsync(employeeId, new DateTime(2026, 8, 21), 500m, 2, 2026, 10));

            Assert.Equal(first.AdvanceNo, refusal.OutstandingAdvanceNo);
        }

        [Fact]
        public async Task A_rejected_advance_stops_blocking_the_next_request()
        {
            var employeeId = SeedEmployee();
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            var first = await admin.RequestAsync(employeeId, new DateTime(2026, 8, 20), 3000m, 6, 2026, 9);
            await admin.RejectAsync(first.Id, "لا يوجد رصيد");

            var second = await admin.RequestAsync(employeeId, new DateTime(2026, 8, 21), 500m, 2, 2026, 10);

            Assert.Equal(SalaryAdvanceStatus.Requested, second.Status);
        }

        [Fact]
        public async Task Another_employees_advance_does_not_block_this_one()
        {
            var first = SeedEmployee("1");
            var second = SeedEmployee("2");
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            await admin.RequestAsync(first, new DateTime(2026, 8, 20), 3000m, 6, 2026, 9);
            var other = await admin.RequestAsync(second, new DateTime(2026, 8, 20), 1000m, 4, 2026, 9);

            Assert.Equal(SalaryAdvanceStatus.Requested, other.Status);
        }

        [Fact]
        public async Task An_impossible_amount_is_refused_at_the_request_not_at_disbursement()
        {
            var employeeId = SeedEmployee();
            using var db = CreateContext();

            await Assert.ThrowsAsync<InvalidAdvanceAmountException>(
                () => CreateAdmin(db).RequestAsync(employeeId, new DateTime(2026, 8, 20), 0m, 6, 2026, 9));
        }

        [Fact]
        public async Task Editing_a_request_needs_an_audit_reason_because_the_amount_is_T1()
        {
            var employeeId = SeedEmployee();
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var advance = await admin.RequestAsync(employeeId, new DateTime(2026, 8, 20), 3000m, 6, 2026, 9);

            await Assert.ThrowsAsync<MissingAuditReasonException>(
                () => admin.UpdateRequestAsync(advance.Id, new DateTime(2026, 8, 20), 2000m, 4, 2026, 9, null));

            _audit.Reason = "صححنا المبلغ بناء على طلب الموظف";
            var updated = await admin.UpdateRequestAsync(advance.Id, new DateTime(2026, 8, 20), 2000m, 4, 2026, 9, null);

            Assert.Equal(2000m, updated.Amount);
        }

        // --- decision and disbursement ---------------------------------------

        [Fact]
        public async Task Disbursing_builds_the_whole_schedule_summing_to_the_advance()
        {
            var employeeId = SeedEmployee();
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            var advance = await admin.RequestAsync(employeeId, new DateTime(2026, 8, 20), 1000m, 3, 2026, 9);
            await admin.ApproveAsync(advance.Id, "موافق");
            await admin.DisburseAsync(advance.Id, new DateTime(2026, 8, 25), AdvanceDisbursementMethod.Cash, "REF-1");

            var schedule = await db.SalaryAdvanceInstallments
                .Where(i => i.SalaryAdvanceId == advance.Id)
                .OrderBy(i => i.SequenceNo)
                .ToListAsync();

            Assert.Equal(3, schedule.Count);
            Assert.Equal(1000m, schedule.Sum(i => i.Amount));
            Assert.Equal(new[] { (2026, 9), (2026, 10), (2026, 11) }, schedule.Select(i => (i.DueYear, i.DueMonth)));
            Assert.All(schedule, i => Assert.Equal(SalaryAdvanceInstallmentStatus.Scheduled, i.Status));
            Assert.All(schedule, i => Assert.Equal(1, i.SchoolId));

            var reloaded = await db.SalaryAdvances.SingleAsync(a => a.Id == advance.Id);
            Assert.Equal(SalaryAdvanceStatus.Disbursed, reloaded.Status);
            Assert.Equal(AdvanceDisbursementMethod.Cash, reloaded.DisbursementMethod);
        }

        [Fact]
        public async Task Disbursing_a_request_nobody_approved_is_refused()
        {
            var employeeId = SeedEmployee();
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var advance = await admin.RequestAsync(employeeId, new DateTime(2026, 8, 20), 1000m, 3, 2026, 9);

            var refusal = await Assert.ThrowsAsync<InvalidSalaryAdvanceStatusTransitionException>(
                () => admin.DisburseAsync(advance.Id, new DateTime(2026, 8, 25), AdvanceDisbursementMethod.Cash));

            Assert.Equal(SalaryAdvanceStatus.Requested, refusal.From);
            Assert.Equal(SalaryAdvanceStatus.Disbursed, refusal.To);
        }

        [Fact]
        public async Task A_disbursed_advance_cannot_be_cancelled_away()
        {
            var employeeId = SeedEmployee();
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var advance = await Disburse(admin, employeeId, 900m, 3);

            await Assert.ThrowsAsync<InvalidSalaryAdvanceStatusTransitionException>(
                () => admin.CancelAsync(advance.Id, "تراجعنا"));
        }

        // --- waiving ----------------------------------------------------------

        [Fact]
        public async Task Waiving_the_last_open_instalment_settles_the_advance()
        {
            var employeeId = SeedEmployee();
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var advance = await Disburse(admin, employeeId, 300m, 2);

            var schedule = await db.SalaryAdvanceInstallments
                .Where(i => i.SalaryAdvanceId == advance.Id).OrderBy(i => i.SequenceNo).ToListAsync();

            await admin.WaiveInstallmentAsync(schedule[0].Id, "ظرف صحي");
            Assert.Equal(SalaryAdvanceStatus.Disbursed, (await db.SalaryAdvances.SingleAsync(a => a.Id == advance.Id)).Status);

            await admin.WaiveInstallmentAsync(schedule[1].Id, "ظرف صحي");

            var settled = await db.SalaryAdvances.SingleAsync(a => a.Id == advance.Id);
            Assert.Equal(SalaryAdvanceStatus.Settled, settled.Status);
            Assert.Equal(_clock.UtcNow, settled.SettledAtUtc);
        }

        [Fact]
        public async Task Waiving_everything_left_closes_the_advance_in_one_step()
        {
            var employeeId = SeedEmployee();
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var advance = await Disburse(admin, employeeId, 1200m, 4);

            await admin.WaiveRemainingAsync(advance.Id, "إعفاء بقرار الإدارة");

            var schedule = await db.SalaryAdvanceInstallments.Where(i => i.SalaryAdvanceId == advance.Id).ToListAsync();
            Assert.All(schedule, i => Assert.Equal(SalaryAdvanceInstallmentStatus.Waived, i.Status));
            Assert.Equal(SalaryAdvanceStatus.Settled, (await db.SalaryAdvances.SingleAsync(a => a.Id == advance.Id)).Status);
        }

        [Fact]
        public async Task An_instalment_already_waived_cannot_be_waived_again()
        {
            var employeeId = SeedEmployee();
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var advance = await Disburse(admin, employeeId, 300m, 2);
            var first = await db.SalaryAdvanceInstallments
                .Where(i => i.SalaryAdvanceId == advance.Id).OrderBy(i => i.SequenceNo).FirstAsync();

            await admin.WaiveInstallmentAsync(first.Id);

            var refusal = await Assert.ThrowsAsync<InstallmentNotWaivableException>(
                () => admin.WaiveInstallmentAsync(first.Id));
            Assert.Equal(SalaryAdvanceInstallmentStatus.Waived, refusal.Status);
        }

        [Fact]
        public async Task An_instalment_whose_month_is_already_approved_cannot_be_waived()
        {
            var employeeId = SeedEmployee();
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var advance = await Disburse(admin, employeeId, 300m, 2);
            var first = await db.SalaryAdvanceInstallments
                .Where(i => i.SalaryAdvanceId == advance.Id).OrderBy(i => i.SequenceNo).FirstAsync();

            // A run for the instalment's own month, signed off.
            db.PayrollRuns.Add(new PayrollRun
            {
                PayrollRunNo = "PAY-0001",
                PeriodYear = first.DueYear,
                PeriodMonth = first.DueMonth,
                PaymentDate = new DateTime(first.DueYear, first.DueMonth, 28),
                Status = PayrollRunStatus.Approved,
            });
            await db.SaveChangesAsync();

            var refusal = await Assert.ThrowsAsync<InstallmentLockedByPayrollRunException>(
                () => admin.WaiveInstallmentAsync(first.Id));

            Assert.Equal("PAY-0001", refusal.RunNo);
            Assert.Equal(PayrollRunStatus.Approved, refusal.RunStatus);
        }

        // --- rescheduling -----------------------------------------------------

        [Fact]
        public async Task Rescheduling_replaces_only_what_has_not_happened_yet()
        {
            var employeeId = SeedEmployee();
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var advance = await Disburse(admin, employeeId, 1200m, 4);

            // One instalment already recovered: 300 gone, 900 left to reschedule.
            var first = await db.SalaryAdvanceInstallments
                .Where(i => i.SalaryAdvanceId == advance.Id).OrderBy(i => i.SequenceNo).FirstAsync();
            first.Status = SalaryAdvanceInstallmentStatus.Deducted;
            first.DeductedAtUtc = _clock.UtcNow;
            await db.SaveChangesAsync();

            _audit.Reason = "بناء على طلب الموظف تخفيض القسط";
            await admin.RescheduleAsync(advance.Id, 6, 2027, 1);

            var schedule = await db.SalaryAdvanceInstallments
                .Where(i => i.SalaryAdvanceId == advance.Id).OrderBy(i => i.SequenceNo).ToListAsync();

            Assert.Equal(7, schedule.Count);
            Assert.Equal(1200m, schedule.Sum(i => i.Amount));

            var recovered = schedule.Single(i => i.Status == SalaryAdvanceInstallmentStatus.Deducted);
            Assert.Equal(300m, recovered.Amount);
            Assert.Equal(1, recovered.SequenceNo);

            var open = schedule.Where(i => i.Status == SalaryAdvanceInstallmentStatus.Scheduled).ToList();
            Assert.Equal(6, open.Count);
            Assert.Equal(900m, open.Sum(i => i.Amount));

            // Numbering continues rather than restarting, so an old payslip citing instalment 1
            // still points at the row it deducted.
            Assert.Equal(new[] { 2, 3, 4, 5, 6, 7 }, open.Select(i => i.SequenceNo));
            Assert.Equal((2027, 1), (open[0].DueYear, open[0].DueMonth));
        }

        [Fact]
        public async Task Rescheduling_without_a_stated_reason_is_refused_and_keeps_the_old_plan()
        {
            var employeeId = SeedEmployee();
            int advanceId;
            using (var setup = CreateContext())
            {
                advanceId = (await Disburse(CreateAdmin(setup), employeeId, 1200m, 4)).Id;
            }

            // Its own context, as a request would have: the instalment count is T1 with a mandatory
            // reason, because changing what comes out of somebody's salary each month is a decision.
            using (var db = CreateContext())
            {
                await Assert.ThrowsAsync<MissingAuditReasonException>(
                    () => CreateAdmin(db).RescheduleAsync(advanceId, 6, 2027, 1));
            }

            // The refusal took the old schedule with it or it took nothing — the deletion and the
            // rewrite are one transaction, so a failed reschedule must not leave money owed with
            // nothing scheduled to recover it.
            using (var db = CreateContext())
            {
                var schedule = await db.SalaryAdvanceInstallments
                    .Where(i => i.SalaryAdvanceId == advanceId).OrderBy(i => i.SequenceNo).ToListAsync();

                Assert.Equal(4, schedule.Count);
                Assert.Equal(1200m, schedule.Sum(i => i.Amount));
                Assert.All(schedule, i => Assert.Equal(SalaryAdvanceInstallmentStatus.Scheduled, i.Status));
            }
        }

        [Fact]
        public async Task Rescheduling_an_advance_that_was_never_disbursed_is_refused()
        {
            var employeeId = SeedEmployee();
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var advance = await admin.RequestAsync(employeeId, new DateTime(2026, 8, 20), 1000m, 3, 2026, 9);
            await admin.ApproveAsync(advance.Id);

            await Assert.ThrowsAsync<InvalidSalaryAdvanceStatusTransitionException>(
                () => admin.RescheduleAsync(advance.Id, 6, 2027, 1));
        }

        // --- the database's own guarantees ------------------------------------

        [Fact]
        public async Task The_database_refuses_two_instalments_with_the_same_sequence()
        {
            var employeeId = SeedEmployee();
            using var db = CreateContext();
            var advance = await Disburse(CreateAdmin(db), employeeId, 300m, 2);

            // Bypasses the service on purpose: a uniqueness guarantee only the service enforces is
            // not a guarantee, and "it compiled" proves nothing about the index.
            db.SalaryAdvanceInstallments.Add(new SalaryAdvanceInstallment
            {
                SalaryAdvanceId = advance.Id,
                SequenceNo = 1,
                DueYear = 2027,
                DueMonth = 5,
                Amount = 10m,
                Status = SalaryAdvanceInstallmentStatus.Scheduled,
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        private static async Task<SalaryAdvance> Disburse(
            SalaryAdvanceAdmin admin, int employeeId, decimal amount, int installments,
            int firstYear = 2026, int firstMonth = 9)
        {
            var advance = await admin.RequestAsync(employeeId, new DateTime(2026, 8, 20), amount, installments, firstYear, firstMonth);
            await admin.ApproveAsync(advance.Id);
            return await admin.DisburseAsync(advance.Id, new DateTime(2026, 8, 25), AdvanceDisbursementMethod.BankTransfer);
        }
    }
}
