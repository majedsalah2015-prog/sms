using System;
using System.Collections.Generic;
using Sms.Domain.Attendance;
using Sms.Domain.Audit;
using Sms.Domain.Calendar;
using Sms.Domain.Grades;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Students;

namespace Sms.Web.Models
{
    // ---------------------------------------------------------------- Attendance (doc/Modules/14 §8, E-301 screens)

    /// <summary>
    /// Date + calendar context every attendance screen needs. The
    /// working-day question (BR-ATD-003, "records exist only on
    /// audience-working days") is answered from E-103's materialized
    /// core.CalendarDay rather than re-deriving week logic — which is
    /// CalendarDayResolver's own standing instruction to consumers.
    /// </summary>
    public abstract class AttendancePageViewModel
    {
        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();

        public AcademicYear? Year { get; set; }

        public DateTime Date { get; set; }

        /// <summary>Null = the year's calendar has no row for this date (unmaterialized).</summary>
        public DayType? DayType { get; set; }

        /// <summary>BR-ATD-010: exam-period days count as working days.</summary>
        public bool IsWorkingDay => DayType == Sms.Domain.Calendar.DayType.Working
            || DayType == Sms.Domain.Calendar.DayType.Partial
            || DayType == Sms.Domain.Calendar.DayType.ExamPeriodWorking;

        public bool DateInYear { get; set; } = true;
    }

    // ---- 8.3 Attendance monitor (supervisor live board) ----

    public sealed class AttendanceMonitorViewModel : AttendancePageViewModel
    {
        public sealed record SectionRow(
            Section Section, GradeLevel? Grade, int Expected, int Captured, int Absent, int Late, bool AnyLocked);

        public sealed record AbsenceRow(
            AttendanceDay Day, Student Student, Section? Section, GradeLevel? Grade, JustificationReviewState? Justification);

        /// <summary>BR-ATD-008 consecutive-absence threshold, evaluated by ConsecutiveAbsenceEscalationEvaluator.</summary>
        public sealed record AlertRow(Student Student, Section? Section, int Streak, int EnrollmentId);

        public IReadOnlyList<SectionRow> Sections { get; set; } = Array.Empty<SectionRow>();

        public IReadOnlyList<AbsenceRow> Absences { get; set; } = Array.Empty<AbsenceRow>();

        public IReadOnlyList<AlertRow> Alerts { get; set; } = Array.Empty<AlertRow>();

        public Dictionary<AttendanceStatus, int> CountsByStatus { get; set; } = new();

        public int ConsecutiveThreshold { get; set; } = 3;

        public int PendingJustifications { get; set; }

        public int OpenLeavePasses { get; set; }

        public int LockedRows { get; set; }

        public int ExpectedTotal { get; set; }

        public int CapturedTotal { get; set; }
    }

    // ---- 8.1 Section capture sheet ----

    public sealed class AttendanceCaptureViewModel : AttendancePageViewModel
    {
        public sealed record SectionOption(Section Section, GradeLevel? Grade);

        /// <summary>One roster line. Existing null = not captured yet (the sheet defaults it to Present).</summary>
        public sealed record Row(int EnrollmentId, Student Student, AttendanceDay? Existing, bool HasOpenLeavePass);

        public IReadOnlyList<SectionOption> AllSections { get; set; } = Array.Empty<SectionOption>();

        public int? SectionId { get; set; }

        public Section? Section { get; set; }

        public GradeLevel? Grade { get; set; }

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public int CapturedCount { get; set; }

        public bool AnyLocked { get; set; }
    }

    // ---- 8.2 Gate console ----

    public sealed class AttendanceGateViewModel : AttendancePageViewModel
    {
        public sealed record StudentHit(int EnrollmentId, Student Student, Section? Section, GradeLevel? Grade, AttendanceDay? Today);

        /// <summary>BR-ATD-004 / BR-PAR-008: a person cleared to collect this student.</summary>
        public sealed record PickupOption(string NameAr, string NameEn, string Kind, string? Phone);

        public sealed record EventRow(GateEvent Event, Student? Student, Section? Section);

        public sealed record PassRow(LeavePass Pass, Student? Student, Section? Section);

        public string? Query { get; set; }

        public IReadOnlyList<StudentHit> Hits { get; set; } = Array.Empty<StudentHit>();

        public StudentHit? Selected { get; set; }

        public IReadOnlyList<PickupOption> PickupList { get; set; } = Array.Empty<PickupOption>();

        public IReadOnlyList<EventRow> TodaysEvents { get; set; } = Array.Empty<EventRow>();

        public IReadOnlyList<PassRow> Passes { get; set; } = Array.Empty<PassRow>();
    }

    // ---- 8.4 Justification review queue ----

    public sealed class JustificationQueueViewModel : AttendancePageViewModel
    {
        public sealed record Row(
            Justification Justification, AttendanceDay Day, Student? Student, Section? Section, int DaysToSubmit, bool OutsideWindow);

