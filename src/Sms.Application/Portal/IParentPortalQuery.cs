using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sms.Application.Portal
{
    /// <summary>
    /// S3/E-304 (Portal essentials) read-only aggregation over E-301
    /// Attendance + E-302 Grading + E-303 Fees — a single `requestingUserAccountId`
    /// covers both "parent views a linked child" and "student views own
    /// record" (BR-SEC-011). BR-SEC-010 (portal-vs-staff URL routing, via
    /// <see cref="Sms.Web.Security.PortalAreaFilter"/>) and BR-SEC-013
    /// (re-auth after idle, via <c>PortalReauthAttribute</c>) are now
    /// implemented in the web layer — see <c>PortalController</c> and
    /// <c>Views/Portal/*</c> (built in 4ad850d, extended in a8163a1).
    /// Announcements (read-only, from the WBS's E-304 description) are
    /// wired too, once E-703 (M32 Messaging) landed — see
    /// <c>PortalController.Announcements</c>, which filters to
    /// <c>Status == Sent</c> per BR-SEC-012.
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

        /// <summary>
        /// doc/Modules/37 §8.10 — the work set to this student's section, due
        /// date first. Only issued homework is returned (BR-LRN-003 / BR-SEC-012:
        /// the portal shows finished work only), so a draft the teacher is still
        /// writing is invisible here exactly as it is everywhere else.
        /// Throws <see cref="Common.Exceptions.PortalAccessDeniedException"/> (BR-SEC-011).
        /// </summary>
        Task<IReadOnlyList<PortalSetWork>> GetSetWorkAsync(int requestingUserAccountId, int studentId, CancellationToken cancellationToken = default);

        /// <summary>
        /// doc/Modules/37 §5 ("Student — portal: read content") and §1/§2, which
        /// put lesson plans and the resource library on the portal alongside
        /// homework. The lessons of the offerings this student's grade actually
        /// studies this year, newest week first, each with its scan-clean
        /// material (BR-LRN-006).
        /// <para>
        /// Published only (BR-LRN-003 / BR-SEC-012), which is what
        /// <c>Lesson.PublishedAtUtc</c> has always been documented to mean.
        /// Throws <see cref="Common.Exceptions.PortalAccessDeniedException"/> (BR-SEC-011).
        /// </para>
        /// </summary>
        Task<IReadOnlyList<PortalLesson>> GetPublishedLessonsAsync(int requestingUserAccountId, int studentId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Whether this caller may be served the file behind one lesson resource
        /// — the same BR-SEC-011 gate as every other portal read, asked about a
        /// resource id rather than a student id so the download action does not
        /// have to trust one supplied in the URL. False when the resource does
        /// not exist, is withdrawn, hangs off an unpublished lesson, or belongs
        /// to no student this caller may see.
        /// </summary>
        Task<bool> CanReadLessonResourceAsync(int requestingUserAccountId, int resourceId, CancellationToken cancellationToken = default);
    }
}
