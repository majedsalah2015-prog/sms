using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Dashboards;
using Sms.Application.Fees;
using Sms.Application.GlExport;
using Sms.Application.ReadModels;
using Sms.Domain.Common;
using Sms.Domain.Employees;
using Sms.Domain.Fees;
using Sms.Domain.Payments;
using Sms.Domain.Students;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Dashboards
{
    /// <summary>
    /// Computes the statistics screen (doc/Modules/31 §8.1). Read-only — never
    /// calls <c>SaveChangesAsync</c>, the same discipline
    /// <see cref="DashboardQuery"/> keeps.
    /// <para>
    /// <b>Where a figure already has an owner, this asks the owner.</b>
    /// Receivables come from <see cref="StudentFinancialPositionCalculator"/>
    /// (BR-FEE-008) and teacher load from <see cref="IReadModelQuery"/>'s
    /// <c>vw_TeacherLoad</c> equivalent, so a number here and the same number on
    /// the screen it drills into cannot disagree (BR-DSH-002). Re-deriving either
    /// one locally would have been shorter and would have been the bug.
    /// </para>
    /// <para>
    /// <b>Every money total is summed in memory, deliberately.</b> EF Core's
    /// Sqlite provider cannot translate <c>Sum()</c> over <c>decimal</c>: it
    /// compiles, and throws the first time a test runs it. Each block below
    /// therefore projects the narrowest possible row set and adds it up here —
    /// see CLAUDE.md's EF traps.
    /// </para>
    /// <para>
    /// <b>Lookups ignore the soft-active filter; pickers do not.</b> A grade level
    /// or fee category the school retires must still name the rows that point at
    /// it, or the day someone deactivates "Bus fee" its whole column vanishes from
    /// the year's history. Every such read below is
    /// <c>IgnoreQueryFilters()</c> plus an explicit <c>SchoolId</c> predicate —
    /// ignoring the filters drops tenant scoping with it, and re-stating it is
    /// what keeps this from reading another school's catalogue.
    /// </para>
    /// </summary>
    public class StatisticsQuery : IStatisticsQuery
    {
        private readonly AppDbContext _db;
        private readonly IReadModelQuery _readModels;
        private readonly IGlLedgerSummary? _ledger;

        /// <param name="ledger">
        /// Optional, exactly as <c>IGlPostingPort</c> is. Absent means no ledger is
        /// attached, and the expenses section comes back null rather than zeroed —
        /// see <see cref="ExpenseStatistics"/>.
        /// </param>
        public StatisticsQuery(AppDbContext db, IReadModelQuery readModels, IGlLedgerSummary? ledger = null)
        {
            _db = db;
            _readModels = readModels;
            _ledger = ledger;
        }

        public async Task<SchoolStatistics> GetAsync(int academicYearId, CancellationToken cancellationToken = default)
        {
            var year = await _db.AcademicYears.AsNoTracking()
                .SingleOrDefaultAsync(y => y.Id == academicYearId, cancellationToken);

            // A year that is not this school's, or not there at all, is not an error
            // to throw at a dashboard: the caller picked it from a list this same
            // context filtered, so the only way here is a stale link. Empty sections
            // say "nothing to show" without pretending the school has no students.
            if (year == null)
            {
                return Empty();
            }

            var months = MonthAxisBuilder.Build(year.StartDate, year.EndDate);

            var students = await StudentsAsync(academicYearId, cancellationToken);
            var staff = await StaffAsync(academicYearId, students.Total, cancellationToken);
            var fees = await FeesAsync(academicYearId, months, cancellationToken);
            var payments = await PaymentsAsync(year.StartDate, year.EndDate, fees, months, cancellationToken);
            var expenses = await ExpensesAsync(year.StartDate, year.EndDate, cancellationToken);

            return new SchoolStatistics(students, staff, fees, payments, expenses);
        }

        // ------------------------------------------------------------------ students

        /// <summary>
        /// doc/Modules/10 §11. Counted from enrollments rather than student records:
        /// a student row outlives the year that created it, so counting students
        /// would report every alumnus the school ever had as if they were sitting
        /// in a classroom.
        /// </summary>
        private async Task<StudentStatistics> StudentsAsync(int academicYearId, CancellationToken cancellationToken)
        {
            var enrollments = await _db.Enrollments.AsNoTracking()
                .Where(e => e.AcademicYearId == academicYearId)
                .Select(e => new
                {
                    e.StudentId,
                    e.GradeYearProfileId,
                    e.Status,
                    e.SourceType,
                })
                .ToListAsync(cancellationToken);

            var active = enrollments.Where(e => e.Status == EnrollmentStatus.Active).ToList();

            // Gender comes off the student, and the enrolled student may since have
            // been deactivated — a withdrawal deactivates the record but does not
            // rewrite the year that already happened. IgnoreQueryFilters keeps them
            // countable; the SchoolId predicate puts back the tenant scoping it drops.
            //
            // Joined rather than looked up by a list of ids, for the reason the fee
            // block spells out: a thousand-student school would otherwise inline a
            // thousand literals into the query.
            var genders = await (
                from e in _db.Enrollments.AsNoTracking()
                    .Where(e => e.AcademicYearId == academicYearId && e.Status == EnrollmentStatus.Active)
                join s in _db.Students.IgnoreQueryFilters().AsNoTracking()
                        .Where(s => s.SchoolId == _db.CurrentSchoolId)
                    on e.StudentId equals s.Id
                select s.Gender).ToListAsync(cancellationToken);

            var byGrade = await ByGradeAsync(academicYearId, active.Select(e => e.GradeYearProfileId).ToList(), cancellationToken);

            var byStatus = enrollments
                .GroupBy(e => e.Status)
                .OrderByDescending(g => g.Count())
                .Select(g => new StatisticSlice(EnrollmentStatusEn(g.Key), EnrollmentStatusAr(g.Key), g.Count()))
                .ToList();

            return new StudentStatistics(
                active.Count,
                genders.Count(g => g == Gender.Male),
                genders.Count(g => g == Gender.Female),
                enrollments.Count(e => e.SourceType == EnrollmentSourceType.Admission),
                enrollments.Count(e => e.Status == EnrollmentStatus.Withdrawn),
                byGrade,
                byStatus);
        }

        /// <summary>
        /// Headcount per grade level, in the grades' own teaching order rather than
        /// by size — a reader scanning a school's grades expects them in the order
        /// children move through them, and a bar chart sorted by count hides the
        /// shape of the school.
        /// </summary>
        private async Task<IReadOnlyList<StatisticSlice>> ByGradeAsync(
            int academicYearId, IReadOnlyList<int> enrolledProfileIds, CancellationToken cancellationToken)
        {
            if (enrolledProfileIds.Count == 0)
            {
                return Array.Empty<StatisticSlice>();
            }

            var profiles = await _db.GradeYearProfiles.AsNoTracking()
                .Where(p => p.AcademicYearId == academicYearId)
                .Select(p => new { p.Id, p.GradeLevelId })
                .ToListAsync(cancellationToken);

            var levelIds = profiles.Select(p => p.GradeLevelId).Distinct().ToList();

            // The lookup, not the picker: a retired grade level still names the
            // enrollments that were made against it (SoftActiveLookupTests).
            var levels = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking()
                .Where(l => l.SchoolId == _db.CurrentSchoolId && levelIds.Contains(l.Id))
                .Select(l => new { l.Id, l.Name.NameEn, l.Name.NameAr, l.SequenceOrder })
                .ToListAsync(cancellationToken);

            var levelByProfile = profiles.ToDictionary(p => p.Id, p => p.GradeLevelId);

            return enrolledProfileIds
                .Where(levelByProfile.ContainsKey)
                .GroupBy(id => levelByProfile[id])
                .Select(g => new { Level = levels.FirstOrDefault(l => l.Id == g.Key), Count = g.Count() })
                .Where(x => x.Level != null)
                .OrderBy(x => x.Level!.SequenceOrder)
                .Select(x => new StatisticSlice(x.Level!.NameEn, x.Level!.NameAr, x.Count))
                .ToList();
        }

        // ------------------------------------------------------------------ staff

        /// <summary>
        /// doc/Modules/12 §11 and doc/Modules/13 §8.4's load board reduced to its
        /// totals. Load comes from <see cref="IReadModelQuery"/> rather than being
        /// re-counted here, so this screen and the teacher-load board report the
        /// same week.
        /// </summary>
        private async Task<StaffStatistics> StaffAsync(int academicYearId, int enrolledStudents, CancellationToken cancellationToken)
        {
            var employees = await _db.Employees.AsNoTracking()
                .Select(e => e.Status)
                .ToListAsync(cancellationToken);

            var teacherCount = await _db.TeacherProfiles.AsNoTracking().CountAsync(cancellationToken);
            var loads = await _readModels.GetTeacherLoadsAsync(academicYearId, cancellationToken);

            var assigned = loads.Where(l => l.CurrentWeeklyPeriods > 0).ToList();

            var byStatus = employees
                .GroupBy(s => s)
                .OrderByDescending(g => g.Count())
                .Select(g => new StatisticSlice(EmployeeStatusEn(g.Key), EmployeeStatusAr(g.Key), g.Count()))
                .ToList();

            return new StaffStatistics(
                employees.Count,
                employees.Count(s => s == EmployeeStatus.Active),
                teacherCount,
                assigned.Count,
                assigned.Count == 0
                    ? 0m
                    : Math.Round((decimal)assigned.Sum(l => l.CurrentWeeklyPeriods) / assigned.Count, 1, MidpointRounding.AwayFromZero),
                assigned.Count == 0
                    ? 0m
                    : Math.Round((decimal)enrolledStudents / assigned.Count, 1, MidpointRounding.AwayFromZero),
                byStatus,
                LoadBands(assigned));
        }

        /// <summary>
        /// Assigned teachers bucketed by how full their week is against
        /// <em>their own</em> cap, not a school-wide one: a part-timer capped at 12
        /// periods and teaching 11 is nearly full, and measuring them against a
        /// full-timer's 24 would file them under "light".
        /// <para>
        /// The overloaded band reads <c>IsOverloaded</c> from the read model rather
        /// than recomputing the comparison, so the count here and the count on the
        /// load board are the same count.
        /// </para>
        /// </summary>
        private static IReadOnlyList<StatisticSlice> LoadBands(IReadOnlyList<TeacherLoadRow> assigned)
        {
            var light = 0;
            var moderate = 0;
            var full = 0;
            var over = 0;

            foreach (var row in assigned)
            {
                if (row.IsOverloaded)
                {
                    over++;
                    continue;
                }

                // A teacher with no cap recorded cannot be measured against one.
                // They are counted as full rather than dropped: they are teaching,
                // and a band chart whose bars do not add up to the headcount above
                // it is worse than a rough placement.
                var percent = row.MaxWeeklyPeriods <= 0
                    ? 100m
                    : ChartGeometry.Percent(row.CurrentWeeklyPeriods, row.MaxWeeklyPeriods);

                if (percent < 50m)
                {
                    light++;
                }
                else if (percent < 80m)
                {
                    moderate++;
                }
                else
                {
                    full++;
                }
            }

            return new[]
            {
                new StatisticSlice("Under half", "أقل من النصف", light),
                new StatisticSlice("Half to four-fifths", "بين النصف والأربعة أخماس", moderate),
                new StatisticSlice("Near or at capacity", "قرب النصاب أو عنده", full),
                new StatisticSlice("Over capacity", "فوق النصاب", over),
            };
        }

        // ------------------------------------------------------------------ fees

        /// <summary>doc/Modules/19 §11. Voided charges are excluded throughout — a cancelled charge billed nothing.</summary>
        private async Task<FeeStatistics> FeesAsync(
            int academicYearId, IReadOnlyList<(int Year, int Month)> months, CancellationToken cancellationToken)
        {
            var postedCharges = _db.Charges.AsNoTracking()
                .Where(c => c.AcademicYearId == academicYearId && c.Status == ChargeStatus.Posted);

            var charges = await postedCharges
                .Select(c => new
                {
                    c.FeeCategoryId,
                    c.GrossAmount,
                    c.NetAmount,
                    c.VatAmount,
                    c.PostedAtUtc,
                })
                .ToListAsync(cancellationToken);

            // Credit notes, discount documents and allocations all hang off a charge,
            // so all three are narrowed to this year's charges rather than to a year
            // column they do not carry.
            //
            // Left as an IQueryable rather than a materialized id list, deliberately.
            // Passing the ids back in as a list makes EF inline every one of them into
            // an IN clause: fine for the two rows a test seeds, and a query naming
            // thirty thousand literals three times over for a real school year. Kept
            // as a subquery it stays one small statement whatever the school's size.
            var chargeIds = postedCharges.Select(c => c.Id);

            var creditNotes = (await _db.CreditNotes.AsNoTracking()
                .Where(n => chargeIds.Contains(n.ChargeId))
                .Select(n => n.Amount)
                .ToListAsync(cancellationToken)).Sum();

            var discounts = (await _db.DiscountDocuments.AsNoTracking()
                .Where(d => chargeIds.Contains(d.ChargeId))
                .Select(d => d.Amount)
                .ToListAsync(cancellationToken)).Sum();

            var allocated = (await _db.PaymentAllocations.AsNoTracking()
                .Where(a => chargeIds.Contains(a.ChargeId))
                .Select(a => a.AllocatedAmount)
                .ToListAsync(cancellationToken)).Sum();

            var billed = charges.Select(c => c.GrossAmount).Sum();

            var byCategory = await ByCategoryAsync(
                charges.GroupBy(c => c.FeeCategoryId)
                    .ToDictionary(g => g.Key, g => g.Select(c => c.GrossAmount).Sum()),
                cancellationToken);

            var byMonth = months
                .Select(m => new MonthlyPoint(m.Year, m.Month,
                    charges.Where(c => c.PostedAtUtc.Year == m.Year && c.PostedAtUtc.Month == m.Month)
                        .Select(c => c.GrossAmount).Sum()))
                .ToList();

            return new FeeStatistics(
                billed,
                charges.Select(c => c.NetAmount).Sum(),
                charges.Select(c => c.VatAmount).Sum(),
                discounts,
                creditNotes,
                // BR-FEE-008's own calculator, at year scope: what this screen calls
                // outstanding is exactly what the receivables report calls it.
                StudentFinancialPositionCalculator.Calculate(billed, creditNotes, discounts, allocated),
                charges.Count,
                byCategory,
                byMonth);
        }

        /// <summary>
        /// Billed per fee category, largest first — the one breakdown on this screen
        /// where size is the question being asked.
        /// </summary>
        private async Task<IReadOnlyList<StatisticSlice>> ByCategoryAsync(
            IReadOnlyDictionary<int, decimal> billedByCategoryId, CancellationToken cancellationToken)
        {
            if (billedByCategoryId.Count == 0)
            {
                return Array.Empty<StatisticSlice>();
            }

            var ids = billedByCategoryId.Keys.ToList();

            // The lookup again: a fee category the school retired mid-year still has
            // to name the charges raised under it (SoftActiveLookupTests).
            var categories = await _db.FeeCategories.IgnoreQueryFilters().AsNoTracking()
                .Where(c => c.SchoolId == _db.CurrentSchoolId && ids.Contains(c.Id))
                .Select(c => new { c.Id, c.NameEn, c.NameAr })
                .ToListAsync(cancellationToken);

            return categories
                .Select(c => new StatisticSlice(c.NameEn, c.NameAr, billedByCategoryId[c.Id]))
                .OrderByDescending(s => s.Value)
                .ToList();
        }

        // ------------------------------------------------------------------ payments

        /// <summary>
        /// doc/Modules/21 §11. Receipts carry no academic year — a payment is dated,
        /// not enrolled — so everything here is bounded by the year's calendar dates.
        /// The end date is taken to the end of its day, or a receipt issued on the
        /// last afternoon of the year would fall outside it.
        /// </summary>
        private async Task<PaymentStatistics> PaymentsAsync(
            DateTime startDate, DateTime endDate, FeeStatistics fees,
            IReadOnlyList<(int Year, int Month)> months, CancellationToken cancellationToken)
        {
            var from = startDate.Date;
            var to = endDate.Date.AddDays(1).AddTicks(-1);

            var receipts = await _db.Receipts.AsNoTracking()
                .Where(r => r.Status == ReceiptStatus.Posted && r.IssuedAtUtc >= from && r.IssuedAtUtc <= to)
                .Select(r => new { r.Method, r.Amount, r.IssuedAtUtc })
                .ToListAsync(cancellationToken);

            // Bounded by when the voucher was raised, because a refund records no
            // payout date of its own (doc/Modules/21 §7 — RefundVoucher carries a
            // status, not a paid-on). Raised-in-year is an approximation; an
            // all-time refund total sitting beside a one-year collection total would
            // be the worse one, because a reader subtracts the two.
            var refunded = (await _db.RefundVouchers.AsNoTracking()
                .Where(v => v.Status == RefundVoucherStatus.Paid && v.CreatedAtUtc >= from && v.CreatedAtUtc <= to)
                .Select(v => v.Amount)
                .ToListAsync(cancellationToken)).Sum();

            var collected = receipts.Select(r => r.Amount).Sum();

            var byMethod = receipts
                .GroupBy(r => r.Method)
                .Select(g => new StatisticSlice(
                    PaymentMethodEn(g.Key), PaymentMethodAr(g.Key), g.Select(r => r.Amount).Sum()))
                .OrderByDescending(s => s.Value)
                .ToList();

            // Billed against collected on one axis: the pair is the point of the
            // chart, and reading them off two charts with two ceilings is what makes
            // a bad month look like a good one.
            var byMonth = months
                .Select(m => new MonthlyPair(m.Year, m.Month,
                    fees.ByMonth.FirstOrDefault(p => p.Year == m.Year && p.Month == m.Month)?.Value ?? 0m,
                    receipts.Where(r => r.IssuedAtUtc.Year == m.Year && r.IssuedAtUtc.Month == m.Month)
                        .Select(r => r.Amount).Sum()))
                .ToList();

            return new PaymentStatistics(
                collected,
                refunded,
                receipts.Count,
                receipts.Count == 0 ? 0m : Math.Round(collected / receipts.Count, 2, MidpointRounding.AwayFromZero),
                ChartGeometry.Percent(collected, fees.Billed),
                byMethod,
                byMonth);
        }

        // ------------------------------------------------------------------ expenses

        /// <summary>
        /// What the school spent, from the attached ledger. Null when none is
        /// attached — never a zeroed record, because "spent nothing" and "nobody
        /// asked the books" are different statements and the screen has to be able
        /// to tell the reader which one it is showing.
        /// </summary>
        private async Task<ExpenseStatistics?> ExpensesAsync(
            DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
        {
            if (_ledger == null)
            {
                return null;
            }

            var result = await _ledger.GetResultAsync(startDate.Date, endDate.Date, cancellationToken);

            var monthly = await _ledger.GetMonthlyResultAsync(
                new DateTime(startDate.Year, startDate.Month, 1),
                MonthAxisBuilder.Count(startDate, endDate),
                cancellationToken);

            return new ExpenseStatistics(
                result.Revenue,
                result.Expenses,
                monthly.Select(m => new MonthlyPair(m.Year, m.Month, m.Revenue, m.Expenses)).ToList());
        }

        // ------------------------------------------------------------------ labels and empties

        // Enum text lives here rather than in Sms.Web/Labels.cs because these names
        // travel inside StatisticSlice, which already carries both languages for the
        // school-named rows beside them. A slice whose name needed translating at
        // the view and a slice whose name did not would be two kinds of row in one
        // list, and the view would have to know which was which.
        private static string EnrollmentStatusEn(EnrollmentStatus status) => status switch
        {
            EnrollmentStatus.Active => "Active",
            EnrollmentStatus.Withdrawn => "Withdrawn",
            EnrollmentStatus.Completed => "Completed",
            EnrollmentStatus.Promoted => "Promoted",
            _ => status.ToString(),
        };

        private static string EnrollmentStatusAr(EnrollmentStatus status) => status switch
        {
            EnrollmentStatus.Active => "مقيَّد",
            EnrollmentStatus.Withdrawn => "منسحب",
            EnrollmentStatus.Completed => "أكمل العام",
            EnrollmentStatus.Promoted => "مُرفَّع",
            _ => status.ToString(),
        };

        private static string EmployeeStatusEn(EmployeeStatus status) => status switch
        {
            EmployeeStatus.Active => "Active",
            EmployeeStatus.Suspended => "Suspended",
            EmployeeStatus.Terminated => "Terminated",
            _ => status.ToString(),
        };

        private static string EmployeeStatusAr(EmployeeStatus status) => status switch
        {
            EmployeeStatus.Active => "على رأس العمل",
            EmployeeStatus.Suspended => "موقوف",
            EmployeeStatus.Terminated => "منتهية خدمته",
            _ => status.ToString(),
        };

        private static string PaymentMethodEn(PaymentMethod method) => method switch
        {
            PaymentMethod.Cash => "Cash",
            PaymentMethod.Card => "Card",
            PaymentMethod.BankTransfer => "Bank transfer",
            PaymentMethod.Cheque => "Cheque",
            PaymentMethod.Pdc => "Post-dated cheque",
            _ => method.ToString(),
        };

        private static string PaymentMethodAr(PaymentMethod method) => method switch
        {
            PaymentMethod.Cash => "نقداً",
            PaymentMethod.Card => "بطاقة",
            PaymentMethod.BankTransfer => "حوالة بنكية",
            PaymentMethod.Cheque => "شيك",
            PaymentMethod.Pdc => "شيك آجل",
            _ => method.ToString(),
        };

        private static SchoolStatistics Empty()
        {
            var noSlices = Array.Empty<StatisticSlice>();

            return new SchoolStatistics(
                new StudentStatistics(0, 0, 0, 0, 0, noSlices, noSlices),
                new StaffStatistics(0, 0, 0, 0, 0m, 0m, noSlices, noSlices),
                new FeeStatistics(0m, 0m, 0m, 0m, 0m, 0m, 0, noSlices, Array.Empty<MonthlyPoint>()),
                new PaymentStatistics(0m, 0m, 0, 0m, 0m, noSlices, Array.Empty<MonthlyPair>()),
                null);
        }
    }
}
