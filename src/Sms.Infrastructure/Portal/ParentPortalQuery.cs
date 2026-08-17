using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Attendance;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Fees;
using Sms.Application.Portal;
using Sms.Domain.Fees;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Portal
{
    /// <summary>Read-only — never calls SaveChangesAsync. Reuses E-301's AttendancePercentageCalculator and E-303's StudentFinancialPositionCalculator rather than recomputing them (BR-ATD-009/BR-FEE-008's "single central computation" mandate).</summary>
    public class ParentPortalQuery : IParentPortalQuery
    {
        private readonly AppDbContext _db;
        private readonly IWorkingYearContext _workingYear;

        public ParentPortalQuery(AppDbContext db, IWorkingYearContext workingYear)
        {
            _db = db;
            _workingYear = workingYear;
        }

        public async Task<IReadOnlyList<PortalChildSummary>> GetVisibleChildrenAsync(int requestingUserAccountId, CancellationToken cancellationToken = default)
        {
            var visibleIds = await GetGuardianVisibleStudentIdsAsync(requestingUserAccountId, cancellationToken);
            if (visibleIds.Count == 0)
            {
                return Array.Empty<PortalChildSummary>();
            }

            var students = await _db.Students.Where(s => visibleIds.Contains(s.Id)).ToListAsync(cancellationToken);
            return students
                .Select(s => new PortalChildSummary { StudentId = s.Id, StudentNo = s.StudentNo, FirstNameAr = s.FirstNameAr, FirstNameEn = s.FirstNameEn })
                .ToList();
        }

        private async Task<IReadOnlyList<int>> GetGuardianVisibleStudentIdsAsync(int requestingUserAccountId, CancellationToken cancellationToken)
        {
            var parent = await _db.Parents.SingleOrDefaultAsync(p => p.UserAccountId == requestingUserAccountId, cancellationToken);
            if (parent == null)
            {
                return Array.Empty<int>();
            }

            var linkRows = await _db.StudentGuardianLinks
                .Where(l => l.ParentId == parent.Id)
                .Select(l => new { l.StudentId, l.IsPortalVisible, l.EffectiveToUtc })
                .ToListAsync(cancellationToken);

            var links = linkRows.Select(l => new GuardianVisibilityEvaluator.GuardianLink(l.StudentId, l.IsPortalVisible, l.EffectiveToUtc));
            return GuardianVisibilityEvaluator.GetVisibleStudentIds(links);
        }

        private async Task EnsureAccessAsync(int requestingUserAccountId, int studentId, CancellationToken cancellationToken)
        {
            var student = await _db.Students.SingleAsync(s => s.Id == studentId, cancellationToken);
            var visibleIds = await GetGuardianVisibleStudentIdsAsync(requestingUserAccountId, cancellationToken);

            if (!PortalAccessEvaluator.CanView(studentId, student.UserAccountId, requestingUserAccountId, visibleIds))
            {
                throw new PortalAccessDeniedException(studentId);
            }
        }

        public async Task<PortalAttendanceSummary> GetAttendanceSummaryAsync(int requestingUserAccountId, int studentId, CancellationToken cancellationToken = default)
        {
            await EnsureAccessAsync(requestingUserAccountId, studentId, cancellationToken);

            var enrollment = await _db.Enrollments
                .Where(e => e.StudentId == studentId && e.AcademicYearId == _workingYear.AcademicYearId)
                .SingleOrDefaultAsync(cancellationToken);
            if (enrollment == null)
            {
                return new PortalAttendanceSummary();
            }

            var statuses = await _db.AttendanceDays.Where(a => a.EnrollmentId == enrollment.Id).Select(a => a.Status).ToListAsync(cancellationToken);
            var scheduled = statuses.Count;
            var exempted = statuses.Count(PortalAttendanceClassifier.IsExempted);
            var absent = statuses.Count(PortalAttendanceClassifier.IsAbsent);

            return new PortalAttendanceSummary
            {
                ScheduledDays = scheduled,
                ExemptedDays = exempted,
                AbsentDays = absent,
                AttendancePercent = AttendancePercentageCalculator.Calculate(scheduled, exempted, absent),
            };
        }

        public async Task<IReadOnlyList<PortalResultSummary>> GetPublishedResultsAsync(int requestingUserAccountId, int studentId, CancellationToken cancellationToken = default)
        {
            await EnsureAccessAsync(requestingUserAccountId, studentId, cancellationToken);

            var enrollment = await _db.Enrollments
                .Where(e => e.StudentId == studentId && e.AcademicYearId == _workingYear.AcademicYearId)
                .SingleOrDefaultAsync(cancellationToken);
            if (enrollment == null)
            {
                return Array.Empty<PortalResultSummary>();
            }

            // TermResult rows only ever exist once a Marksheet publishes (E-302) - BR-SEC-012's "published only" is satisfied by construction, no extra filter needed.
            var results = await _db.TermResults.Where(r => r.EnrollmentId == enrollment.Id).ToListAsync(cancellationToken);
            var bandIds = results.Where(r => r.ScaleBandId.HasValue).Select(r => r.ScaleBandId!.Value).Distinct().ToList();
            var bandCodes = await _db.ScaleBands.Where(b => bandIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.BandCode, cancellationToken);

            return results
                .Select(r => new PortalResultSummary
                {
                    CurriculumOfferingId = r.CurriculumOfferingId,
                    TermId = r.TermId,
                    ScorePercent = r.ScorePercent,
                    BandCode = r.ScaleBandId.HasValue && bandCodes.TryGetValue(r.ScaleBandId.Value, out var code) ? code : null,
                    PublishedAtUtc = r.PublishedAtUtc,
                })
                .ToList();
        }

        public async Task<PortalFeePosition> GetFeePositionAsync(int requestingUserAccountId, int studentId, CancellationToken cancellationToken = default)
        {
            await EnsureAccessAsync(requestingUserAccountId, studentId, cancellationToken);

            // Only Posted charges are ever queried here - BR-SEC-012's "posted invoices only" is satisfied by this filter, matching FeeAdmin.ComputeStudentPositionAsync's own scope.
            var chargeRows = await _db.Charges
                .Where(c => c.StudentId == studentId && c.Status == ChargeStatus.Posted)
                .Select(c => new { c.Id, c.ChargeNo, c.GrossAmount, c.PostedAtUtc })
                .ToListAsync(cancellationToken);
            var chargeIds = chargeRows.Select(c => c.Id).ToList();

            // EF Core's Sqlite provider can't translate Sum() over decimal to SQL - materialize then sum in memory (same fix as E-303's FeeAdmin/PaymentAdmin).
            var totalCharges = chargeRows.Sum(c => c.GrossAmount);
            var totalCreditNotes = (await _db.CreditNotes.Where(n => chargeIds.Contains(n.ChargeId)).Select(n => n.Amount).ToListAsync(cancellationToken)).Sum();
            var totalDiscounts = (await _db.DiscountDocuments.Where(d => chargeIds.Contains(d.ChargeId)).Select(d => d.Amount).ToListAsync(cancellationToken)).Sum();
            var totalAllocated = (await _db.PaymentAllocations.Where(a => chargeIds.Contains(a.ChargeId)).Select(a => a.AllocatedAmount).ToListAsync(cancellationToken)).Sum();

            return new PortalFeePosition
            {
                Position = StudentFinancialPositionCalculator.Calculate(totalCharges, totalCreditNotes, totalDiscounts, totalAllocated),
                GrossCharges = totalCharges,
                Discounts = totalDiscounts,
                Charges = chargeRows.Select(c => new PortalChargeLine { ChargeNo = c.ChargeNo, GrossAmount = c.GrossAmount, PostedAtUtc = c.PostedAtUtc }).ToList(),
            };
        }
    }
}
