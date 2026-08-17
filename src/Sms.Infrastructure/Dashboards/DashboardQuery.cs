using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Attendance;
using Sms.Application.Dashboards;
using Sms.Application.Fees;
using Sms.Application.Portal;
using Sms.Domain.Certificates;
using Sms.Domain.Fees;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Dashboards
{
    /// <summary>Read-only — never calls SaveChangesAsync. Each method reuses its owning module's own calculator (BR-DSH-002's "one computation source" mandate), same discipline as E-304's ParentPortalQuery.</summary>
    public class DashboardQuery : IDashboardQuery
    {
        private readonly AppDbContext _db;

        public DashboardQuery(AppDbContext db)
        {
            _db = db;
        }

        public async Task<SectionAttendanceSnapshot> GetSectionAttendanceTodayAsync(int sectionId, DateTime date, CancellationToken cancellationToken = default)
        {
            var statuses = await _db.AttendanceDays
                .Where(a => a.SectionId == sectionId && a.Date == date.Date)
                .Select(a => a.Status)
                .ToListAsync(cancellationToken);

            var scheduled = statuses.Count;
            var exempted = statuses.Count(PortalAttendanceClassifier.IsExempted);
            var absent = statuses.Count(PortalAttendanceClassifier.IsAbsent);

            return new SectionAttendanceSnapshot
            {
                ScheduledCount = scheduled,
                AbsentCount = absent,
                ExemptedCount = exempted,
                PresentPercent = AttendancePercentageCalculator.Calculate(scheduled, exempted, absent),
            };
        }

        public async Task<decimal> GetSchoolReceivablesTotalAsync(CancellationToken cancellationToken = default)
        {
            // EF Core's Sqlite provider can't translate Sum() over decimal to SQL - materialize then sum in memory
            // (same fix as E-303/E-304's money aggregates).
            var totalCharges = (await _db.Charges.Where(c => c.Status == ChargeStatus.Posted).Select(c => c.GrossAmount).ToListAsync(cancellationToken)).Sum();
            var totalCreditNotes = (await _db.CreditNotes.Select(n => n.Amount).ToListAsync(cancellationToken)).Sum();
            var totalAllocated = (await _db.PaymentAllocations.Select(a => a.AllocatedAmount).ToListAsync(cancellationToken)).Sum();

            return StudentFinancialPositionCalculator.Calculate(totalCharges, totalCreditNotes, totalAllocated);
        }

        public async Task<int> GetPendingCertificateRequestsCountAsync(CancellationToken cancellationToken = default)
            => await _db.CertificateRequests.CountAsync(
                r => r.Status == CertificateRequestStatus.Requested || r.Status == CertificateRequestStatus.Approved, cancellationToken);
    }
}
