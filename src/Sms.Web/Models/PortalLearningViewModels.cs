using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.Portal;
using Sms.Domain.Sections;
using Sms.Domain.Students;

namespace Sms.Web.Models
{
    /// <summary>
    /// doc/Modules/37 §8.10 — the portal's "my work" page.
    ///
    /// Kept out of <c>PortalViewModels.cs</c> deliberately: module 37 is outside
    /// approved Analysis v1.0, and keeping its types in their own file makes the
    /// scope-change easy to see and easy to lift back out if the module is not
    /// approved.
    /// </summary>
    public sealed class PortalWorkViewModel
    {
        public sealed record StudentWork(
            Student Student,
            bool IsSelf,
            Section? Section,
            IReadOnlyList<PortalSetWork> Work);

        public List<StudentWork> Students { get; } = new();

        /// <summary>True when the family has students but none of them has any work set — a different sentence from "you have no children here".</summary>
        public bool HasStudentsButNoWork => Students.Count > 0 && Students.All(s => s.Work.Count == 0);

        /// <summary>
        /// Work due today or later, soonest first, across every student — what
        /// the page leads with, because "what is due next" is the question a
        /// family actually opens this page to answer.
        /// </summary>
        public IEnumerable<(StudentWork Student, PortalSetWork Work)> Upcoming(DateTime today)
            => Students
                .SelectMany(s => s.Work.Select(w => (Student: s, Work: w)))
                .Where(x => x.Work.DueDate.Date >= today.Date)
                .OrderBy(x => x.Work.DueDate);

        /// <summary>Work whose due date has passed. Still shown — BR-LRN-005 accepts late work, so a family must be able to see what they missed rather than have it silently disappear.</summary>
        public IEnumerable<(StudentWork Student, PortalSetWork Work)> Overdue(DateTime today)
            => Students
                .SelectMany(s => s.Work.Select(w => (Student: s, Work: w)))
                .Where(x => x.Work.DueDate.Date < today.Date)
                .OrderByDescending(x => x.Work.DueDate);
    }

    /// <summary>
    /// doc/Modules/37 §5 — the portal's "my lessons" page: the content half of
    /// the student's portal contract, which §8's numbered screen list never
    /// enumerated and which was therefore built for the teacher alone.
    ///
    /// Same file, and the same reason: module 37 is outside approved Analysis
    /// v1.0, so its types stay together and stay liftable.
    /// </summary>
    public sealed class PortalLessonsViewModel
    {
        public sealed record StudentLessons(
            Student Student,
            bool IsSelf,
            Sms.Domain.Grades.GradeLevel? Grade,
            IReadOnlyList<PortalLesson> Lessons);

        public List<StudentLessons> Students { get; } = new();

        /// <summary>True when the family has students but the school has published no lesson to any of them — a different sentence from "you have no children here".</summary>
        public bool HasStudentsButNoLessons => Students.Count > 0 && Students.All(s => s.Lessons.Count == 0);

        /// <summary>
        /// Every lesson once, grouped by subject and newest week first.
        /// <para>
        /// Deduplicated on the lesson id rather than concatenated per student:
        /// content follows the <c>CurriculumOffering</c>, so two children in the
        /// same grade share one plan, and listing it twice would tell a parent
        /// their school had planned two.
        /// </para>
        /// </summary>
        public IEnumerable<IGrouping<string, PortalLesson>> BySubject(bool isArabic)
            => Students
                .SelectMany(s => s.Lessons)
                .GroupBy(l => l.LessonId)
                .Select(g => g.First())
                .OrderByDescending(l => l.WeekNumber)
                .ThenBy(l => l.LessonId)
                .GroupBy(l => (isArabic ? l.SubjectNameAr : l.SubjectNameEn) is { Length: > 0 } name ? name : "—")
                .OrderBy(g => g.Key, StringComparer.CurrentCulture);
    }
}
