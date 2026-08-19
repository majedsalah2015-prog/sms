using System;
using System.Collections.Generic;
using Sms.Application.Portal;
using Sms.Domain.Attendance;
using Sms.Domain.Grades;
using Sms.Domain.Messaging;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Students;
using Sms.Domain.Subjects;

namespace Sms.Web.Models
{
    // ---------------------------------------------------------------- Portal (E-304: doc/Modules/11 §8.5, 10 §8, docs/06-Security BR-SEC-010..013)

    /// <summary>One child (or the student's own record) as the family view shows it.</summary>
    public sealed record PortalChildCard(Student Student, bool IsSelf, GradeLevel? Grade, Section? Section, PortalAttendanceSummary? Attendance, decimal? FeePosition, int PublishedResults);

    public sealed class PortalHomeViewModel
    {
        public string? ParentNameAr { get; set; }

        public string? ParentNameEn { get; set; }

        public bool IsPortalAccount { get; set; }

        public IReadOnlyList<PortalChildCard> Children { get; set; } = Array.Empty<PortalChildCard>();

        public IReadOnlyList<Announcement> Announcements { get; set; } = Array.Empty<Announcement>();

        public AcademicYear? Year { get; set; }
    }

    public sealed class PortalStudentViewModel
    {
        public sealed record ResultRow(Term Term, Subject Subject, PortalResultSummary Result);

        public sealed record DayRow(AttendanceDay Day);

        public Student Student { get; set; } = null!;

        public bool IsSelf { get; set; }

        public GradeLevel? Grade { get; set; }

        public Section? Section { get; set; }

        public AcademicYear? Year { get; set; }

        public string ActiveTab { get; set; } = "attendance";

        public PortalAttendanceSummary Attendance { get; set; } = new();

        public IReadOnlyList<AttendanceDay> RecentDays { get; set; } = Array.Empty<AttendanceDay>();

        public IReadOnlyList<ResultRow> Results { get; set; } = Array.Empty<ResultRow>();

        public IReadOnlyList<Term> Terms { get; set; } = Array.Empty<Term>();

        public PortalFeePosition Fees { get; set; } = new();

        /// <summary>BR-STU-002: Suspended blocks portal result visibility per configuration — never fee visibility.</summary>
        public bool ResultsHidden { get; set; }
    }

    public sealed class PortalStatementViewModel
    {
        public sealed record Line(Student Student, PortalFeePosition Position);

        public IReadOnlyList<Line> Lines { get; set; } = Array.Empty<Line>();

        public decimal Total { get; set; }
    }

    public sealed class PortalAnnouncementsViewModel
    {
        public IReadOnlyList<Announcement> Announcements { get; set; } = Array.Empty<Announcement>();
    }
}
