using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.Timetable;
using Sms.TestSupport;
using Xunit;
using static Sms.Application.Timetable.TimetableQualityEvaluator;

namespace Sms.Application.Tests.Timetable
{
    /// <summary>BR-TTB-005 soft constraints: warn + score, never block.</summary>
    public class TimetableQualityEvaluatorTests
    {
        private static PlacedPeriod P(int id, int section, int offering, int teacher, DayOfWeek day, int seq) => new(id, section, offering, teacher, day, seq);

        [Fact]
        [BusinessRule("BR-TTB-005")]
        public void A_clean_week_scores_100_with_no_warnings()
        {
            var r = Evaluate(new[]
            {
                P(1, 1, 10, 100, DayOfWeek.Sunday, 1), P(2, 1, 11, 101, DayOfWeek.Sunday, 2),
                P(3, 1, 10, 100, DayOfWeek.Monday, 1), P(4, 1, 11, 101, DayOfWeek.Monday, 2),
            });

            Assert.Empty(r.Warnings);
            Assert.Equal(100, r.Score);
        }

        [Fact]
        [BusinessRule("BR-TTB-005")]
        public void The_same_subject_twice_in_a_day_for_a_section_is_warned_and_penalised()
        {
            var r = Evaluate(new[] { P(1, 1, 10, 100, DayOfWeek.Sunday, 1), P(2, 1, 10, 100, DayOfWeek.Sunday, 3) });

            var w = Assert.Single(r.Warnings, x => x.Kind == WarningKind.SubjectRepeatedSameDay);
            Assert.Equal(1, w.SectionId);
            Assert.Equal(10, w.CurriculumOfferingId);
            Assert.Equal(2, w.Magnitude);
            Assert.True(r.Score < 100);
        }

        [Fact]
        [BusinessRule("BR-TTB-005")]
        public void A_teachers_consecutive_run_over_the_max_is_warned()
        {
            var placements = Enumerable.Range(1, 5).Select(seq => P(seq, seq, 20 + seq, 100, DayOfWeek.Tuesday, seq)).ToList(); // 5 in a row, different sections/offerings

            var r = Evaluate(placements, maxConsecutivePeriods: 4);

            var w = Assert.Single(r.Warnings, x => x.Kind == WarningKind.TeacherConsecutiveOverMax);
            Assert.Equal(100, w.TeacherProfileId);
            Assert.Equal(5, w.Magnitude);
            Assert.DoesNotContain(r.Warnings, x => x.Kind == WarningKind.TeacherDailyGap);
        }

        [Fact]
        [BusinessRule("BR-TTB-005")]
        public void Idle_gaps_in_a_teachers_day_are_counted()
        {
            var r = Evaluate(new[] { P(1, 1, 10, 100, DayOfWeek.Sunday, 1), P(2, 2, 10, 100, DayOfWeek.Sunday, 4) }); // periods 2 and 3 idle

            var w = Assert.Single(r.Warnings, x => x.Kind == WarningKind.TeacherDailyGap);
            Assert.Equal(2, w.Magnitude);
            Assert.Equal(96, r.Score);
        }

        [Fact]
        [BusinessRule("BR-TTB-005")]
        public void A_break_slot_between_two_periods_is_neither_a_gap_nor_part_of_a_run()
        {
            var breaks = new HashSet<(DayOfWeek, int)> { (DayOfWeek.Sunday, 3) };
            var placements = new[] { P(1, 1, 10, 100, DayOfWeek.Sunday, 1), P(2, 2, 10, 100, DayOfWeek.Sunday, 2), P(3, 3, 10, 100, DayOfWeek.Sunday, 4), P(4, 4, 10, 100, DayOfWeek.Sunday, 5), P(5, 5, 10, 100, DayOfWeek.Sunday, 6) };

            var r = Evaluate(placements, breaks, maxConsecutivePeriods: 4);

            Assert.DoesNotContain(r.Warnings, x => x.Kind == WarningKind.TeacherDailyGap);
            Assert.DoesNotContain(r.Warnings, x => x.Kind == WarningKind.TeacherConsecutiveOverMax); // 2 + break + 3, never 5 in a row
            Assert.Equal(100, r.Score);
        }

        [Fact]
        [BusinessRule("BR-TTB-005")]
        public void Score_never_drops_below_zero()
        {
            var placements = Enumerable.Range(0, 30).Select(i => P(i, 1, 10, 100, DayOfWeek.Sunday, i * 2 + 1)).ToList(); // 30× same subject, 29 gaps

            Assert.Equal(0, Evaluate(placements).Score);
        }
    }
}
