using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sms.Application.Learning
{
    /// <summary>
    /// doc/Modules/37 §8.4 (submission tracker) and §8.5 (marking queue) — the
    /// teacher's half of the homework loop, from "who has handed in" to "these
    /// marks are Module 17's now".
    ///
    /// Standalone shape: every method saves itself, like <see cref="IHomeworkAdmin"/>.
    ///
    /// <para>
    /// Reach (BR-LRN-002) is enforced here rather than in the controller, for
    /// <see cref="IHomeworkAdmin"/>'s reason: "whose class's work may I read and
    /// mark" is a business rule, and a second caller must not be able to skip
    /// it. The caller supplies only the school-wide flag, which comes from its
    /// permission check; this service resolves the placements and the department
    /// itself — through <see cref="IHomeworkAdmin.ReachableSectionsAsync"/>, so
    /// the desk and the marking queue can never disagree about who reaches what.
    /// </para>
    ///
    /// <para>
    /// The student's half — actually handing work in — is deliberately a
    /// different port, <see cref="IPortalHomeworkSubmitter"/>: BR-LRN-013 says
    /// portal writes "widen no staff surface", and a portal write reached through
    /// a staff service is exactly the widening it forbids.
    /// </para>
    /// </summary>
    public interface IHomeworkSubmissionAdmin
    {
        /// <summary>
        /// doc/Modules/37 §8.4 — the tracker: <b>every student in the homework's
        /// section</b>, with submitted / late / missing and the score, ordered by
        /// name. Missing students are rows with no submission, not absent rows;
        /// that is what the screen exists to show.
        /// Throws <see cref="Common.Exceptions.TeachingReachException"/> without
        /// reach over the homework's (offering, section) pair (BR-LRN-002).
        /// </summary>
        Task<IReadOnlyList<HomeworkRosterRow>> RosterAsync(
            int homeworkId,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// doc/Modules/37 §4 — closes the homework to the teacher's queue and
        /// opens marking (<c>Issued</c>/<c>Collecting</c> -> <c>Marking</c>).
        ///
        /// <para>
        /// Present because release is otherwise unreachable: the §4 spine passes
        /// through <c>Marking</c>, <see cref="ReleaseAsync"/> refuses from
        /// anywhere else, and no service in the module moved a homework into it.
        /// It belongs to §8.5's marking queue rather than to the desk, which is
        /// why it is here and not on <see cref="IHomeworkAdmin"/>.
        /// </para>
        ///
        /// <para>
        /// BR-LRN-005 is unaffected: this closes the <em>queue</em>, and late
        /// work handed in afterwards is a different question — one this status
        /// answers by refusing, exactly as
        /// <c>HomeworkStatusTransitions.AcceptsSubmissions</c> has always said it
        /// would.
        /// </para>
        /// Throws <see cref="Common.Exceptions.HomeworkTransitionException"/> from
        /// any other state, and
        /// <see cref="Common.Exceptions.TeachingReachException"/> without reach.
        /// </summary>
        Task BeginMarkingAsync(
            int homeworkId,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// doc/Modules/37 §8.5 — enter a mark and feedback for one hand-in.
        ///
        /// <para>
        /// <paramref name="score"/> is what the teacher typed, out of the
        /// homework's max marks. BR-LRN-005's lateness penalty is applied
        /// <em>here</em> — at marking, never automatically at submit — so the
        /// value stored and later handed to Module 17 is the mark that counts.
        /// Null clears the mark back to unscored, which BR-LRN-011 then blocks
        /// the release on; feedback alone with no score is legitimate and is the
        /// normal path for ungraded practice (BR-LRN-004).
        /// </para>
        /// Throws <see cref="Common.Exceptions.SubmissionScoreOutOfRangeException"/>
        /// for a score below zero, above the homework's max marks, or entered at
        /// all against ungraded practice;
        /// <see cref="Common.Exceptions.SubmissionMarkingClosedException"/> once
        /// the homework is Released (the mark is Module 17's — BR-LRN-012) or
        /// Withdrawn; and
        /// <see cref="Common.Exceptions.TeachingReachException"/> without reach.
        /// </summary>
        Task ScoreAsync(
            int submissionId,
            decimal? score,
            string? feedback,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-LRN-012 (doc/Modules/37 §8.5 "bulk-release") — writes each marked
        /// hand-in's <b>raw mark</b> into Module 17's marksheet against the
        /// homework's <c>BlueprintComponentId</c> through
        /// <c>IGradingAdmin.EnterMarkAsync</c>, then moves the homework to
        /// Released. This module never computes a grade and never publishes a
        /// result: from here WF-07 runs unchanged and Module 17 owns the mark.
        ///
        /// <para>
        /// Students who submitted nothing are left alone rather than written as
        /// zero. "Did not hand in" is a teacher's judgement to record as an
        /// absence, an exemption or a nil mark in Module 17's own marksheet, and
        /// silently posting zeros from here would be this module deciding a grade
        /// — which §1 puts squarely out of scope.
        /// </para>
        /// Throws <see cref="Common.Exceptions.HomeworkReleaseRefusedException"/>
        /// carrying the specific reason and the unscored count (BR-LRN-011);
        /// <see cref="Common.Exceptions.HomeworkMarksheetUnresolvedException"/>
        /// when the marksheet the marks must land in does not exist or does not
        /// cover a student — no second mark store is invented; and
        /// <see cref="Common.Exceptions.TeachingReachException"/> without reach.
        /// </summary>
        Task ReleaseAsync(
            int homeworkId,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// doc/Modules/37 §8.4's "one-click chase": tell the named students, and
        /// their families, that this homework has not reached us (§12
        /// <c>HomeworkOverdue</c>).
        ///
        /// <para>
        /// It lives here rather than on the screen because the enrolment ids
        /// arrive from a form and a chase names a child. Reach (BR-LRN-002) is
        /// re-checked, and the ids are intersected with the homework's own roster
        /// before anything is sent — a hand-edited request cannot make this
        /// product message a family whose class the sender does not teach.
        /// </para>
        ///
        /// <para>
        /// Only students who have actually submitted nothing are chased. Passing
        /// one who has handed in is silently skipped rather than refused: the
        /// roster is a live screen, and a hand-in that lands between rendering it
        /// and pressing the button is exactly the case where telling a family
        /// their child did not submit would be both wrong and unkind.
        /// </para>
        ///
        /// <para>
        /// BR-LRN-005 governs the wording downstream: late work stays acceptable,
        /// so this is a reminder and never a refusal. Returns how many students
        /// were chased, so the screen can say so.
        /// </para>
        /// Throws <see cref="Common.Exceptions.TeachingReachException"/> without
        /// reach.
        /// </summary>
        Task<int> ChaseAsync(
            int homeworkId,
            IReadOnlyCollection<int> enrollmentIds,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);
    }
}
