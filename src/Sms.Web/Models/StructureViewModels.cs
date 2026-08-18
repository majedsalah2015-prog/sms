using System;
using System.Collections.Generic;
using Sms.Domain.Calendar;
using Sms.Domain.Grades;
using Sms.Domain.Schools;
using Sms.Domain.Sections;

namespace Sms.Web.Models
{
    // ---------------------------------------------------------------- Calendar (doc/Modules/04 §8)

    public sealed class CalendarBoardViewModel
    {
        public sealed record DayCell(DateTime Date, DayType Type, bool IsOverride, bool IsProvisional, string? HijriDay, IReadOnlyList<CalendarEvent> Events, bool InYear);

        public sealed record MonthGrid(int Year, int Month, IReadOnlyList<DayCell?[]> Weeks);

        public sealed record PeriodCount(string Label, int WorkingDays, int TotalDays);

        public AcademicYear Year { get; set; } = null!;

        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();

        public IReadOnlyList<MonthGrid> Months { get; set; } = Array.Empty<MonthGrid>();

        public IReadOnlyList<DayOfWeek> WeekOrder { get; set; } = Array.Empty<DayOfWeek>();

        public IReadOnlyList<PeriodCount> Counters { get; set; } = Array.Empty<PeriodCount>();

        public IReadOnlyList<CalendarEvent> Events { get; set; } = Array.Empty<CalendarEvent>();

        public IReadOnlyList<CalendarVersion> Versions { get; set; } = Array.Empty<CalendarVersion>();

        public bool HijriOverlay { get; set; }

        public bool HasUnpublishedEdits { get; set; }

        public int InstructionalDays { get; set; }

        public int OverrideCount { get; set; }
    }

    public sealed class CalendarDayFormViewModel
    {
        public int AcademicYearId { get; set; }

        public DateTime? Date { get; set; }

        public DateTime? EndDate { get; set; }

        public DayType DayType { get; set; } = DayType.Holiday;

        public CalendarAudience Audience { get; set; } = CalendarAudience.All;

        public bool IsProvisional { get; set; }
    }

    public sealed class CalendarEventFormViewModel
    {
        public int AcademicYearId { get; set; }

        public string? NameAr { get; set; }

        public string? NameEn { get; set; }

        public CalendarEventCategory Category { get; set; } = CalendarEventCategory.SchoolEvent;

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public CalendarAudience Audience { get; set; } = CalendarAudience.All;

        public bool IsPortalVisible { get; set; } = true;

        /// <summary>Also paint the range as Holiday days (national/religious events usually are).</summary>
        public bool MarkAsHoliday { get; set; }
    }

    // ---------------------------------------------------------------- Grades (doc/Modules/05 §8)

    public sealed class GradeLadderViewModel
    {
        public sealed record GradeRow(GradeLevel Grade, GradeLevel? PromotionTarget, GradeYearProfile? Profile, int Sections, int Enrolled);

        public sealed record StageRow(Stage Stage, IReadOnlyList<GradeRow> Grades);

        public IReadOnlyList<StageRow> Stages { get; set; } = Array.Empty<StageRow>();

        public IReadOnlyList<GradeLevel> AllGrades { get; set; } = Array.Empty<GradeLevel>();

        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();

        public AcademicYear? Year { get; set; }

        public IReadOnlyList<(int Id, string Code, string Ar, string En)> Curricula { get; set; } = Array.Empty<(int, string, string, string)>();

        // stage form
        public string? StageNameAr { get; set; }

        public string? StageNameEn { get; set; }

        public int? StageOrder { get; set; }

        public GenderPolicy StageGender { get; set; } = GenderPolicy.Mixed;

        // grade form
        public int? GradeStageId { get; set; }

        public string? GradeCode { get; set; }

        public string? GradeNameAr { get; set; }

        public string? GradeNameEn { get; set; }

        public int? GradeOrder { get; set; }

        public int? PromotionTargetId { get; set; }

        public bool IsGraduating { get; set; }

        // profile form
        public int? ProfileGradeId { get; set; }

        public GenderPolicy ProfileGender { get; set; } = GenderPolicy.Mixed;

        public int? TargetSections { get; set; }

        public int? TargetSectionSize { get; set; }

        public int? CurriculumId { get; set; }

        public decimal? MinAge { get; set; }

        public decimal? MaxAge { get; set; }

        public DateTime? AgeCutoff { get; set; }
    }

    // ---------------------------------------------------------------- Sections (doc/Modules/06 §8)

    public sealed class SectionListViewModel
    {
        public sealed record Row(Section Section, GradeLevel Grade, GradeYearProfile Profile, int Members, string? HomeroomName, string? RoomName);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();

        public AcademicYear? Year { get; set; }

        public IReadOnlyList<(int ProfileId, string GradeAr, string GradeEn, int TargetSections, int TargetSize)> Profiles { get; set; } = Array.Empty<(int, string, string, int, int)>();

        // define form
        public int? GradeYearProfileId { get; set; }

        public string? NameAr { get; set; }

        public string? NameEn { get; set; }

        public int? Capacity { get; set; }

        public GenderPolicy GenderPolicy { get; set; } = GenderPolicy.Mixed;
    }

    public sealed class SectionDetailViewModel
    {
        public sealed record MemberRow(SectionMembership Membership, string StudentNo, string NameAr, string NameEn, int EnrollmentId);

        public sealed record HomeroomRow(HomeroomAssignment Assignment, string TeacherName);

        public sealed record TeacherOption(int? UserAccountId, string NameAr, string NameEn);

        public sealed record EnrollmentOption(int EnrollmentId, string StudentNo, string NameAr, string NameEn);

        public Section Section { get; set; } = null!;

        public GradeLevel Grade { get; set; } = null!;

        public AcademicYear Year { get; set; } = null!;

        public IReadOnlyList<MemberRow> Members { get; set; } = Array.Empty<MemberRow>();

        public IReadOnlyList<MemberRow> PastMembers { get; set; } = Array.Empty<MemberRow>();

        public IReadOnlyList<HomeroomRow> Homerooms { get; set; } = Array.Empty<HomeroomRow>();

        public IReadOnlyList<TeacherOption> Teachers { get; set; } = Array.Empty<TeacherOption>();

        public IReadOnlyList<EnrollmentOption> Unassigned { get; set; } = Array.Empty<EnrollmentOption>();

        public IReadOnlyList<Section> SiblingSections { get; set; } = Array.Empty<Section>();

        public string? RoomName { get; set; }
    }
}