        /// <summary>An absence with no justification yet — the counter-submission ("paper at the counter") picker.</summary>
        public sealed record AbsenceOption(int AttendanceDayId, Student Student, DateTime Date, AttendanceStatus Status);

        public JustificationReviewState? State { get; set; }

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public Dictionary<JustificationReviewState, int> CountsByState { get; set; } = new();

        public IReadOnlyList<AbsenceOption> OpenAbsences { get; set; } = Array.Empty<AbsenceOption>();

        /// <summary>BR-ATD-005 submission window, in days (doc default 3).</summary>
        public int WindowDays { get; set; } = 3;
    }

    // ---- 8.5 Correction screen (WF-14) ----

    public sealed class AttendanceCorrectionsViewModel : AttendancePageViewModel
    {
        public sealed record Row(AttendanceDay Day, Student? Student, Section? Section, GradeLevel? Grade);

        public sealed record RegisterRow(AuditEntry Entry, Student? Student, DateTime? Date, Section? Section);

        public sealed record SectionOption(Section Section, GradeLevel? Grade);

        public DateTime From { get; set; }

        public DateTime To { get; set; }

        public int? SectionId { get; set; }

        public string? Query { get; set; }

        public bool LockedOnly { get; set; } = true;

        public IReadOnlyList<SectionOption> AllSections { get; set; } = Array.Empty<SectionOption>();

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        /// <summary>doc §10 "Correction register (WF-14 audit view)" — the AttendanceDay.Status field-level entries.</summary>
        public IReadOnlyList<RegisterRow> Register { get; set; } = Array.Empty<RegisterRow>();
    }

    // ---- 8.6 Analytics ----

    public sealed class AttendanceAnalyticsViewModel : AttendancePageViewModel
    {
        public sealed record SectionStat(
            Section Section, GradeLevel? Grade, int Records, int Absent, int Unexcused, int Late, int Exempt, decimal Percent);

        public sealed record StudentStat(
            Student Student, Section? Section, GradeLevel? Grade, int Records, int Absent, int Unexcused, int Late, decimal Percent);

        public sealed record WeekdayStat(DayOfWeek Day, int Records, int Absent, int Late);

        public DateTime From { get; set; }

        public DateTime To { get; set; }

        public IReadOnlyList<Term> Terms { get; set; } = Array.Empty<Term>();

        public int? TermId { get; set; }

        public int? SectionId { get; set; }

        public IReadOnlyList<AttendanceCorrectionsViewModel.SectionOption> AllSections { get; set; }
            = Array.Empty<AttendanceCorrectionsViewModel.SectionOption>();

        public IReadOnlyList<SectionStat> SectionStats { get; set; } = Array.Empty<SectionStat>();

        public IReadOnlyList<StudentStat> Chronic { get; set; } = Array.Empty<StudentStat>();

        public IReadOnlyList<StudentStat> LateLeaders { get; set; } = Array.Empty<StudentStat>();

        public IReadOnlyList<WeekdayStat> Weekdays { get; set; } = Array.Empty<WeekdayStat>();

        /// <summary>doc §10 "chronic (>= X%)" list — attendance below this is flagged.</summary>
        public decimal ChronicBelowPercent { get; set; } = 90m;

        public int TotalRecords { get; set; }

        public decimal OverallPercent { get; set; }
    }

    public static class AttendanceLabels
    {
        public static string Status(AttendanceStatus s, bool arabic) => s switch
        {
            AttendanceStatus.Present => arabic ? "حاضر" : "Present",
            AttendanceStatus.Late => arabic ? "متأخر" : "Late",
            AttendanceStatus.AbsentExcused => arabic ? "غياب بعذر" : "Absent (excused)",
            AttendanceStatus.AbsentUnexcused => arabic ? "غياب بدون عذر" : "Absent (unexcused)",
            AttendanceStatus.MedicalLeave => arabic ? "إجازة مرضية" : "Medical leave",
            AttendanceStatus.Permission => arabic ? "استئذان" : "Permission",
            AttendanceStatus.EarlyLeave => arabic ? "خروج مبكر" : "Early leave",
            AttendanceStatus.Exempted => arabic ? "معفى" : "Exempted",
            _ => s.ToString(),
        };

        public static string StatusBadge(AttendanceStatus s) => s switch
        {
            AttendanceStatus.Present => "text-bg-success",
            AttendanceStatus.Late => "text-bg-warning",
            AttendanceStatus.AbsentExcused => "text-bg-info",
            AttendanceStatus.AbsentUnexcused => "text-bg-danger",
            AttendanceStatus.MedicalLeave => "text-bg-info",
            AttendanceStatus.Permission => "text-bg-secondary",
            AttendanceStatus.EarlyLeave => "text-bg-secondary",
            AttendanceStatus.Exempted => "text-bg-light border",
            _ => "text-bg-light",
        };

