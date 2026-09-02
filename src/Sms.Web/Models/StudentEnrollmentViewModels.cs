using System;
using System.Collections.Generic;
using Sms.Application.Common.Guards;

namespace Sms.Web.Models
{
    /// <summary>
    /// What the academic-history tab needs in order to offer a correction or a removal on one of its
    /// rows, rather than only to list them (doc/Modules/10 §8.10; the pattern is P-DETAIL's history
    /// table with row actions, as the guardians tab already does with unlink).
    /// <para>
    /// It lives in its own file because <c>PeopleViewModels.cs</c> is edited by several strands of
    /// work at once; nothing here changes the shape of what was already on the page.
    /// </para>
    /// </summary>
    public sealed partial class StudentFileViewModel
    {
        /// <summary>
        /// One enrollment row's answer to "what may be done to this?" — computed per row, because
        /// the answer differs between the child's current year and the three years behind it.
        /// </summary>
        /// <param name="EnrollmentId">The row this describes.</param>
        /// <param name="Usage">
        /// What hangs off it. Empty means the row is a keying mistake and nothing more; anything in
        /// it is history, and history is not removed (BR-GLB-005). The report is carried rather than
        /// a boolean so the refusal — and the disabled button's tooltip — can name what is in the way.
        /// </param>
        /// <param name="Seated">
        /// Whether the child currently sits in a section of this enrollment's grade. A seat blocks a
        /// grade correction, because the section belongs to the grade being corrected.
        /// </param>
        public sealed record EnrollmentActions(int EnrollmentId, UsageReport Usage, bool Seated)
        {
            public bool CanRemove => !Usage.IsInUse;
        }

        /// <summary>Keyed by enrollment id; a row with no entry offers no actions at all.</summary>
        public IReadOnlyDictionary<int, EnrollmentActions> EnrollmentActionsById { get; set; }
            = new Dictionary<int, EnrollmentActions>();

        /// <summary>STU/Enrollment/Edit — changing the grade already on a record.</summary>
        public bool CanCorrectEnrollment { get; set; }

        /// <summary>STU/Enrollment/Deactivate — taking one off the record altogether.</summary>
        public bool CanRemoveEnrollment { get; set; }

        /// <summary>
        /// SEC/Roster/Edit — the seat half of the same question. Held here as well as on the
        /// placement screen because the academic tab now offers "leave the section" beside the row
        /// it applies to, and BR-SEC-010 wants the control absent rather than refusing on click.
        /// </summary>
        public bool CanSeat { get; set; }

        /// <summary>
        /// Grade-years each enrollment may be corrected *into* — its own year's grades and no
        /// others, since a year change is a rollover and not a correction (BR-GLB-023).
        /// <para>
        /// Keyed by enrollment rather than by year, because each row's list also has to contain the
        /// grade that row currently names even when that grade has since been retired. Two rows in
        /// the same year can therefore hold different lists.
        /// </para>
        /// </summary>
        public IReadOnlyDictionary<int, IReadOnlyList<GradeYearOption>> CorrectionOptionsByEnrollment { get; set; }
            = new Dictionary<int, IReadOnlyList<GradeYearOption>>();
    }

    /// <summary>A grade-year a child can be placed in or corrected into, named in both languages.</summary>
    public sealed record GradeYearOption(int ProfileId, string Code, string NameAr, string NameEn);

    public sealed partial class StudentPlacementViewModel
    {
        /// <summary>
        /// The open enrollment's own year's grades — the only legal targets for a correction
        /// (BR-GLB-023). Empty when the student is not enrolled.
        /// </summary>
        public IReadOnlyList<GradeYearOption> CorrectionOptions { get; set; } = Array.Empty<GradeYearOption>();

        /// <summary>What removing the open enrollment would take with it; null when there is none.</summary>
        public UsageReport? Usage { get; set; }

        /// <summary>STU/Enrollment/Edit.</summary>
        public bool CanCorrectEnrollment { get; set; }

        /// <summary>STU/Enrollment/Deactivate.</summary>
        public bool CanRemoveEnrollment { get; set; }
    }
}
