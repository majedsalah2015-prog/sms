using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Learning;

namespace Sms.Application.Learning
{
    /// <summary>
    /// doc/Modules/37 §8.3 — the homework desk.
    ///
    /// Standalone shape: every method saves itself, like <see cref="ILessonAdmin"/>.
    ///
    /// <para>
    /// Reach (BR-LRN-002) and the issue gate (BR-LRN-004) are both enforced here
    /// rather than in the controller, because "who may set work for which class"
    /// and "what must be true before a class is told to do it" are business
    /// rules, and a second caller must not be able to skip either. The caller
    /// supplies only the school-wide flag, which comes from its permission
    /// check; this service resolves placements, the department, the academic
    /// year's bounds and the school calendar itself.
    /// </para>
    /// </summary>
    public interface IHomeworkAdmin
    {
        /// <summary>
        /// BR-LRN-002: the (offering, section) pairs this user may issue work to,
        /// so the desk offers only what it will accept. One resolution used by
        /// both the picker and the guard is what keeps an offered option from
        /// being refused on submit.
        /// </summary>
        Task<IReadOnlyList<PlacementReach>> ReachableSectionsAsync(
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// doc/Modules/37 §8.3. Creates a Draft homework. BR-GLB-031 — a draft
        /// affects nothing and is invisible in the portal, so it is deliberately
        /// allowed to be incomplete; BR-LRN-004's requirements are checked at
        /// issue, not here.
        /// Throws <see cref="Common.Exceptions.TeachingReachException"/> when the
        /// author holds neither the (offering, section) placement nor the
        /// department (BR-LRN-002).
        /// </summary>
        Task<Homework> CreateAsync(
            int curriculumOfferingId,
            int sectionId,
            string titleAr,
            string titleEn,
            DateTime dueDate,
            string? instructionsAr = null,
            string? instructionsEn = null,
            decimal? maxMarks = null,
            int? blueprintComponentId = null,
            LatenessPolicy latenessPolicy = LatenessPolicy.AcceptWithoutPenalty,
            decimal? latePenaltyPercent = null,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Edits a Draft or Issued homework. Editing after issue is allowed on
        /// purpose — a teacher correcting a typo in the instructions the morning
        /// after setting the work is ordinary, and BR-LRN-016 reserves the heavy
        /// path (withdrawal, with a reason the class is told) for actually taking
        /// the work back.
        /// Throws <see cref="Common.Exceptions.HomeworkTransitionException"/> once
        /// the homework is Released or Withdrawn, and
        /// <see cref="Common.Exceptions.TeachingReachException"/> without reach.
        /// </summary>
        Task<Homework> UpdateAsync(
            int homeworkId,
            string titleAr,
            string titleEn,
            DateTime dueDate,
            string? instructionsAr = null,
            string? instructionsEn = null,
            decimal? maxMarks = null,
            int? blueprintComponentId = null,
            LatenessPolicy latenessPolicy = LatenessPolicy.AcceptWithoutPenalty,
            decimal? latePenaltyPercent = null,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-LRN-003/004: issue is the event the section's families see. Applies
        /// the full gate — a graded homework must name its blueprint component,
        /// an ungraded one must not, and the due date must fall inside the
        /// academic year (BR-GLB-051) on a working day (BR-GLB-052). Stamps
        /// <c>IssuedAtUtc</c>.
        /// Throws <see cref="Common.Exceptions.HomeworkIssueRefusedException"/>
        /// carrying the specific reason, and
        /// <see cref="Common.Exceptions.HomeworkTransitionException"/> unless the
        /// homework is Draft.
        /// </summary>
        Task IssueAsync(int homeworkId, bool hasSchoolWideReach = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-LRN-016: work is withdrawn with a stated reason, never deleted,
        /// because anyone who already submitted is told why (§12
        /// <c>HomeworkWithdrawn</c>).
        /// Throws <see cref="Common.Exceptions.HomeworkWithdrawalBlockedException"/>
        /// when the due date has passed and submissions exist (§9), and
        /// <see cref="Common.Exceptions.HomeworkTransitionException"/> from a
        /// terminal state.
        /// </summary>
        Task WithdrawAsync(int homeworkId, string reason, bool hasSchoolWideReach = false, CancellationToken cancellationToken = default);
    }
}
