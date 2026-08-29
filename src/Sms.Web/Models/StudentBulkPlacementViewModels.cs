using System;
using System.Collections.Generic;
using Sms.Domain.Grades;
using Sms.Domain.Sections;
using Sms.Domain.Students;

namespace Sms.Web.Models
{
    /// <summary>
    /// The bulk placement screen (doc/Modules/10 §8, BR-STU-010): a filtered roll of students with
    /// checkboxes, one grade-year and at most one section to put them in, and — between choosing
    /// and committing — the dry run BR-STU-010 requires.
    /// </summary>
    public sealed class BulkPlacementViewModel
    {
        /// <summary>What the commit will do to one selected child, decided before anything is written.</summary>
        public enum Verdict
        {
            /// <summary>Not enrolled in the destination year: an enrollment will be created.</summary>
            WillEnroll = 1,

            /// <summary>Enrolled and seated in one step.</summary>
            WillEnrollAndSeat = 2,

            /// <summary>Already enrolled in the destination grade-year but sitting in no section.</summary>
            WillSeat = 3,

            /// <summary>Nothing to do — already where the form is trying to put them.</summary>
            AlreadyThere = 4,

            /// <summary>BR-GLB-024 or BR-SCN-005: an open enrollment or a section this run must not overwrite.</summary>
            EnrolledElsewhere = 5,

            /// <summary>BR-SCN-002: enrolled in the grade, left unseated because the section has no seat left.</summary>
            SectionFull = 6,

            /// <summary>BR-SCN-003: enrolled in the grade, left unseated because the section's gender policy refuses them.</summary>
            GenderMismatch = 7,
        }

        /// <summary>A grade-year the roll can be placed into, newest year first.</summary>
        public sealed record ProfileOption(int Id, string GradeName, string YearName, int AcademicYearId, int Order);

        /// <summary>A section of the destination grade-year, with what it holds right now — not what it was planned to hold.</summary>
        public sealed record SectionOption(Section Section, int Members);

        /// <summary>
        /// One selectable student. <paramref name="GradeName"/> and <paramref name="YearName"/>
        /// describe where they sit today — usually last year, or nowhere at all after an import.
        /// </summary>
        public sealed record Row(Student Student, string? GradeName, string? SectionName, string? YearName, bool InTargetYear);

        /// <summary>One line of the dry run. <paramref name="Reason"/> is already translated.</summary>
        public sealed record PreviewRow(Student Student, Verdict Verdict, Enrollment? Enrollment, Section? CurrentSection, string Reason);

        // ---- the form
        public int? ProfileId { get; set; }

        public int? SectionId { get; set; }

        public string? Q { get; set; }

        /// <summary>Narrows the roll by where the students sit *now*, which is how a registrar thinks of last year's third grade.</summary>
        public int? GradeFilter { get; set; }

        /// <summary>Off by default: the screen exists for the children who are not placed yet, and showing the placed ones by default buries them.</summary>
        public bool PlacedToo { get; set; }

        public DateTime EnrollmentDate { get; set; }

        /// <summary>A refusal that belongs to the form rather than to a student — already translated.</summary>
        public string? Error { get; set; }

        // ---- rights (BR-SEC-010)
        public bool CanEnroll { get; set; }

        public bool CanSeat { get; set; }

        // ---- pickers
        public IReadOnlyList<GradeLevel> Grades { get; set; } = Array.Empty<GradeLevel>();

        public IReadOnlyList<ProfileOption> Profiles { get; set; } = Array.Empty<ProfileOption>();

        public IReadOnlyList<SectionOption> Sections { get; set; } = Array.Empty<SectionOption>();

        // ---- the chosen destination
        public GradeYearProfile? Profile { get; set; }

        public Section? Section { get; set; }

        public string? GradeYearName { get; set; }

        public string? YearName { get; set; }

        /// <summary>Null when no section is chosen; otherwise seats left in it before this run starts.</summary>
        public int? SeatsLeft { get; set; }

        // ---- the roll
        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        /// <summary>What the filter matched, which is not the same as what the page shows.</summary>
        public int MatchCount { get; set; }

        public bool IsTruncated { get; set; }

        /// <summary>Students of this school with no open enrollment in the destination year — the number the screen exists to bring down.</summary>
        public int UnplacedTotal { get; set; }

        // ---- the dry run
        public IReadOnlyList<PreviewRow> Preview { get; set; } = Array.Empty<PreviewRow>();

        public bool HasPreview => Preview.Count > 0;
    }
}
