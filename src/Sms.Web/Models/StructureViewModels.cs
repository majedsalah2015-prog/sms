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

        /// <summary>
        /// One Gregorian month of the board. <c>HijriLabel</c> names the Hijri month or
        /// two that month falls across, and is filled only while the overlay is on — the
        /// grid itself stays Gregorian (ADR-4 / docs/UI/02: Gregorian dates with a Hijri
        /// sub-display, never a calendar swapped in behind the reader).
        /// </summary>
        public sealed record MonthGrid(int Year, int Month, IReadOnlyList<DayCell?[]> Weeks, string? HijriLabel);

        public sealed record PeriodCount(string Label, int WorkingDays, int TotalDays);

        public AcademicYear Year { get; set; } = null!;

        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();

        public IReadOnlyList<MonthGrid> Months { get; set; } = Array.Empty<MonthGrid>();

        public IReadOnlyList<DayOfWeek> WeekOrder { get; set; } = Array.Empty<DayOfWeek>();

        public IReadOnlyList<PeriodCount> Counters { get; set; } = Array.Empty<PeriodCount>();

        /// <summary>Every event of the year, cancelled ones included — the manager lists what was called off (BR-GLB-006).</summary>
        public IReadOnlyList<CalendarEvent> Events { get; set; } = Array.Empty<CalendarEvent>();

        /// <summary>The event the ?edit= id names, when the entry card is amending one instead of adding one.</summary>
        public CalendarEvent? EditEvent { get; set; }

        /// <summary>Today by the application clock — what the list compares against to know an event has already started (BR-CAL-004).</summary>
        public DateTime TodayUtc { get; set; }

        public IReadOnlyList<CalendarVersion> Versions { get; set; } = Array.Empty<CalendarVersion>();

        public bool HijriOverlay { get; set; }

        public bool HasUnpublishedEdits { get; set; }

        public int InstructionalDays { get; set; }

        public int OverrideCount { get; set; }

        /// <summary>
        /// BR-CAL-005: the days still flagged provisional, so the screen can show which Hijri
        /// holidays are still waiting on a confirmed Gregorian date instead of only marking them
        /// ◔ on a grid of three hundred cells.
        /// </summary>
        public IReadOnlyList<CalendarDay> ProvisionalDays { get; set; } = Array.Empty<CalendarDay>();

        /// <summary>
        /// BR-CAL-006: the configured ministry minimum (<c>Regional.MinimumInstructionalDays</c>),
        /// or null when the school has not set one. Compared against the whole-year count — the
        /// setting is one number for the year, so a per-semester comparison would be inventing a
        /// threshold nobody configured.
        /// </summary>
        public int? MinimumInstructionalDays { get; set; }

        /// <summary>
        /// The earliest date the paint tool may write (BR-CAL-003): today, or the year start when
        /// the year has not begun. Null when the year ended — nothing in it can be painted at all,
        /// and a form that refuses every submit is worse than one that says why.
        /// </summary>
        public DateTime? PaintFloor { get; set; }
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

        /// <summary>Set when the card is amending an event rather than adding one; null is "add".</summary>
        public int? Id { get; set; }

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

    /// <summary>
    /// Text the calendar board's Hijri overlay prints (doc/Modules/04 §8.1, BR-CAL-005).
    /// <para>
    /// The month names are written out rather than read off a
    /// <see cref="System.Globalization.CultureInfo"/>. The only culture that would name a
    /// Hijri month is <c>ar-SA</c>, whose default calendar is Umm al-Qura — and asking it
    /// to format a date is how the board's month titles came to read ربيع الأول ١٤٤٧ over a
    /// grid of September's days. Startup pins the request culture's calendar to Gregorian
    /// for exactly that reason (ADR-4), so the Hijri names have to come from somewhere
    /// else; StatisticsLabels made the same call for the Gregorian months.
    /// </para>
    /// </summary>
    public static class CalendarLabels
    {
        private static readonly string[] HijriMonthsEn =
        {
            "Muharram", "Safar", "Rabi' al-Awwal", "Rabi' al-Thani", "Jumada al-Ula", "Jumada al-Akhirah",
            "Rajab", "Sha'ban", "Ramadan", "Shawwal", "Dhu al-Qi'dah", "Dhu al-Hijjah",
        };

        private static readonly string[] HijriMonthsAr =
        {
            "محرم", "صفر", "ربيع الأول", "ربيع الآخر", "جمادى الأولى", "جمادى الآخرة",
            "رجب", "شعبان", "رمضان", "شوال", "ذو القعدة", "ذو الحجة",
        };

        /// <summary>
        /// Hijri month name, 1–12. Out-of-range months render as their number rather than
        /// throwing — a month title is not worth a 500.
        /// </summary>
        public static string HijriMonth(int month, bool isRtl)
            => month >= 1 && month <= 12
                ? (isRtl ? HijriMonthsAr[month - 1] : HijriMonthsEn[month - 1])
                : month.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// Who a day or event applies to (BR-CAL-002). The events list printed the enum name, so
        /// an Arabic reader was told a school trip's audience was "StudentsOnly".
        /// </summary>
        public static string Audience(CalendarAudience audience, bool isRtl) => audience switch
        {
            CalendarAudience.All => isRtl ? "الجميع" : "All",
            CalendarAudience.StudentsOnly => isRtl ? "الطلاب فقط" : "Students only",
            CalendarAudience.StaffOnly => isRtl ? "الموظفون فقط" : "Staff only",
            _ => audience.ToString(),
        };

        /// <summary>
        /// What a day type means to the modules that read it (BR-CAL-001). The board named these
        /// itself while the paint confirmation printed the enum, so an Arabic reader who painted
        /// an exam week was told «تم تعيين ٧ أيام كـ ExamPeriodWorking» — the same leak
        /// <see cref="Audience"/> was written to close. Both now read from here.
        /// </summary>
        public static string DayType(Sms.Domain.Calendar.DayType dayType, bool isRtl) => dayType switch
        {
            Sms.Domain.Calendar.DayType.Working => isRtl ? "عمل" : "Working",
            Sms.Domain.Calendar.DayType.Weekend => isRtl ? "عطلة أسبوعية" : "Weekend",
            Sms.Domain.Calendar.DayType.Holiday => isRtl ? "إجازة" : "Holiday",
            Sms.Domain.Calendar.DayType.Partial => isRtl ? "يوم جزئي" : "Partial day",
            Sms.Domain.Calendar.DayType.ExamPeriodWorking => isRtl ? "فترة اختبارات" : "Exam period",
            _ => dayType.ToString(),
        };

        /// <summary>The event category, in the reader's language (BR-CAL-002).</summary>
        public static string Category(CalendarEventCategory category, bool isRtl) => category switch
        {
            CalendarEventCategory.National => isRtl ? "وطني" : "National",
            CalendarEventCategory.Religious => isRtl ? "ديني" : "Religious",
            CalendarEventCategory.SchoolEvent => isRtl ? "حدث مدرسي" : "School event",
            CalendarEventCategory.ProfessionalDay => isRtl ? "يوم مهني" : "Professional day",
            _ => category.ToString(),
        };
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

    /// <summary>Edit form for a stage (names/order/default gender).</summary>
    public sealed class StageEditViewModel
    {
        public int Id { get; set; }

        public int? Year { get; set; }

        public string? NameAr { get; set; }

        public string? NameEn { get; set; }

        public int? Order { get; set; }

        public GenderPolicy Gender { get; set; } = GenderPolicy.Mixed;

        public int GradeCount { get; set; }
    }

    /// <summary>Edit form for a grade level (stage/code/names/order; promotion path stays on the ladder).</summary>
    public sealed class GradeEditViewModel
    {
        public int Id { get; set; }

        public int? Year { get; set; }

        public int? StageId { get; set; }

        public string? Code { get; set; }

        public string? NameAr { get; set; }

        public string? NameEn { get; set; }

        public int? Order { get; set; }

        public IReadOnlyList<Stage> Stages { get; set; } = Array.Empty<Stage>();
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

    /// <summary>Edit form for a section (names/capacity/gender/room).</summary>
    public sealed class SectionEditViewModel
    {
        public int Id { get; set; }

        public Section Section { get; set; } = null!;

        public string GradeLabelAr { get; set; } = string.Empty;

        public string GradeLabelEn { get; set; } = string.Empty;

        public int PlanSectionSize { get; set; }

        public GenderPolicy GradeGender { get; set; }

        public int CurrentMembers { get; set; }

        public string? NameAr { get; set; }

        public string? NameEn { get; set; }

        public int? Capacity { get; set; }

        public GenderPolicy GenderPolicy { get; set; } = GenderPolicy.Mixed;

        public int? DefaultClassroomId { get; set; }

        public IReadOnlyList<(int Id, string NameAr, string NameEn)> Rooms { get; set; } = Array.Empty<(int, string, string)>();
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
