using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Dashboards;
using Sms.Application.GlExport;
using Sms.Application.ReadModels;
using Sms.Domain.Cafeteria;
using Sms.Domain.Common;
using Sms.Domain.Employees;
using Sms.Domain.Fees;
using Sms.Domain.Payments;
using Sms.Domain.Students;
using Sms.Domain.Teachers;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Dashboards;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// The statistics screen's one read (doc/Modules/31 §8.1) over a real
    /// Sqlite-backed <see cref="AppDbContext"/>, on the E-801 fixture: 12 students
    /// across 3 grades, two posted tuition charges, one of them fully credited.
    /// </summary>
    public sealed class StatisticsQueryTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2027, 6, 15, 8, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; } = 42;
        }

        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;

            public int AcademicYearId { get; set; }
        }

        /// <summary>
        /// Teacher load is the read model's answer, not this query's — the stub is
        /// what proves the query asks rather than re-counting assignments itself.
        /// </summary>
        private sealed class StubReadModels : IReadModelQuery
        {
            public List<TeacherLoadRow> Loads { get; } = new();

            public Task<IReadOnlyList<TeacherLoadRow>> GetTeacherLoadsAsync(int academicYearId, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<TeacherLoadRow>>(Loads);

            public Task<IReadOnlyList<StudentPositionRow>> GetStudentPositionsAsync(int? studentId = null, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<StudentPositionRow>>(Array.Empty<StudentPositionRow>());

            public Task<IReadOnlyList<AttendanceRateRow>> GetAttendanceRatesAsync(int sectionId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<AttendanceRateRow>>(Array.Empty<AttendanceRateRow>());

            public Task<IReadOnlyList<SeatUtilizationRow>> GetSeatUtilizationAsync(int academicYearId, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<SeatUtilizationRow>>(Array.Empty<SeatUtilizationRow>());

            public Task<IReadOnlyList<WalletBalanceRow>> GetWalletBalancesAsync(CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<WalletBalanceRow>>(Array.Empty<WalletBalanceRow>());
        }

        private sealed class StubLedger : IGlLedgerSummary
        {
            public DateTime? AskedFrom { get; private set; }

            public DateTime? AskedTo { get; private set; }

            public int AskedMonths { get; private set; }

            public Task<LedgerResultSummary> GetResultAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
            {
                AskedFrom = fromDate;
                AskedTo = toDate;
                return Task.FromResult(new LedgerResultSummary(500_000m, 420_000m));
            }

            public Task<IReadOnlyList<LedgerMonthSummary>> GetMonthlyResultAsync(DateTime firstMonth, int months, CancellationToken cancellationToken = default)
            {
                AskedMonths = months;
                return Task.FromResult<IReadOnlyList<LedgerMonthSummary>>(
                    new[] { new LedgerMonthSummary(2026, 9, 50_000m, 42_000m) });
            }
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();
        private readonly StubReadModels _readModels = new();
        private readonly RolloverFixture _fx;

        public StatisticsQueryTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();
            _fx = RolloverFixture.Seed(db, _clock.UtcNow, studentsPerGrade: 4);
            _tenant.AcademicYearId = _fx.SourceYearId;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private StatisticsQuery CreateQuery(AppDbContext db, IGlLedgerSummary? ledger = null)
            => new(db, _readModels, ledger);

        // ---------------------------------------------------------------- students

        [Fact]
        public async Task Students_are_counted_from_active_enrollments_not_from_student_records()
        {
            using var db = CreateContext();
            var stats = await CreateQuery(db).GetAsync(_fx.SourceYearId);

            Assert.Equal(12, stats.Students.Total);
            Assert.Equal(6, stats.Students.Male);
            Assert.Equal(6, stats.Students.Female);
            Assert.Equal(12, stats.Students.Admitted);
        }

        [Fact]
        public async Task A_withdrawn_enrollment_leaves_the_headcount_but_stays_in_the_outcome_split()
        {
            using (var arrange = CreateContext())
            {
                var enrollment = await arrange.Enrollments.FirstAsync(e => e.AcademicYearId == _fx.SourceYearId);
                enrollment.Status = EnrollmentStatus.Withdrawn;
                await arrange.SaveChangesAsync();
            }

            using var db = CreateContext();
            var stats = await CreateQuery(db).GetAsync(_fx.SourceYearId);

            Assert.Equal(11, stats.Students.Total);
            Assert.Equal(1, stats.Students.Withdrawn);
            Assert.Equal(12, stats.Students.ByStatus.Sum(s => s.Value));
        }

        [Fact]
        public async Task Grades_come_back_in_teaching_order_not_by_size()
        {
            using var db = CreateContext();
            var stats = await CreateQuery(db).GetAsync(_fx.SourceYearId);

            Assert.Equal(new[] { "Grade 1", "Grade 2", "Grade 3" }, stats.Students.ByGrade.Select(g => g.NameEn));
            Assert.All(stats.Students.ByGrade, g => Assert.Equal(4m, g.Value));
        }

        [Fact]
        public async Task A_retired_grade_level_still_names_the_enrollments_made_against_it()
        {
            // The soft-active trap: load the picker's filtered list and look a row up
            // by id, and the page dies the day someone retires a grade. Nobody has a
            // deactivated row in development, which is why this needs a test.
            using (var arrange = CreateContext())
            {
                var grade = await arrange.GradeLevels.SingleAsync(g => g.Id == _fx.GradeIds["G2"]);
                grade.IsActive = false;
                await arrange.SaveChangesAsync();
            }

            using var db = CreateContext();
            var stats = await CreateQuery(db).GetAsync(_fx.SourceYearId);

            Assert.Equal(3, stats.Students.ByGrade.Count);
            Assert.Contains(stats.Students.ByGrade, g => g.NameEn == "Grade 2" && g.Value == 4m);
        }

        // ---------------------------------------------------------------- staff

        [Fact]
        public async Task Staff_counts_split_by_status_and_teaching_load_comes_from_the_read_model()
        {
            int employeeId;
            using (var arrange = CreateContext())
            {
                employeeId = await AddEmployeeAsync(arrange, "EMP-1", EmployeeStatus.Active);
                await AddEmployeeAsync(arrange, "EMP-2", EmployeeStatus.Active);
                await AddEmployeeAsync(arrange, "EMP-3", EmployeeStatus.Terminated);

                arrange.TeacherProfiles.Add(new TeacherProfile { EmployeeId = employeeId, MaxWeeklyPeriods = 24 });
                await arrange.SaveChangesAsync();
            }

            // One teacher per band, each measured against their own cap of 24:
            // 41.7%, 75%, 91.7%, and one the read model has already called overloaded.
            _readModels.Loads.Add(new TeacherLoadRow(1, employeeId, 10, 24, false));
            _readModels.Loads.Add(new TeacherLoadRow(2, employeeId, 18, 24, false));
            _readModels.Loads.Add(new TeacherLoadRow(3, employeeId, 22, 24, false));
            _readModels.Loads.Add(new TeacherLoadRow(4, employeeId, 30, 24, true));
            _readModels.Loads.Add(new TeacherLoadRow(5, employeeId, 0, 24, false));

            using var db = CreateContext();
            var stats = await CreateQuery(db).GetAsync(_fx.SourceYearId);

            Assert.Equal(3, stats.Staff.Employees);
            Assert.Equal(2, stats.Staff.ActiveEmployees);
            Assert.Equal(1, stats.Staff.Teachers);

            // The teacher with no periods is not "teaching this year", so neither the
            // average nor the ratio is diluted by them.
            Assert.Equal(4, stats.Staff.AssignedTeachers);
            Assert.Equal(20m, stats.Staff.AveragePeriods);
            Assert.Equal(3m, stats.Staff.StudentsPerTeacher);

            Assert.Equal(1m, stats.Staff.ByLoadBand.Single(b => b.NameEn == "Under half").Value);
            Assert.Equal(1m, stats.Staff.ByLoadBand.Single(b => b.NameEn == "Half to four-fifths").Value);
            Assert.Equal(1m, stats.Staff.ByLoadBand.Single(b => b.NameEn == "Near or at capacity").Value);
            Assert.Equal(1m, stats.Staff.ByLoadBand.Single(b => b.NameEn == "Over capacity").Value);

            // The bands account for every assigned teacher and nobody else.
            Assert.Equal(4m, stats.Staff.ByLoadBand.Sum(b => b.Value));
        }

        [Fact]
        public async Task A_teacher_with_no_recorded_cap_is_still_counted_somewhere()
        {
            // A band chart whose bars do not add up to the headcount above it is
            // worse than a rough placement.
            _readModels.Loads.Add(new TeacherLoadRow(1, 0, 12, 0, false));

            using var db = CreateContext();
            var stats = await CreateQuery(db).GetAsync(_fx.SourceYearId);

            Assert.Equal(1, stats.Staff.AssignedTeachers);
            Assert.Equal(1m, stats.Staff.ByLoadBand.Sum(b => b.Value));
        }

        // ---------------------------------------------------------------- fees

        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task Fee_totals_add_up_and_outstanding_is_the_fee_modules_own_figure()
        {
            // Also the Sqlite decimal-Sum guard: every money total here is summed in
            // memory, and the day one of them becomes SumAsync this test throws.
            using var db = CreateContext();
            var stats = await CreateQuery(db).GetAsync(_fx.SourceYearId);

            Assert.Equal(2, stats.Fees.ChargeCount);
            Assert.Equal(2300m, stats.Fees.Billed);
            Assert.Equal(2000m, stats.Fees.Net);
            Assert.Equal(300m, stats.Fees.Vat);
            Assert.Equal(1150m, stats.Fees.CreditNotes);
            Assert.Equal(0m, stats.Fees.Discounts);

            // 2300 billed - 1150 credited - 0 discounted - 0 allocated.
            Assert.Equal(1150m, stats.Fees.Outstanding);
        }

        [Fact]
        public async Task A_voided_charge_billed_nothing()
        {
            using (var arrange = CreateContext())
            {
                var charge = await arrange.Charges.FirstAsync(c => c.AcademicYearId == _fx.SourceYearId);
                charge.Status = ChargeStatus.Void;
                await arrange.SaveChangesAsync();
            }

            using var db = CreateContext();
            var stats = await CreateQuery(db).GetAsync(_fx.SourceYearId);

            Assert.Equal(1, stats.Fees.ChargeCount);
            Assert.Equal(1150m, stats.Fees.Billed);
        }

        [Fact]
        public async Task A_retired_fee_category_still_names_the_charges_raised_under_it()
        {
            using (var arrange = CreateContext())
            {
                var category = await arrange.FeeCategories.SingleAsync(c => c.Id == _fx.TuitionCategoryId);
                category.IsActive = false;
                await arrange.SaveChangesAsync();
            }

            using var db = CreateContext();
            var stats = await CreateQuery(db).GetAsync(_fx.SourceYearId);

            var tuition = Assert.Single(stats.Fees.ByCategory);
            Assert.Equal("Tuition", tuition.NameEn);
            Assert.Equal(2300m, tuition.Value);
        }

        [Fact]
        public async Task The_month_axis_is_zero_filled_across_the_whole_academic_year()
        {
            // Grouping by month would return only the months that had charges, and a
            // two-point line reading "October, June" implies a continuous rise with
            // eight quiet months silently deleted.
            using var db = CreateContext();
            var stats = await CreateQuery(db).GetAsync(_fx.SourceYearId);

            Assert.Equal(10, stats.Fees.ByMonth.Count);
            Assert.Equal((2026, 9), (stats.Fees.ByMonth[0].Year, stats.Fees.ByMonth[0].Month));
            Assert.Equal((2027, 6), (stats.Fees.ByMonth[^1].Year, stats.Fees.ByMonth[^1].Month));

            // Both fixture charges are posted eight months before 2027-06-15.
            Assert.Equal(2300m, stats.Fees.ByMonth.Single(p => p.Year == 2026 && p.Month == 10).Value);
            Assert.Equal(0m, stats.Fees.ByMonth.Single(p => p.Year == 2026 && p.Month == 9).Value);
        }

        [Fact]
        public async Task A_charge_posted_before_the_year_opens_is_named_rather_than_silently_dropped()
        {
            // Early registration bills a year months before it starts. The charge
            // belongs to the year it is for, so it counts in Billed — but it lands on
            // no bar of the monthly axis, and a chart totalling less than the number
            // printed above it is the failure BR-DSH-002 exists to prevent.
            using (var arrange = CreateContext())
            {
                var existing = await arrange.Charges.FirstAsync(c => c.AcademicYearId == _fx.SourceYearId);
                arrange.Charges.Add(new Charge
                {
                    AcademicYearId = _fx.SourceYearId,
                    StudentId = existing.StudentId,
                    PayerId = existing.PayerId,
                    FeeCategoryId = _fx.TuitionCategoryId,
                    SourceType = ChargeSourceType.Registration,
                    ChargeNo = "INV-EARLY",
                    NetAmount = 500m, VatRateSnapshot = 0m, VatAmount = 0m, GrossAmount = 500m,
                    Status = ChargeStatus.Posted,
                    // Two months before the year opens on 2026-09-01.
                    PostedAtUtc = new DateTime(2026, 7, 1),
                    InvoiceUuid = Guid.NewGuid(),
                });
                await arrange.SaveChangesAsync();
            }

            using var db = CreateContext();
            var stats = await CreateQuery(db).GetAsync(_fx.SourceYearId);

            Assert.Equal(2800m, stats.Fees.Billed);
            Assert.Equal(2300m, stats.Fees.ByMonth.Sum(p => p.Value));
            Assert.Equal(500m, stats.Fees.BilledOutsideMonths);
        }

        [Fact]
        public async Task Nothing_falls_outside_the_axis_when_every_charge_was_posted_inside_the_year()
        {
            using var db = CreateContext();
            var stats = await CreateQuery(db).GetAsync(_fx.SourceYearId);

            Assert.Equal(0m, stats.Fees.BilledOutsideMonths);
        }

        // ---------------------------------------------------------------- payments

        [Fact]
        public async Task Collections_are_bounded_by_the_years_dates_because_a_receipt_carries_no_year()
        {
            using (var arrange = CreateContext())
            {
                var payerId = (await arrange.Payers.OrderBy(p => p.Id).FirstAsync()).Id;

                await AddReceiptAsync(arrange, payerId, "RCP-IN", 400m, new DateTime(2027, 1, 10));
                // The last afternoon of the year: excluded if the range stops at midnight.
                await AddReceiptAsync(arrange, payerId, "RCP-LAST", 100m, new DateTime(2027, 6, 30, 16, 30, 0));
                // The next year's money.
                await AddReceiptAsync(arrange, payerId, "RCP-OUT", 999m, new DateTime(2027, 7, 1));
                // Cancelled: collected nothing.
                await AddReceiptAsync(arrange, payerId, "RCP-VOID", 750m, new DateTime(2027, 2, 1), ReceiptStatus.Void);
            }

            using var db = CreateContext();
            var stats = await CreateQuery(db).GetAsync(_fx.SourceYearId);

            Assert.Equal(2, stats.Payments.ReceiptCount);
            Assert.Equal(500m, stats.Payments.Collected);
            Assert.Equal(250m, stats.Payments.AverageReceipt);
        }

        [Fact]
        public async Task Collection_rate_reads_against_what_was_billed()
        {
            using (var arrange = CreateContext())
            {
                var payerId = (await arrange.Payers.OrderBy(p => p.Id).FirstAsync()).Id;
                await AddReceiptAsync(arrange, payerId, "RCP-1", 1150m, new DateTime(2027, 1, 10));
            }

            using var db = CreateContext();
            var stats = await CreateQuery(db).GetAsync(_fx.SourceYearId);

            // 1150 of 2300 billed.
            Assert.Equal(50m, stats.Payments.CollectionRate);
        }

        [Fact]
        public async Task Collections_by_method_are_largest_first()
        {
            using (var arrange = CreateContext())
            {
                var payerId = (await arrange.Payers.OrderBy(p => p.Id).FirstAsync()).Id;
                await AddReceiptAsync(arrange, payerId, "RCP-C", 100m, new DateTime(2027, 1, 10), method: PaymentMethod.Cash);
                await AddReceiptAsync(arrange, payerId, "RCP-B", 900m, new DateTime(2027, 1, 11), method: PaymentMethod.BankTransfer);
            }

            using var db = CreateContext();
            var stats = await CreateQuery(db).GetAsync(_fx.SourceYearId);

            Assert.Equal(new[] { "Bank transfer", "Cash" }, stats.Payments.ByMethod.Select(m => m.NameEn));
        }

        [Fact]
        public async Task The_monthly_pair_carries_billed_and_collected_on_one_axis()
        {
            using (var arrange = CreateContext())
            {
                var payerId = (await arrange.Payers.OrderBy(p => p.Id).FirstAsync()).Id;
                await AddReceiptAsync(arrange, payerId, "RCP-1", 600m, new DateTime(2026, 10, 20));
            }

            using var db = CreateContext();
            var stats = await CreateQuery(db).GetAsync(_fx.SourceYearId);

            var october = stats.Payments.ByMonth.Single(p => p.Year == 2026 && p.Month == 10);
            Assert.Equal(2300m, october.First);
            Assert.Equal(600m, october.Second);
        }

        // ---------------------------------------------------------------- expenses

        [Fact]
        public async Task With_no_ledger_attached_expenses_are_absent_rather_than_zero()
        {
            // "The school spent nothing" and "nobody asked the books" are different
            // statements, and the screen has to be able to tell them apart.
            using var db = CreateContext();
            var stats = await CreateQuery(db).GetAsync(_fx.SourceYearId);

            Assert.Null(stats.Expenses);
        }

        [Fact]
        public async Task An_attached_ledger_is_asked_for_the_years_own_dates()
        {
            var ledger = new StubLedger();

            using var db = CreateContext();
            var stats = await CreateQuery(db, ledger).GetAsync(_fx.SourceYearId);

            Assert.NotNull(stats.Expenses);
            Assert.Equal(500_000m, stats.Expenses!.Revenue);
            Assert.Equal(420_000m, stats.Expenses.Expenses);
            Assert.Equal(80_000m, stats.Expenses.Net);

            Assert.Equal(new DateTime(2026, 9, 1), ledger.AskedFrom);
            Assert.Equal(new DateTime(2027, 6, 30), ledger.AskedTo);
            Assert.Equal(10, ledger.AskedMonths);
        }

        // ---------------------------------------------------------------- edges

        [Fact]
        public async Task A_year_that_is_not_there_returns_empty_sections_rather_than_throwing()
        {
            // The only route here is a stale link — a dashboard is not the place to
            // throw for one, and empty sections must not read as "no students".
            using var db = CreateContext();
            var stats = await CreateQuery(db).GetAsync(-1);

            Assert.Equal(0, stats.Students.Total);
            Assert.Equal(0m, stats.Fees.Billed);
            Assert.Empty(stats.Fees.ByMonth);
            Assert.Null(stats.Expenses);
        }

        // ---------------------------------------------------------------- helpers

        private static async Task<int> AddEmployeeAsync(AppDbContext db, string employeeNo, EmployeeStatus status)
        {
            var employee = new Employee
            {
                EmployeeNo = employeeNo,
                FirstNameAr = "موظف", FatherNameAr = "أب", GrandfatherNameAr = "جد", FamilyNameAr = "عائلة",
                FirstNameEn = "Employee", FatherNameEn = "Father", GrandfatherNameEn = "Grandfather", FamilyNameEn = "Family",
                Gender = Gender.Male, DateOfBirth = new DateTime(1985, 1, 1), NationalityLookupId = 1, Status = status,
            };
            db.Employees.Add(employee);
            await db.SaveChangesAsync();
            return employee.Id;
        }

        private static async Task AddReceiptAsync(
            AppDbContext db, int payerId, string receiptNo, decimal amount, DateTime issuedAtUtc,
            ReceiptStatus status = ReceiptStatus.Posted, PaymentMethod method = PaymentMethod.Cash)
        {
            db.Receipts.Add(new Receipt
            {
                PayerId = payerId,
                ReceiptNo = receiptNo,
                Method = method,
                Amount = amount,
                Status = status,
                Purpose = ReceiptPurpose.FeePayment,
                IssuedAtUtc = issuedAtUtc,
            });
            await db.SaveChangesAsync();
        }
    }
}
