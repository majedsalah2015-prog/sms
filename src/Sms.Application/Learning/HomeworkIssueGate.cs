using System;

namespace Sms.Application.Learning
{
    /// <summary>
    /// Pure BR-LRN-004: what must be true before homework may be issued to a
    /// section. Everything here is checked at issue rather than at save, because
    /// a draft is allowed to be incomplete — BR-GLB-031 says a draft affects
    /// nothing, so refusing an unfinished draft would be refusing the teacher
    /// the ability to start work on Tuesday and finish it on Wednesday.
    ///
    /// <para>
    /// The working-day question arrives as a predicate rather than a calendar,
    /// following <c>SessionGenerator</c> and <c>DueDateShifter</c>: the school
    /// calendar is data this layer must not touch, and passing the answer in
    /// keeps the rule unit-testable without a database.
    /// </para>
    /// </summary>
    public static class HomeworkIssueGate
    {
        /// <summary>BR-LRN-004: marks set means graded, which means Module 17 is expecting something.</summary>
        public static bool IsGraded(decimal? maxMarks) => maxMarks is > 0m;

        /// <summary>
        /// The first refusal that applies, or <see cref="HomeworkIssueRefusal.None"/>.
        /// Ordered so the teacher fixes the structural problem (a mark with
        /// nowhere to land) before the scheduling one.
        /// </summary>
        public static HomeworkIssueRefusal Check(
            decimal? maxMarks,
            int? blueprintComponentId,
            DateTime dueDate,
            DateTime yearStartDate,
            DateTime yearEndDate,
            Func<DateTime, bool> isWorkingDay)
        {
            if (isWorkingDay == null)
            {
                throw new ArgumentNullException(nameof(isWorkingDay));
            }

            // BR-LRN-004: a graded homework must name the component it will feed
            // BEFORE it is issued. Discovering at release that a mark has nowhere
            // to land means telling a class their work does not count.
            if (IsGraded(maxMarks) && blueprintComponentId is null)
            {
                return HomeworkIssueRefusal.GradedWithoutBlueprintComponent;
            }

            // A component named on ungraded practice is a contradiction, not a
            // harmless extra: it promises Module 17 a mark that will never come.
            if (!IsGraded(maxMarks) && blueprintComponentId is not null)
            {
                return HomeworkIssueRefusal.UngradedWithBlueprintComponent;
            }

            // BR-GLB-051: no transactional date outside its academic year.
            if (dueDate.Date < yearStartDate.Date || dueDate.Date > yearEndDate.Date)
            {
                return HomeworkIssueRefusal.DueDateOutsideAcademicYear;
            }

            // BR-GLB-052: due dates respect the school calendar. Work due on a
            // holiday is work due on a day nobody is there to hand it in.
            if (!isWorkingDay(dueDate.Date))
            {
                return HomeworkIssueRefusal.DueDateNotAWorkingDay;
            }

            return HomeworkIssueRefusal.None;
        }
    }

    /// <summary>Why BR-LRN-004 refused to issue. Mapped to a typed exception by the service and translated at the Web boundary.</summary>
    public enum HomeworkIssueRefusal
    {
        None = 0,
        GradedWithoutBlueprintComponent = 1,
        UngradedWithBlueprintComponent = 2,
        DueDateOutsideAcademicYear = 3,
        DueDateNotAWorkingDay = 4,
    }
}
