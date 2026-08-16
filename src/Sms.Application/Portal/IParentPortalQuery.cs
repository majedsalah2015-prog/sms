using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sms.Application.Portal
{
    /// <summary>
    /// S3/E-304 (Portal essentials) read-only aggregation over E-301
    /// Attendance + E-302 Grading + E-303 Fees — a single `requestingUserAccountId`
    /// covers both "parent views a linked child" and "student views own
    /// record" (BR-SEC-011). BR-SEC-010 (portal-vs-staff URL routing) and
    /// BR-SEC-013 (re-auth after idle) are web-layer/screen concerns —
    /// deferred, no real Razor views exist yet, same posture as every
    /// other epic. Announcements (read-only, from the WBS's E-304
    /// description) are deferred entirely — no Messaging/Announcement
    /// module exists yet (M32 is S6/S7 territory).
    /// </summary>
    public interface IParentPortalQuery
    {
        /// <summary>BR-PAR-004: a parent's family view. Empty for a caller with no Parent record.</summary>
        Task<IReadOnlyList<PortalChildSummary>> GetVisibleChildrenAsync(int requestingUserAccountId, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.PortalAccessDeniedException"/> (BR-SEC-011).</summary>
        Task<PortalAttendanceSummary> GetAttendanceSummaryAsync(int requestingUserAccountId, int studentId, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.PortalAccessDeniedException"/> (BR-SEC-011).</summary>
        Task<IReadOnlyList<PortalResultSummary>> GetPublishedResultsAsync(int requestingUserAccountId, int studentId, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.PortalAccessDeniedException"/> (BR-SEC-011).</summary>
        Task<PortalFeePosition> GetFeePositionAsync(int requestingUserAccountId, int studentId, CancellationToken cancellationToken = default);
    }
}
