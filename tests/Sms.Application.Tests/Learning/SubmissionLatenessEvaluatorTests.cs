using System;
using Sms.Application.Learning;
using Sms.Domain.Learning;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Learning
{
    /// <summary>
    /// BR-LRN-005 (doc/Modules/37 §9): late work is accepted and flagged, never
    /// refused — the policy decides the mark penalty, not the acceptance.
    /// </summary>
    public class SubmissionLatenessEvaluatorTests
    {
        // 2026-10-05 is the due day. The school means the day, not midnight.
        private static readonly DateTime DueDate = new(2026, 10, 5);

        // ---------------------------------------------------------------- was it late

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public void Work_handed_in_late_on_the_due_day_itself_is_on_time()
        {
            // The single most common real case: a student finishing at bedtime on
            // the day the work was due. Comparing raw timestamps would make the
            // entire school day the work was set for "late".
            var late = SubmissionLatenessEvaluator.IsLate(
                new DateTime(2026, 10, 5, 23, 50, 0, DateTimeKind.Utc), DueDate);

            Assert.False(late);
        }

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public void Work_handed_in_the_next_morning_is_late()
        {
            var late = SubmissionLatenessEvaluator.IsLate(
                new DateTime(2026, 10, 6, 0, 5, 0, DateTimeKind.Utc), DueDate);

            Assert.True(late);
        }

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public void Work_handed_in_early_is_not_late()
        {
            var late = SubmissionLatenessEvaluator.IsLate(
                new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc), DueDate);

            Assert.False(late);
        }

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public void A_due_date_carrying_a_time_component_is_still_read_as_a_day()
        {
            // Homework.DueDate is written as .Date by the desk, but nothing stops a
            // caller passing a stamped value; the rule must not change if one does.
            var late = SubmissionLatenessEvaluator.IsLate(
                new DateTime(2026, 10, 5, 18, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 10, 5, 7, 30, 0));

            Assert.False(late);
        }

        // ---------------------------------------------------------------- what it costs

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public void On_time_work_keeps_the_mark_the_teacher_entered()
        {
            var score = SubmissionLatenessEvaluator.PenalisedScore(
                18m, isLate: false, LatenessPolicy.AcceptWithPenalty, 25m);

            Assert.Equal(18m, score);
        }

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public void Late_work_under_the_no_penalty_policy_keeps_its_full_mark()
        {
            // The flag is still raised for the teacher's judgement - it just costs
            // nothing. That is the whole difference between the two policies.
            var score = SubmissionLatenessEvaluator.PenalisedScore(
                18m, isLate: true, LatenessPolicy.AcceptWithoutPenalty, 25m);

            Assert.Equal(18m, score);
        }

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public void Late_work_under_the_penalty_policy_is_reduced()
        {
            var score = SubmissionLatenessEvaluator.PenalisedScore(
                18m, isLate: true, LatenessPolicy.AcceptWithPenalty, 25m);

            Assert.Equal(13.5m, score);
        }

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public void A_penalty_policy_with_no_percentage_set_costs_nothing()
        {
            // A half-configured homework must not silently zero a class's marks.
            var score = SubmissionLatenessEvaluator.PenalisedScore(
                18m, isLate: true, LatenessPolicy.AcceptWithPenalty, null);

            Assert.Equal(18m, score);
        }

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public void A_full_penalty_reduces_the_mark_to_zero()
        {
            var score = SubmissionLatenessEvaluator.PenalisedScore(
                18m, isLate: true, LatenessPolicy.AcceptWithPenalty, 100m);

            Assert.Equal(0m, score);
        }

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public void A_misconfigured_penalty_over_a_hundred_percent_stops_at_zero()
        {
            // Never negative: Module 17 would aggregate it into a term percentage
            // and nothing downstream questions a number that arrived as a mark.
            var score = SubmissionLatenessEvaluator.PenalisedScore(
                18m, isLate: true, LatenessPolicy.AcceptWithPenalty, 150m);

            Assert.Equal(0m, score);
        }

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public void A_negative_penalty_percentage_never_inflates_a_mark()
        {
            var score = SubmissionLatenessEvaluator.PenalisedScore(
                18m, isLate: true, LatenessPolicy.AcceptWithPenalty, -40m);

            Assert.Equal(18m, score);
        }

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void An_unmarked_submission_stays_unmarked_rather_than_becoming_zero()
        {
            // "Not marked yet" and "scored nothing" are the difference BR-LRN-011
            // blocks a release over. A penalty must never collapse one into the
            // other.
            var score = SubmissionLatenessEvaluator.PenalisedScore(
                null, isLate: true, LatenessPolicy.AcceptWithPenalty, 25m);

            Assert.Null(score);
        }

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public void The_penalised_mark_is_rounded_to_the_two_places_the_column_holds()
        {
            // 17 x 0.67 = 11.39, exactly; 17 x 0.665 = 11.305 must not reach the
            // marksheet as a third place the column would silently truncate.
            var score = SubmissionLatenessEvaluator.PenalisedScore(
                17m, isLate: true, LatenessPolicy.AcceptWithPenalty, 33.5m);

            Assert.Equal(11.31m, score);
        }
    }
}
