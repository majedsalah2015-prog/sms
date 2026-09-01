using System;
using Sms.Application.Learning;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Learning
{
    /// <summary>
    /// BR-LRN-004 (doc/Modules/37 §9): what must be true before a class is told
    /// to do the work.
    /// </summary>
    public class HomeworkIssueGateTests
    {
        private static readonly DateTime YearStart = new(2026, 9, 1);
        private static readonly DateTime YearEnd = new(2027, 6, 30);
        private static readonly DateTime AWorkingDay = new(2026, 10, 5);

        private static readonly Func<DateTime, bool> EveryDayWorks = _ => true;
        private static readonly Func<DateTime, bool> NoDayWorks = _ => false;

        [Fact]
        [BusinessRule("BR-LRN-004")]
        public void Ungraded_practice_issues_without_a_blueprint_component()
        {
            // BR-LRN-004: max marks are optional - an ungraded practice homework
            // is legitimate and never reaches Module 17.
            var refusal = HomeworkIssueGate.Check(
                maxMarks: null, blueprintComponentId: null,
                AWorkingDay, YearStart, YearEnd, EveryDayWorks);

            Assert.Equal(HomeworkIssueRefusal.None, refusal);
        }

        [Fact]
        [BusinessRule("BR-LRN-004")]
        public void Graded_homework_without_a_component_is_refused()
        {
            var refusal = HomeworkIssueGate.Check(
                maxMarks: 10m, blueprintComponentId: null,
                AWorkingDay, YearStart, YearEnd, EveryDayWorks);

            Assert.Equal(HomeworkIssueRefusal.GradedWithoutBlueprintComponent, refusal);
        }

        [Fact]
        [BusinessRule("BR-LRN-004")]
        public void Ungraded_homework_naming_a_component_is_refused()
        {
            // Naming a component promises Module 17 a mark that will never come.
            var refusal = HomeworkIssueGate.Check(
                maxMarks: null, blueprintComponentId: 7,
                AWorkingDay, YearStart, YearEnd, EveryDayWorks);

            Assert.Equal(HomeworkIssueRefusal.UngradedWithBlueprintComponent, refusal);
        }

        [Fact]
        [BusinessRule("BR-LRN-004")]
        public void Graded_homework_with_a_component_issues()
        {
            var refusal = HomeworkIssueGate.Check(
                maxMarks: 10m, blueprintComponentId: 7,
                AWorkingDay, YearStart, YearEnd, EveryDayWorks);

            Assert.Equal(HomeworkIssueRefusal.None, refusal);
        }

        [Fact]
        public void Zero_max_marks_reads_as_ungraded_rather_than_as_a_graded_zero()
        {
            // A homework worth zero marks is practice, not an assessment worth
            // nothing - so it must not demand a component.
            Assert.False(HomeworkIssueGate.IsGraded(0m));

            var refusal = HomeworkIssueGate.Check(
                maxMarks: 0m, blueprintComponentId: null,
                AWorkingDay, YearStart, YearEnd, EveryDayWorks);

            Assert.Equal(HomeworkIssueRefusal.None, refusal);
        }

        [Theory]
        [BusinessRule("BR-GLB-051")]
        [InlineData("2026-08-31")]
        [InlineData("2027-07-01")]
        public void A_due_date_outside_the_academic_year_is_refused(string dueDate)
        {
            var refusal = HomeworkIssueGate.Check(
                maxMarks: null, blueprintComponentId: null,
                DateTime.Parse(dueDate), YearStart, YearEnd, EveryDayWorks);

            Assert.Equal(HomeworkIssueRefusal.DueDateOutsideAcademicYear, refusal);
        }

        [Theory]
        [BusinessRule("BR-GLB-051")]
        [InlineData("2026-09-01")]
        [InlineData("2027-06-30")]
        public void The_first_and_last_day_of_the_year_are_inside_it(string dueDate)
        {
            var refusal = HomeworkIssueGate.Check(
                maxMarks: null, blueprintComponentId: null,
                DateTime.Parse(dueDate), YearStart, YearEnd, EveryDayWorks);

            Assert.Equal(HomeworkIssueRefusal.None, refusal);
        }

        [Fact]
        [BusinessRule("BR-GLB-052")]
        public void A_due_date_that_is_not_a_working_day_is_refused()
        {
            // Work due on a holiday is work due on a day nobody is there to
            // receive it.
            var refusal = HomeworkIssueGate.Check(
                maxMarks: null, blueprintComponentId: null,
                AWorkingDay, YearStart, YearEnd, NoDayWorks);

            Assert.Equal(HomeworkIssueRefusal.DueDateNotAWorkingDay, refusal);
        }

        [Fact]
        public void The_structural_refusal_is_reported_before_the_scheduling_one()
        {
            // Both are wrong here. The teacher should be told about the mark with
            // nowhere to land first, because fixing the date would not help.
            var refusal = HomeworkIssueGate.Check(
                maxMarks: 10m, blueprintComponentId: null,
                new DateTime(2030, 1, 1), YearStart, YearEnd, NoDayWorks);

            Assert.Equal(HomeworkIssueRefusal.GradedWithoutBlueprintComponent, refusal);
        }

        [Fact]
        public void The_time_of_day_on_a_due_date_does_not_push_it_out_of_the_year()
        {
            // The year's bounds are dates; a due date carrying a time component
            // must compare by date or the last day of the year is rejected.
            var refusal = HomeworkIssueGate.Check(
                maxMarks: null, blueprintComponentId: null,
                YearEnd.AddHours(23), YearStart, YearEnd, EveryDayWorks);

            Assert.Equal(HomeworkIssueRefusal.None, refusal);
        }

        [Fact]
        public void A_missing_working_day_predicate_is_a_programming_error_not_a_silent_pass()
        {
            Assert.Throws<ArgumentNullException>(() => HomeworkIssueGate.Check(
                maxMarks: null, blueprintComponentId: null,
                AWorkingDay, YearStart, YearEnd, null!));
        }
    }
}