        public static string StatusIcon(AttendanceStatus s) => s switch
        {
            AttendanceStatus.Present => "bi-check-lg",
            AttendanceStatus.Late => "bi-clock",
            AttendanceStatus.AbsentExcused => "bi-file-earmark-check",
            AttendanceStatus.AbsentUnexcused => "bi-x-lg",
            AttendanceStatus.MedicalLeave => "bi-heart-pulse",
            AttendanceStatus.Permission => "bi-box-arrow-right",
            AttendanceStatus.EarlyLeave => "bi-door-open",
            AttendanceStatus.Exempted => "bi-dash-lg",
            _ => "bi-question",
        };

        /// <summary>The one-tap chip set on the capture sheet — the full BR-ATD-002 taxonomy.</summary>
        public static readonly AttendanceStatus[] CaptureChips =
        {
            AttendanceStatus.Present,
            AttendanceStatus.Late,
            AttendanceStatus.AbsentUnexcused,
            AttendanceStatus.AbsentExcused,
            AttendanceStatus.MedicalLeave,
            AttendanceStatus.Permission,
            AttendanceStatus.EarlyLeave,
            AttendanceStatus.Exempted,
        };

        public static string JustificationState(JustificationReviewState s, bool arabic) => s switch
        {
            JustificationReviewState.Submitted => arabic ? "مُقدَّم" : "Submitted",
            JustificationReviewState.Accepted => arabic ? "مقبول" : "Accepted",
            JustificationReviewState.Rejected => arabic ? "مرفوض" : "Rejected",
            _ => s.ToString(),
        };

        public static string JustificationBadge(JustificationReviewState s) => s switch
        {
            JustificationReviewState.Submitted => "text-bg-warning",
            JustificationReviewState.Accepted => "text-bg-success",
            JustificationReviewState.Rejected => "text-bg-danger",
            _ => "text-bg-light",
        };

        public static string JustificationTypeName(JustificationType t, bool arabic) => t switch
        {
            JustificationType.Excuse => arabic ? "عذر" : "Excuse",
            JustificationType.Medical => arabic ? "تقرير طبي" : "Medical",
            _ => t.ToString(),
        };

        public static string LeavePass(LeavePassStatus s, bool arabic) => s switch
        {
            LeavePassStatus.Requested => arabic ? "مطلوب" : "Requested",
            LeavePassStatus.Approved => arabic ? "معتمد" : "Approved",
            LeavePassStatus.Released => arabic ? "خرج" : "Released",
            LeavePassStatus.Returned => arabic ? "عاد" : "Returned",
            LeavePassStatus.Rejected => arabic ? "مرفوض" : "Rejected",
            _ => s.ToString(),
        };

        public static string LeavePassBadge(LeavePassStatus s) => s switch
        {
            LeavePassStatus.Requested => "text-bg-warning",
            LeavePassStatus.Approved => "text-bg-primary",
            LeavePassStatus.Released => "text-bg-info",
            LeavePassStatus.Returned => "text-bg-success",
            LeavePassStatus.Rejected => "text-bg-danger",
            _ => "text-bg-light",
        };

        public static string GateEvent(GateEventType t, bool arabic) => t switch
        {
            GateEventType.Late => arabic ? "وصول متأخر" : "Late arrival",
            GateEventType.EarlyLeaveRelease => arabic ? "خروج مبكر" : "Early release",
            _ => t.ToString(),
        };

        public static string CalendarDayType(DayType d, bool arabic) => d switch
        {
            DayType.Working => arabic ? "يوم دراسي" : "Working day",
            DayType.Weekend => arabic ? "عطلة نهاية الأسبوع" : "Weekend",
            DayType.Holiday => arabic ? "إجازة" : "Holiday",
            DayType.Partial => arabic ? "يوم مختصر" : "Short day",
            DayType.ExamPeriodWorking => arabic ? "يوم اختبارات" : "Exam day",
            _ => d.ToString(),
        };

        public static string Weekday(DayOfWeek d, bool arabic) => d switch
        {
            DayOfWeek.Sunday => arabic ? "الأحد" : "Sunday",
            DayOfWeek.Monday => arabic ? "الاثنين" : "Monday",
            DayOfWeek.Tuesday => arabic ? "الثلاثاء" : "Tuesday",
            DayOfWeek.Wednesday => arabic ? "الأربعاء" : "Wednesday",
            DayOfWeek.Thursday => arabic ? "الخميس" : "Thursday",
            DayOfWeek.Friday => arabic ? "الجمعة" : "Friday",
            _ => arabic ? "السبت" : "Saturday",
        };

        /// <summary>Local to the attendance screens so the shared PeopleViewModels helper stays untouched.</summary>
        public static string StudentName(Student s, bool arabic) => arabic
            ? string.Join(" ", s.FirstNameAr, s.FatherNameAr, s.FamilyNameAr)
            : string.Join(" ", s.FirstNameEn, s.FatherNameEn, s.FamilyNameEn);
    }
}
