using System;
using System.Collections.Generic;

namespace Sms.Web.Api.Models
{
    /// <summary>One of the family's students, as the app's home screen lists them.</summary>
    public sealed class ApiPortalChild
    {
        public int StudentId { get; set; }

        public string StudentNo { get; set; } = string.Empty;

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        /// <summary>True when the caller is the student themselves rather than a guardian.</summary>
        public bool IsSelf { get; set; }

        public string? GradeCode { get; set; }

        public string? GradeName { get; set; }

        public string? SectionName { get; set; }

        /// <summary>BR-ATD-009 for the working year. Null when the student has no enrollment in it.</summary>
        public decimal? AttendancePercent { get; set; }

        /// <summary>
        /// What is outstanding on this student's fees. Positive is owed.
        /// Null when the fee gate refused — which for a guardian who is not
        /// financially responsible is a normal answer, not an error.
        /// </summary>
        public decimal? FeeBalance { get; set; }
    }

    /// <summary>BR-ATD-009 as the portal states it.</summary>
    public sealed class ApiPortalAttendance
    {
        public int StudentId { get; set; }

        public int ScheduledDays { get; set; }

        public int ExemptedDays { get; set; }

        public int AbsentDays { get; set; }

        public decimal AttendancePercent { get; set; }
    }

    /// <summary>One published term result (BR-SEC-012 — drafts do not exist here).</summary>
    public sealed class ApiPortalResult
    {
        public int CurriculumOfferingId { get; set; }

        public string SubjectNameAr { get; set; } = string.Empty;

        public string SubjectNameEn { get; set; } = string.Empty;

        public int TermId { get; set; }

        public string? TermName { get; set; }

        public decimal ScorePercent { get; set; }

        public string? BandCode { get; set; }

        public DateTime PublishedAtUtc { get; set; }
    }

    /// <summary>
    /// The family's money for one student. Gross and discounts are reported
    /// apart and never netted invisibly (BR-DIS-010).
    /// </summary>
    public sealed class ApiPortalFees
    {
        public int StudentId { get; set; }

        public decimal Position { get; set; }

        public decimal GrossCharges { get; set; }

        public decimal Discounts { get; set; }

        public string Currency { get; set; } = string.Empty;

        public IReadOnlyList<ApiPortalChargeLine> Charges { get; set; } = Array.Empty<ApiPortalChargeLine>();
    }

    /// <summary>One posted charge. Void charges never appear (BR-SEC-012).</summary>
    public sealed class ApiPortalChargeLine
    {
        public string ChargeNo { get; set; } = string.Empty;

        public decimal GrossAmount { get; set; }

        public DateTime PostedAtUtc { get; set; }
    }

    /// <summary>The whole family's position in one figure, for the app's home card.</summary>
    public sealed class ApiPortalStatement
    {
        public decimal Total { get; set; }

        public string Currency { get; set; } = string.Empty;

        public IReadOnlyList<ApiPortalFees> Students { get; set; } = Array.Empty<ApiPortalFees>();
    }

    /// <summary>doc/Modules/37 §8.10 — one piece of set work.</summary>
    public sealed class ApiPortalHomework
    {
        public int HomeworkId { get; set; }

        public string TitleAr { get; set; } = string.Empty;

        public string TitleEn { get; set; } = string.Empty;

        public string? InstructionsAr { get; set; }

        public string? InstructionsEn { get; set; }

        public string SubjectNameAr { get; set; } = string.Empty;

        public string SubjectNameEn { get; set; } = string.Empty;

        public DateTime DueDate { get; set; }

        /// <summary>BR-LRN-004: null means ungraded practice, which the app should say rather than show a blank mark.</summary>
        public decimal? MaxMarks { get; set; }

        public bool LatePenaltyApplies { get; set; }

        public decimal? LatePenaltyPercent { get; set; }
    }

    /// <summary>doc/Modules/37 §5 — one published lesson and its material.</summary>
    public sealed class ApiPortalLesson
    {
        public int LessonId { get; set; }

        public int WeekNumber { get; set; }

        public string TitleAr { get; set; } = string.Empty;

        public string TitleEn { get; set; } = string.Empty;

        public string? ObjectivesAr { get; set; }

        public string? ObjectivesEn { get; set; }

        public string SubjectNameAr { get; set; } = string.Empty;

        public string SubjectNameEn { get; set; } = string.Empty;

        public DateTime? PublishedAtUtc { get; set; }

        public IReadOnlyList<ApiPortalLessonResource> Resources { get; set; } = Array.Empty<ApiPortalLessonResource>();
    }

    /// <summary>
    /// One downloadable item. BR-LRN-006: a resource whose current version is
    /// not scan-clean is never even listed, so everything here is fetchable at
    /// the moment it was listed.
    /// </summary>
    public sealed class ApiPortalLessonResource
    {
        public int ResourceId { get; set; }

        public string TitleAr { get; set; } = string.Empty;

        public string TitleEn { get; set; } = string.Empty;

        public int DisplayOrder { get; set; }

        /// <summary>Where to GET the bytes. Same bearer token; the scan gate is re-applied there.</summary>
        public string DownloadUrl { get; set; } = string.Empty;
    }

    /// <summary>BR-SEC-012: only a sent announcement ever reaches a family.</summary>
    public sealed class ApiPortalAnnouncement
    {
        public int Id { get; set; }

        public string TitleAr { get; set; } = string.Empty;

        public string TitleEn { get; set; } = string.Empty;

        public string? BodyAr { get; set; }

        public string? BodyEn { get; set; }

        public DateTime? SentAtUtc { get; set; }
    }

    /// <summary>One week of the student's section timetable, flattened for a phone.</summary>
    public sealed class ApiPortalTimetable
    {
        public int StudentId { get; set; }

        public string? SectionName { get; set; }

        public string? GradeCode { get; set; }

        /// <summary>The Saturday (or the school's own first weekday) this grid starts on.</summary>
        public DateTime WeekStart { get; set; }

        public IReadOnlyList<ApiTimetableEntry> Entries { get; set; } = Array.Empty<ApiTimetableEntry>();
    }

    /// <summary>
    /// One period on one day. A phone renders a list per day, not a grid, so the
    /// week is flattened here rather than in the client.
    /// </summary>
    public sealed class ApiTimetableEntry
    {
        /// <summary>Sunday = 0, per <see cref="System.DayOfWeek"/>.</summary>
        public int DayOfWeek { get; set; }

        public int PeriodSequence { get; set; }

        public string? StartTime { get; set; }

        public string? EndTime { get; set; }

        public string SubjectNameAr { get; set; } = string.Empty;

        public string SubjectNameEn { get; set; } = string.Empty;

        public string? TeacherNameAr { get; set; }

        public string? TeacherNameEn { get; set; }

        public string? RoomName { get; set; }

        public string? SectionName { get; set; }

        /// <summary>
        /// BR-TTB-008: this week's dated overlay, when there is one — a
        /// substitution, a room change or a cancellation. Null on an ordinary
        /// week.
        /// </summary>
        public string? ChangeKind { get; set; }
    }
}
