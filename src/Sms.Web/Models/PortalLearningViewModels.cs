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
}
