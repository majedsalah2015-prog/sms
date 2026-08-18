using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Attendance;
using Sms.Application.Cafeteria;
using Sms.Application.Fees;
using Sms.Application.Grades;
using Sms.Application.Portal;
using Sms.Application.ReadModels;
using Sms.Application.Teachers;
using Sms.Domain.Admissions;
using Sms.Domain.Fees;
using Sms.Domain.Sections;
using Sms.Domain.Students;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.ReadModels
{
    /// <summary>
    /// S8/E-802 — DB/04 §4 "views" over EF. Every number comes from the owning module's calculator
    /// (BR-DSH-002 / BR-FEE-008 one computation source). Money aggregates materialize then sum in memory
    /// (EF Core Sqlite can't translate Sum() over decimal — see build conventions); whole-school reads avoid
    /// `Contains(list)` (Sqlite's ~999-parameter cap) by joining in memory instead.
    /// </summary>
    public class ReadModelQuery : IReadModelQuery
    {
        private static readonly ApplicationStatus[] PipelineStatuses =
        {
            ApplicationStatus.Submitted, ApplicationStatus.UnderReview, ApplicationStatus.Recommended, ApplicationStatus.Approved, ApplicationStatus.Waitlisted,
        };

        private readonly AppDbContext _db;

        public ReadModelQuery(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<StudentPositionRow>> GetStudentPositionsAsync(int? studentId = null, CancellationToken cancellationToken = default)
        {
            var chargesQuery = _db.Charges.Where(c => c.Status == ChargeStatus.Posted);
            if (studentId != null)
            {
                chargesQuery = chargesQuery.Where(c => c.StudentId == studentId.Value);
            }

            var charges = await chargesQuery.Select(c => new { c.Id, c.StudentId, c.PayerId, c.GrossAmount }).ToListAsync(cancellationToken);
            var byCharge = charges.ToDictionary(c => c.Id, c => (c.StudentId, c.PayerId));

            List<(int ChargeId, decimal Amount)> notes, discounts, allocations;
            if (studentId != null)
            {
                var ids = charges.Select(c => c.Id).ToList();
                notes = (await _db.CreditNotes.Where(n => ids.Contains(n.ChargeId)).Select(n => new { n.ChargeId, n.Amount }).ToListAsync(cancellationToken)).Select(x => (x.ChargeId, x.Amount)).ToList();
                discounts = (await _db.DiscountDocuments.Where(d => ids.Contains(d.ChargeId)).Select(d => new { d.ChargeId, d.Amount }).ToListAsync(cancellationToken)).Select(x => (x.ChargeId, x.Amount)).ToList();
                allocations = (await _db.PaymentAllocations.Where(a => ids.Contains(a.ChargeId)).Select(a => new { a.ChargeId, Amount = a.AllocatedAmount }).ToListAsync(cancellationToken)).Select(x => (x.ChargeId, x.Amount)).ToList();
            }
            else
            {
                notes = (await _db.CreditNotes.Select(n => new { n.ChargeId, n.Amount }).ToListAsync(cancellationToken)).Select(x => (x.ChargeId, x.Amount)).ToList();
                discounts = (await _db.DiscountDocuments.Select(d => new { d.ChargeId, d.Amount }).ToListAsync(cancellationToken)).Select(x => (x.ChargeId, x.Amount)).ToList();
                allocations = (await _db.PaymentAllocations.Select(a => new { a.ChargeId, Amount = a.AllocatedAmount }).ToListAsync(cancellationToken)).Select(x => (x.ChargeId, x.Amount)).ToList();
            }

            var rows = new Dictionary<(int, int), (decimal C, decimal N, decimal D, decimal A)>();
            foreach (var c in charges)
            {
                var key = (c.StudentId, c.PayerId);
                rows.TryGetValue(key, out var acc);
                rows[key] = (acc.C + c.GrossAmount, acc.N, acc.D, acc.A);
            }

            void Accumulate(IEnumerable<(int ChargeId, decimal Amount)> docs, int slot)
            {
                foreach (var (chargeId, amount) in docs)
                {
                    if (!byCharge.TryGetValue(chargeId, out var key))
                    {
                        continue;   // a note/allocation against a voided or foreign-scope charge — not a receivable
                    }

                    var acc = rows[key];
                    rows[key] = slot switch
                    {
                        1 => (acc.C, acc.N + amount, acc.D, acc.A),
                        2 => (acc.C, acc.N, acc.D + amount, acc.A),
                        _ => (acc.C, acc.N, acc.D, acc.A + amount),
                    };
                }
            }

            Accumulate(notes, 1);
            Accumulate(discounts, 2);
            Accumulate(allocations, 3);

            return rows.OrderBy(r => r.Key.Item1).ThenBy(r => r.Key.Item2)
                .Select(r => new StudentPositionRow(r.Key.Item1, r.Key.Item2, r.Value.C, r.Value.N, r.Value.D, r.Value.A,
                    StudentFinancialPositionCalculator.Calculate(r.Value.C, r.Value.N, r.Value.D, r.Value.A)))
                .ToList();
        }

        public async Task<IReadOnlyList<AttendanceRateRow>> GetAttendanceRatesAsync(int sectionId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            var days = await _db.AttendanceDays
                .Where(d => d.SectionId == sectionId && d.Date >= from.Date && d.Date <= to.Date)
                .Select(d => new { d.EnrollmentId, d.Status }).ToListAsync(cancellationToken);

            return days.GroupBy(d => d.EnrollmentId).OrderBy(g => g.Key).Select(g =>
            {
                var scheduled = g.Count();
                var exempted = g.Count(d => PortalAttendanceClassifier.IsExempted(d.Status));
                var absent = g.Count(d => PortalAttendanceClassifier.IsAbsent(d.Status));
                return new AttendanceRateRow(g.Key, scheduled, exempted, absent, AttendancePercentageCalculator.Calculate(scheduled, exempted, absent));
            }).ToList();
        }

        public async Task<IReadOnlyList<TeacherLoadRow>> GetTeacherLoadsAsync(int academicYearId, CancellationToken cancellationToken = default)
        {
            var loads = await (
                from a in _db.TeacherAssignments
                join o in _db.CurriculumOfferings on a.CurriculumOfferingId equals o.Id
                where a.AcademicYearId == academicYearId && a.EffectiveToUtc == null
                select new { a.TeacherProfileId, o.WeeklyPeriods }).ToListAsync(cancellationToken);
            var profiles = await _db.TeacherProfiles.Select(p => new { p.Id, p.EmployeeId, p.MaxWeeklyPeriods }).ToListAsync(cancellationToken);
            var byTeacher = loads.GroupBy(l => l.TeacherProfileId).ToDictionary(g => g.Key, g => g.Select(x => x.WeeklyPeriods).ToArray());

            return profiles.OrderBy(p => p.Id).Select(p =>
            {
                var current = TeacherLoadCalculator.CurrentLoad(byTeacher.TryGetValue(p.Id, out var periods) ? periods : Array.Empty<int>());
                return new TeacherLoadRow(p.Id, p.EmployeeId, current, p.MaxWeeklyPeriods, TeacherLoadCalculator.ExceedsMax(current, 0, p.MaxWeeklyPeriods));
            }).ToList();
        }

        public async Task<IReadOnlyList<SeatUtilizationRow>> GetSeatUtilizationAsync(int academicYearId, CancellationToken cancellationToken = default)
        {
            var profiles = await _db.GradeYearProfiles.Where(p => p.AcademicYearId == academicYearId)
                .Select(p => new { p.Id, p.TargetSections, p.TargetSectionSize }).ToListAsync(cancellationToken);
            var capacity = (await _db.Sections.Where(s => s.AcademicYearId == academicYearId && s.Status == SectionStatus.Active)
                .Select(s => new { s.GradeYearProfileId, s.Capacity }).ToListAsync(cancellationToken))
                .GroupBy(s => s.GradeYearProfileId).ToDictionary(g => g.Key, g => g.Sum(s => s.Capacity));
            var enrolled = (await _db.Enrollments.Where(e => e.AcademicYearId == academicYearId && e.Status == EnrollmentStatus.Active)
                .Select(e => e.GradeYearProfileId).ToListAsync(cancellationToken))
                .GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
            var pipeline = (await (
                from a in _db.Applications
                join c in _db.AdmissionCampaigns on a.CampaignId equals c.Id
                where c.AcademicYearId == academicYearId && PipelineStatuses.Contains(a.Status)
                select c.GradeYearProfileId).ToListAsync(cancellationToken))
                .GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

            return profiles.OrderBy(p => p.Id).Select(p =>
            {
                var planned = GradeCapacityCalculator.PlannedSeats(p.TargetSections, p.TargetSectionSize);
                var taken = enrolled.TryGetValue(p.Id, out var e) ? e : 0;
                return new SeatUtilizationRow(p.Id, planned, capacity.TryGetValue(p.Id, out var cap) ? cap : 0, taken,
                    pipeline.TryGetValue(p.Id, out var pl) ? pl : 0, planned - taken);
            }).ToList();
        }

        public async Task<IReadOnlyList<WalletBalanceRow>> GetWalletBalancesAsync(CancellationToken cancellationToken = default)
        {
            var wallets = await _db.Wallets.Select(w => new { w.Id, w.HolderKind, w.HolderId }).ToListAsync(cancellationToken);
            var ledger = (await _db.WalletLedgerEntries.Select(e => new { e.WalletId, e.Amount }).ToListAsync(cancellationToken))
                .GroupBy(e => e.WalletId).ToDictionary(g => g.Key, g => g.Select(e => e.Amount).ToList());

            return wallets.OrderBy(w => w.Id)
                .Select(w => new WalletBalanceRow(w.Id, w.HolderKind, w.HolderId, WalletBalanceCalculator.Balance(ledger.TryGetValue(w.Id, out var amounts) ? amounts : new List<decimal>())))
                .ToList();
        }
    }
}
