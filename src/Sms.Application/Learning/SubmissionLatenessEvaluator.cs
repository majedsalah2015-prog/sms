using System;
using Sms.Domain.Learning;

namespace Sms.Application.Learning
{
    /// <summary>
    /// Pure BR-LRN-005: the two questions lateness actually asks — <em>was this
    /// hand-in late</em>, and <em>what does the school's policy cost it</em>.
    ///
    /// <para>
    /// The rule's whole point is that those are two questions and not one. Late
    /// work is <b>accepted and flagged, never silently refused</b>: nothing here
    /// returns "rejected", and <see cref="LatenessPolicy"/> deliberately offers
    /// no member that could. What lateness changes is the mark, and it changes
    /// it <em>at marking</em> — <see cref="PenalisedScore"/> takes a score, so it
    /// cannot be called at submit time when there is no score to reduce.
    /// </para>
    ///
    /// <para>
    /// Pure by the same reasoning as <see cref="HomeworkIssueGate"/>: the clock,
    /// the homework and the policy arrive as values, so the rule is unit-testable
    /// without a database and no second caller can spell its own version of it.
    /// </para>
    /// </summary>
    public static class SubmissionLatenessEvaluator
    {
        /// <summary>
        /// BR-LRN-005 / BR-LRN-004: a due <em>date</em> is a day, not an instant.
        /// <c>Homework.DueDate</c> is stored as a date (the desk writes
        /// <c>dueDate.Date</c>), and a school that says "due Thursday" means work
        /// handed in at 23:50 on Thursday is on time. So lateness compares whole
        /// days, and only Friday makes it late.
        ///
        /// <para>
        /// Comparing the raw timestamps instead would make every hand-in after
        /// midnight <em>on the due day itself</em> late — the entire school day
        /// the work was actually set for.
        /// </para>
        /// </summary>
        public static bool IsLate(DateTime submittedAtUtc, DateTime dueDate)
            => submittedAtUtc.Date > dueDate.Date;

        /// <summary>
        /// BR-LRN-005: the policy decides the mark penalty. Returns the mark that
        /// counts — the teacher's entry when nothing is owed, and the reduced
        /// mark when it is.
        ///
        /// <para>
        /// Null in, null out: an unmarked submission has no penalty to apply, and
        /// returning zero for it would turn "not yet marked" into "scored
        /// nothing", which is the difference BR-LRN-011 blocks a release over.
        /// </para>
        ///
        /// <para>
        /// The percentage is clamped into 0-100 rather than trusted: a
        /// mis-configured 150% penalty must reduce a mark to zero, never past it
        /// into a negative that Module 17 would then aggregate.
        /// </para>
        /// </summary>
        public static decimal? PenalisedScore(
            decimal? rawScore,
            bool isLate,
            LatenessPolicy latenessPolicy,
            decimal? latePenaltyPercent)
        {
            if (rawScore is null)
            {
                return null;
            }

            if (!isLate || latenessPolicy != LatenessPolicy.AcceptWithPenalty)
            {
                return rawScore;
            }

            var percent = Math.Clamp(latePenaltyPercent ?? 0m, 0m, 100m);
            if (percent == 0m)
            {
                return rawScore;
            }

            var penalised = rawScore.Value * (100m - percent) / 100m;

            // Two places, matching the decimal(7,2) the mark columns carry in
            // both this module and Module 17 — rounding here rather than letting
            // the database truncate keeps the number the teacher is shown and the
            // number the marksheet receives the same number.
            return Math.Round(penalised, 2, MidpointRounding.AwayFromZero);
        }
    }
}
