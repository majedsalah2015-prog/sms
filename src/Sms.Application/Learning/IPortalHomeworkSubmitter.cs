using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Learning;

namespace Sms.Application.Learning
{
    /// <summary>
    /// BR-LRN-013 (doc/Modules/37 §8.10) — <b>the first write surface the portal
    /// has ever had</b>. A student hands work in for themselves, and nobody else
    /// hands it in for them.
    ///
    /// <para>
    /// Standalone shape: the submit saves itself. It rides no larger
    /// transaction — the student pressing the button is the whole unit of work.
    /// </para>
    ///
    /// <para>
    /// <b>A separate port from <see cref="IHomeworkSubmissionAdmin"/> on
    /// purpose.</b> BR-LRN-013 says portal writes "widen no staff surface", and
    /// BR-SEC-010's portal/staff separation is the rule this module is most able
    /// to break by accident. One interface with both a teacher's marking methods
    /// and a student's submit on it would be a single object a portal controller
    /// holds and a staff controller holds, and the separation would survive only
    /// as long as nobody called the wrong method.
    /// </para>
    ///
    /// <para>
    /// The identity is the <em>account</em>, not a student id supplied by the
    /// caller: the caller may not name whose work this is. That is what makes "a
    /// parent account may view but never submit on a child's behalf" enforceable
    /// here rather than in a controller that has to remember.
    /// </para>
    /// </summary>
    public interface IPortalHomeworkSubmitter
    {
        /// <summary>
        /// doc/Modules/37 §8.10 — "submit with upload". Supersedes the live
        /// submission and appends a <see cref="SubmissionVersion"/> (BR-LRN-005:
        /// one live row, the prior retained as a version), flags lateness from
        /// <see cref="SubmissionLatenessEvaluator"/> without ever refusing over
        /// it, and moves the homework <c>Issued -> Collecting</c> on the first
        /// hand-in of the class.
        ///
        /// <para>
        /// A resubmission clears any score already entered, and returns the row
        /// to <see cref="SubmissionStatus.Submitted"/>: the mark described work
        /// that has just been replaced, and carrying it forward would release to
        /// Module 17 a mark for something the teacher never saw. BR-LRN-011 then
        /// holds the release until it is re-marked, which is the intended
        /// consequence.
        /// </para>
        ///
        /// <para>
        /// BR-LRN-006: uploads ride the existing attachment pipeline unchanged —
        /// <paramref name="attachmentIds"/> are <c>doc.Attachment</c> rows the
        /// caller has already created through it, typed and size-limited there.
        /// The virus scan gates <em>serving</em>, not accepting: an unscanned
        /// file is taken from the student and simply never shown to the teacher
        /// until it is clean.
        /// </para>
        /// </summary>
        /// <param name="requestingUserAccountId">
        /// The signed-in account. BR-LRN-013: it must be the student's own —
        /// a parent, a staff member or an unlinked account is refused with
        /// <see cref="Common.Exceptions.PortalSubmissionIdentityException"/>.
        /// </param>
        /// <param name="homeworkId">
        /// Must be work set to the section this student currently sits in and
        /// visible in the portal (BR-LRN-003), or
        /// <see cref="Common.Exceptions.HomeworkNotOfferedToStudentException"/> —
        /// which is also what an unknown id returns, so a probe cannot tell the
        /// two apart (BR-SEC-010).
        /// </param>
        /// <param name="textResponse">The typed answer, if any. A hand-in may be files alone.</param>
        /// <param name="attachmentIds">doc.Attachment ids for this hand-in's files, if any.</param>
        /// <exception cref="Common.Exceptions.HomeworkClosedToSubmissionsException">
        /// The homework no longer accepts work (BR-LRN-005 keeps it open past the
        /// due date — only the status closes it).
        /// </exception>
        Task<HomeworkSubmission> SubmitAsync(
            int requestingUserAccountId,
            int homeworkId,
            string? textResponse = null,
            IReadOnlyList<int>? attachmentIds = null,
            CancellationToken cancellationToken = default);
    }
}
