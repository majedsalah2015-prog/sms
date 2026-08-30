using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Learning;

namespace Sms.Application.Learning
{
    /// <summary>
    /// doc/Modules/37 §8.1-2 — the lesson planner and its resource library.
    ///
    /// Standalone shape: every method saves itself. Content authoring does not
    /// ride a larger transaction the way number issuing or a workflow final
    /// effect does.
    ///
    /// Reach (BR-LRN-002) is enforced here rather than in the controller,
    /// because "who may put this in front of which students" is a business rule
    /// and a second caller must not be able to skip it. The caller supplies the
    /// school-wide flag from its permission check; this service resolves the
    /// teacher's own placements from the published timetable version.
    /// </summary>
    public interface ILessonAdmin
    {
        /// <summary>
        /// BR-LRN-002: the offerings this user may author against, so the planner
        /// offers only what it will accept. The screen must not have to re-derive
        /// reach — one resolution, used by both the picker and the guard, is what
        /// keeps an offered option from being refused on submit.
        /// </summary>
        Task<IReadOnlyList<int>> ReachableOfferingIdsAsync(bool hasSchoolWideReach = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// doc/Modules/37 §8.1. Creates a Draft lesson (BR-LRN-003 — a draft
        /// affects nothing and is invisible in the portal).
        /// Throws <see cref="Common.Exceptions.TeachingReachException"/> when the
        /// author holds no reach over the offering (BR-LRN-002), and
        /// <see cref="Common.Exceptions.LessonSessionMismatchException"/> when
        /// <paramref name="sessionId"/> names a session that does not teach this
        /// offering (BR-LRN-001).
        /// </summary>
        Task<Lesson> CreateAsync(
            int curriculumOfferingId,
            int weekNumber,
            string titleAr,
            string titleEn,
            string? objectivesAr = null,
            string? objectivesEn = null,
            int? sessionId = null,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Edits a Draft or Published lesson in place.
        /// Throws <see cref="Common.Exceptions.LessonRetiredException"/> once the
        /// lesson is retired (BR-LRN-016) and
        /// <see cref="Common.Exceptions.TeachingReachException"/> without reach.
        /// </summary>
        Task<Lesson> UpdateAsync(
            int lessonId,
            int weekNumber,
            string titleAr,
            string titleEn,
            string? objectivesAr = null,
            string? objectivesEn = null,
            int? sessionId = null,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-LRN-003: publication is the event families see. Stamps
        /// <c>PublishedAtUtc</c>.
        /// Throws <see cref="Common.Exceptions.LessonTransitionException"/> unless
        /// the lesson is Draft.
        /// </summary>
        Task PublishAsync(int lessonId, bool hasSchoolWideReach = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-LRN-016: content is retired, never deleted, and the reason is
        /// mandatory because a student who read it yesterday will ask.
        /// Throws <see cref="Common.Exceptions.LessonTransitionException"/> if it
        /// is already retired.
        /// </summary>
        Task RetireAsync(int lessonId, string reason, bool hasSchoolWideReach = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// doc/Modules/37 §8.2. Hangs an already-uploaded doc.Attachment on the
        /// lesson. The attachment's own pipeline owns typing, size and scanning
        /// (BR-LRN-006); this only links and orders.
        /// </summary>
        Task<LessonResource> AttachResourceAsync(
            int lessonId,
            int attachmentId,
            string titleAr,
            string titleEn,
            int displayOrder = 0,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-GLB-005/BR-LRN-016: a mis-attached file is withdrawn from the
        /// lesson, never hard-deleted — the row stays, deactivated.
        /// </summary>
        Task WithdrawResourceAsync(int lessonResourceId, bool hasSchoolWideReach = false, CancellationToken cancellationToken = default);
    }
}
