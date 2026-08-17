using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sms.Application.Dashboards
{
    /// <summary>
    /// A handful of real widget computations, each reusing its owning
    /// module's own calculator so BR-DSH-002's "one computation source"
    /// holds by construction — not a general widget-data framework (the
    /// full registry-to-data wiring is Phase 9/10 content work).
    /// </summary>
    public interface IDashboardQuery
    {
        /// <summary>Reuses E-301's AttendancePercentageCalculator.</summary>
        Task<SectionAttendanceSnapshot> GetSectionAttendanceTodayAsync(int sectionId, DateTime date, CancellationToken cancellationToken = default);

        /// <summary>Reuses E-303's StudentFinancialPositionCalculator at school-wide scope (BR-FEE-008).</summary>
        Task<decimal> GetSchoolReceivablesTotalAsync(CancellationToken cancellationToken = default);

        /// <summary>Requested + Approved certificate requests not yet Issued (BR-CRT-003's WF-09 queue).</summary>
        Task<int> GetPendingCertificateRequestsCountAsync(CancellationToken cancellationToken = default);
    }
}
