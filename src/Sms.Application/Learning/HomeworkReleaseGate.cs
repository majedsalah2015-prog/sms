using Sms.Domain.Learning;

namespace Sms.Application.Learning
{
    /// <summary>
    /// Pure BR-LRN-011/012 (doc/Modules/37 §4, §8.5): what must be true before a
    /// marked homework's raw marks may be written into Module 17's marksheet.
    ///
    /// <para>
    /// BR-LRN-011 is written about sittings — "a sitting is not <em>marked</em>
    /// until every item carries a score; a partly-marked sitting can never be
    /// released" — and §8.5 states the same requirement of the homework marking
    /// queue in as many words: it "refuses release while any item is unscored
    /// (BR-LRN-011)". The reason is the same in both places and is worth stating
    /// plainly: releasing half a class's marks publishes a mark of nothing for
    /// the other half, and Module 17 cannot tell "scored zero" from "nobody
    /// looked at it yet".
    /// </para>
    ///
    /// <para>
    /// Pure, and given counts rather than rows, for <see cref="HomeworkIssueGate"/>'s
    /// reason: the roster is a database question and this layer must not touch
    /// one. The service counts, this decides.
    /// </para>
    /// </summary>
    public static class HomeworkReleaseGate
    {
        /// <summary>
        /// The first refusal that applies, or <see cref="HomeworkReleaseRefusal.None"/>.
        ///
        /// <para>
        /// Ordered so the teacher is told the structural problem before the
        /// clerical one. "This homework is not being marked yet" and "this
        /// homework was never graded" are both answers that make the unscored
        /// count meaningless, so they are asked first; only then does the count
        /// of unmarked hand-ins decide.
        /// </para>
        /// </summary>
        /// <param name="status">The homework's current status.</param>
        /// <param name="maxMarks">BR-LRN-004: null is ungraded practice.</param>
        /// <param name="blueprintComponentId">BR-LRN-012: the Module 17 component the marks will land in.</param>
        /// <param name="unscoredSubmissionCount">How many live submissions still carry no score (BR-LRN-011).</param>
        public static HomeworkReleaseRefusal Check(
            HomeworkStatus status,
            decimal? maxMarks,
            int? blueprintComponentId,
            int unscoredSubmissionCount)
        {
            // doc/Modules/37 §4: release is the step out of Marking and nowhere
            // else. Asked of an Issued homework it would hand Module 17 the marks
            // of a class still doing the work; asked of a Released one it would
            // be a mark change, which BR-LRN-012 puts under Module 17's control,
            // not under a second release here.
            if (status != HomeworkStatus.Marking)
            {
                return HomeworkReleaseRefusal.NotBeingMarked;
            }

            // BR-LRN-004: ungraded practice "never reaches Module 17". There is
            // nothing to release, so the teacher is told that rather than being
            // left to wonder why the button did nothing. The homework stays in
            // Marking, which is honest: for practice, marking with feedback IS
            // the finished state.
            if (!HomeworkIssueGate.IsGraded(maxMarks))
            {
                return HomeworkReleaseRefusal.UngradedPractice;
            }

            // BR-LRN-004/012. The issue gate already refuses a graded homework
            // with no component, so reaching this means the component was cleared
            // after issue - a mark with nowhere to land, caught here rather than
            // as a null reference one line into the handoff.
            if (blueprintComponentId is null)
            {
                return HomeworkReleaseRefusal.NoBlueprintComponent;
            }

            // BR-LRN-011 / §8.5.
            if (unscoredSubmissionCount > 0)
            {
                return HomeworkReleaseRefusal.SubmissionsUnscored;
            }

            // A homework nobody submitted is deliberately NOT refused. It
            // releases, writes no marks, and reaches Released — because the
            // alternative is a row stranded in Marking for ever with no action
            // that can ever move it, and "nobody handed anything in" is a real
            // outcome a teacher must be able to close.
            return HomeworkReleaseRefusal.None;
        }
    }

    /// <summary>
    /// Why BR-LRN-011/012 refused to release. Mapped to a typed exception by the
    /// service and translated at the Web boundary — never surfaced as English
    /// exception text, per §9.
    /// </summary>
    public enum HomeworkReleaseRefusal
    {
        None = 0,

        /// <summary>doc/Modules/37 §4: release is the step out of Marking, and only out of Marking.</summary>
        NotBeingMarked = 1,

        /// <summary>BR-LRN-004: ungraded practice never reaches Module 17, so there is no mark to release.</summary>
        UngradedPractice = 2,

        /// <summary>BR-LRN-004/012: graded, but naming no component — the mark would have nowhere to land.</summary>
        NoBlueprintComponent = 3,

        /// <summary>BR-LRN-011: at least one hand-in still carries no score. The count travels on the exception so the teacher is told how many.</summary>
        SubmissionsUnscored = 4,
    }
}
