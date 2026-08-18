using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Attendance;
using Sms.Application.Common.Interfaces;
using Sms.Application.Installments;
using Sms.Application.Portal;
using Sms.Application.ReadModels;
using Sms.Domain.Fees;
using Sms.Domain.Installments;
using Sms.Domain.ReadModels;
using Sms.Domain.Sections;
using Sms.Domain.Students;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.ReadModels
{
    /// <summary>
    /// S8/E-802 — DB/04 §4 snapshot tables. Each refresh reads the hot tables once, aggregates in memory,
    /// then replaces the school's rows in one save (heavy reports then read the snapshot, never the hot
    /// tables — NF-P5). Numbers reuse the owning module's calculators; the aged-receivables math is the
    /// same charge − credits − discounts − allocations remainder every position reader in this codebase uses.
    /// </summary>
    public class SnapshotRefreshService : ISnapshotRefreshService
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly IInstallmentAdmin _installments;

        public SnapshotRefreshService(AppDbContext db, IClock clock, IInstallmentAdmin installments)
        {
            _db = db;
            _clock = clock;
            _installments = installments;
        }

        public async Task<int> RefreshAgedReceivablesAsync(int currentDays = ReceivablesAgingBucketer.DefaultCurrentDays, CancellationToken cancellationToken = default)
        {
            var asOf = _clock.UtcNow;
            var charges = await _db.Charges.Where(c => c.Status == ChargeStatus.Posted)
                .Select(c => new { c.Id, c.StudentId, c.PayerId, c.AcademicYearId, c.GrossAmount, c.PostedAtUtc }).ToListAsync(cancellationToken);
            // Whole-school read: join in memory rather than Contains(list) (Sqlite parameter cap); Sqlite can't Sum() decimals in SQL.
            var credited = (await _db.CreditNotes.Select(n => new { n.ChargeId, n.Amount }).ToListAsync(cancellationToken)).GroupBy(x => x.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
            var discounted = (await _db.DiscountDocuments.Select(d => new { d.ChargeId, d.Amount }).ToListAsync(cancellationToken)).GroupBy(x => x.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
            var allocated = (await _db.PaymentAllocations.Select(a => new { a.ChargeId, a.AllocatedAmount }).ToListAsync(cancellationToken)).GroupBy(x => x.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.AllocatedAmount));
            var profileByStudentYear = (await _db.Enrollments.Where(e => e.Status == EnrollmentStatus.Active)
                .Select(e => new { e.StudentId, e.AcademicYearId, e.GradeYearProfileId }).ToListAsync(cancellationToken))
                .GroupBy(e => (e.StudentId, e.AcademicYearId)).ToDictionary(g => g.Key, g => g.First().GradeYearProfileId);

            var rows = new Dictionary<(int Payer, int Student, int Year), AgedReceivablesSnapshot>();
            foreach (var c in charges)
            {
                var remaining = c.GrossAmount
                    - (credited.TryGetValue(c.Id, out var cr) ? cr : 0m)
                    - (discounted.TryGetValue(c.Id, out var di) ? di : 0m)
                    - (allocated.TryGetValue(c.Id, out var al) ? al : 0m);
                if (remaining <= 0m)
                {
                    continue;
                }

                var key = (c.PayerId, c.StudentId, c.AcademicYearId);
                if (!rows.TryGetValue(key, out var row))
                {
                    row = new AgedReceivablesSnapshot
                    {
                        AsOfUtc = asOf, PayerId = c.PayerId, StudentId = c.StudentId, AcademicYearId = c.AcademicYearId,
                        GradeYearProfileId = profileByStudentYear.TryGetValue((c.StudentId, c.AcademicYearId), out var p) ? p : (int?)null,
                    };
                    rows[key] = row;
                }

                switch (ReceivablesAgingBucketer.Bucket(c.PostedAtUtc, asOf, currentDays))
                {
                    case AgingBucket.Current: row.Current += remaining; break;
                    case AgingBucket.Days1To30: row.Days1To30 += remaining; break;
                    case AgingBucket.Days31To60: row.Days31To60 += remaining; break;
                    case AgingBucket.Days61To90: row.Days61To90 += remaining; break;
                    default: row.Over90 += remaining; break;
                }

                row.Total += remaining;
            }

            await ReplaceAsync(_db.AgedReceivablesSnapshots, rows.Values.OrderBy(r => r.PayerId).ThenBy(r => r.StudentId).ThenBy(r => r.AcademicYearId), cancellationToken);
            return rows.Count;
        }

        public async Task<int> RefreshDailyAttendanceSummaryAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            var asOf = _clock.UtcNow;
            var day = date.Date;
            var captures = await _db.AttendanceDays.Where(d => d.Date == day)
                .Select(d => new { d.SectionId, d.AcademicYearId, d.Status }).ToListAsync(cancellationToken);
            var sectionIds = captures.Select(c => c.SectionId).Distinct().ToList();
            var sections = await (
                from s in _db.Sections
                join p in _db.GradeYearProfiles on s.GradeYearProfileId equals p.Id
                join g in _db.GradeLevels on p.GradeLevelId equals g.Id
                where sectionIds.Contains(s.Id)
                select new { s.Id, ProfileId = p.Id, g.StageId }).ToDictionaryAsync(x => x.Id, cancellationToken);

            var rows = captures.GroupBy(c => c.SectionId).Select(g =>
            {
                var scheduled = g.Count();
                var exempted = g.Count(c => PortalAttendanceClassifier.IsExempted(c.Status));
                var absent = g.Count(c => PortalAttendanceClassifier.IsAbsent(c.Status));
                var meta = sections[g.Key];
                return new DailyAttendanceSummarySnapshot
                {
                    AsOfUtc = asOf, Date = day, AcademicYearId = g.First().AcademicYearId, SectionId = g.Key, GradeYearProfileId = meta.ProfileId, StageId = meta.StageId,
                    ScheduledCount = scheduled, AbsentCount = absent, ExemptedCount = exempted,
                    LateCount = g.Count(c => c.Status == Domain.Attendance.AttendanceStatus.Late),
                    PresentPercent = AttendancePercentageCalculator.Calculate(scheduled, exempted, absent),
                };
            }).OrderBy(r => r.SectionId).ToList();

            // Only this date's rows are replaced — the table keeps one row per (date, section) across days.
            // Loaded (not stubbed) so a re-run in the same context resolves against rows it just added.
            _db.DailyAttendanceSummarySnapshots.RemoveRange(await _db.DailyAttendanceSummarySnapshots.Where(s => s.Date == day).ToListAsync(cancellationToken));
            _db.DailyAttendanceSummarySnapshots.AddRange(rows);
            await _db.SaveChangesAsync(cancellationToken);
            return rows.Count;
        }

        public async Task<int> RefreshCollectionCalendarAsync(int horizonDays = 120, int lookbackDays = 90, CancellationToken cancellationToken = default)
        {
            var asOf = _clock.UtcNow;
            var from = asOf.Date.AddDays(-lookbackDays);
            var to = asOf.Date.AddDays(horizonDays);
            var assignments = await _db.PlanAssignments
                .Where(a => a.Installments.Any(i => !i.IsSuperseded && !i.IsWrittenOff && i.DueDate >= from && i.DueDate <= to))
                .Select(a => new { a.Id, a.AcademicYearId }).ToListAsync(cancellationToken);

            var rows = new Dictionary<(DateTime Due, int Year), CollectionCalendarSnapshot>();
            foreach (var a in assignments)
            {
                // BR-INS-007: status/paid are derived, never stored — reuse Module 20's own schedule reader (one computation source).
                var schedule = await _installments.GetScheduleAsync(a.Id, cancellationToken);
                foreach (var i in schedule.Where(i => i.Status != InstallmentStatus.Rescheduled && i.Status != InstallmentStatus.WrittenOff && i.DueDate >= from && i.DueDate <= to))
                {
                    var key = (i.DueDate.Date, a.AcademicYearId);
                    if (!rows.TryGetValue(key, out var row))
                    {
                        row = new CollectionCalendarSnapshot { AsOfUtc = asOf, DueDate = i.DueDate.Date, AcademicYearId = a.AcademicYearId };
                        rows[key] = row;
                    }

                    row.InstallmentCount++;
                    row.ScheduledAmount += i.Amount;
                    row.PaidAmount += i.Paid;
                    row.OutstandingAmount += i.Amount - i.Paid;
                    if (i.Status == InstallmentStatus.Overdue)
                    {
                        row.OverdueCount++;
                    }
                }
            }

            await ReplaceAsync(_db.CollectionCalendarSnapshots, rows.Values.OrderBy(r => r.DueDate).ThenBy(r => r.AcademicYearId), cancellationToken);
            return rows.Count;
        }

        /// <summary>Wholesale replace of the school's rows (the tenant filter scopes the delete) in one save.</summary>
        private async Task ReplaceAsync<T>(DbSet<T> set, IEnumerable<T> fresh, CancellationToken ct) where T : SnapshotRow
        {
            set.RemoveRange(await set.ToListAsync(ct));   // loaded, not stubbed: safe when the same context refreshed earlier
            set.AddRange(fresh);
            await _db.SaveChangesAsync(ct);
        }
    }
}
