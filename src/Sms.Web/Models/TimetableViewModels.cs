using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.Timetable;
using Sms.Domain.Classrooms;
using Sms.Domain.Employees;
using Sms.Domain.Grades;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Subjects;
using Sms.Domain.Teachers;
using Sms.Domain.Timetable;

namespace Sms.Web.Models
{
    // ---------------------------------------------------------------- Timetable (doc/Modules/15 §8, E-401)

    /// <summary>Shared label helpers for the Timetable screens (kept here, not in the parallel session's Labels.cs).</summary>
    public static class TimetableLabels
    {
        public static string TeacherName(Employee e, bool isRtl) => isRtl ? $"{e.FirstNameAr} {e.FamilyNameAr}" : $"{e.FirstNameEn} {e.FamilyNameEn}";

        public static string RoomName(Room r, bool isRtl) => $"{r.Code} · {(isRtl ? r.Name.NameAr : r.Name.NameEn)}";

        public static string SubjectName(Subject s, bool isRtl) => isRtl ? s.Name.NameAr : s.Name.NameEn;

        public static string SectionName(Section s, bool isRtl) => isRtl ? s.NameAr : s.NameEn;

        public static string Time(TimeSpan t) => t.ToString("hh\\:mm");

        public static string Span(PeriodSlot s) => $"{Time(s.StartTime)}–{Time(s.EndTime)}";

        public static string Day(DayOfWeek d, bool isRtl) => d switch
        {
            DayOfWeek.Sunday => isRtl ? "الأحد" : "Sunday",
            DayOfWeek.Monday => isRtl ? "الاثنين" : "Monday",
            DayOfWeek.Tuesday => isRtl ? "الثلاثاء" : "Tuesday",
            DayOfWeek.Wednesday => isRtl ? "الأربعاء" : "Wednesday",
            DayOfWeek.Thursday => isRtl ? "الخميس" : "Thursday",
            DayOfWeek.Friday => isRtl ? "الجمعة" : "Friday",
            _ => isRtl ? "السبت" : "Saturday",
        };

        public static string VersionStatus(TimetableVersionStatus s, bool isRtl) => s switch
        {
            TimetableVersionStatus.Draft => isRtl ? "مسودة" : "Draft",
            TimetableVersionStatus.Validated => isRtl ? "مُتحقَّق" : "Validated",
            _ => isRtl ? "منشور" : "Published",
        };

        public static string VersionBadge(TimetableVersionStatus s) => s switch
        {
            TimetableVersionStatus.Draft => "text-bg-secondary",
            TimetableVersionStatus.Validated => "text-bg-info",
            _ => "text-bg-success",
        };

        public static string SessionStatus(SessionStatus s, bool isRtl) => s switch
        {
            Sms.Domain.Timetable.SessionStatus.Held => isRtl ? "منعقدة" : "Held",
            Sms.Domain.Timetable.SessionStatus.Substituted => isRtl ? "مُعوَّضة" : "Substituted",
            Sms.Domain.Timetable.SessionStatus.RoomChanged => isRtl ? "تغيير قاعة" : "Room changed",
            _ => isRtl ? "ملغاة" : "Cancelled",
        };

        public static string SessionBadge(SessionStatus s) => s switch
        {
            Sms.Domain.Timetable.SessionStatus.Held => "text-bg-light border",
            Sms.Domain.Timetable.SessionStatus.Substituted => "text-bg-warning",
            Sms.Domain.Timetable.SessionStatus.RoomChanged => "text-bg-info",
            _ => "text-bg-danger",
        };

        public static string VersionLabel(TimetableVersion v, Term? term, bool isRtl)
        {
            var scope = term == null ? (isRtl ? "العام كاملاً" : "whole year") : (isRtl ? term.NameAr : term.NameEn);
            return $"v{v.Id} · {scope}";
        }

        public static string Warning(TimetableQualityEvaluator.Warning w, bool isRtl) => w.Kind switch
        {
            TimetableQualityEvaluator.WarningKind.SubjectRepeatedSameDay => isRtl ? $"المادة مكررة {w.Magnitude} مرات في اليوم نفسه" : $"Same subject {w.Magnitude}× on one day",
            TimetableQualityEvaluator.WarningKind.TeacherConsecutiveOverMax => isRtl ? $"{w.Magnitude} حصص متتالية للمعلم" : $"{w.Magnitude} consecutive periods for the teacher",
            _ => isRtl ? $"{w.Magnitude} فجوة/فجوات في يوم المعلم" : $"{w.Magnitude} idle gap(s) in the teacher's day",
        };
    }

    /// <summary>One placed cell as the grids render it: placement + resolved subject/teacher/room.</summary>
    public sealed record PlacementCell(Placement Placement, PeriodSlot Slot, Section Section, Subject Subject, CurriculumOffering Offering, Employee Teacher, TeacherProfile Profile, Room? Room);

    public abstract class TimetableYearViewModel
    {
        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();

        public AcademicYear? Year { get; set; }

        public IReadOnlyList<TimetableVersion> Versions { get; set; } = Array.Empty<TimetableVersion>();

        public IReadOnlyDictionary<int, Term> Terms { get; set; } = new Dictionary<int, Term>();

        public TimetableVersion? Version { get; set; }
    }

    // 8.1 Shape designer
    public sealed class ShapeDesignerViewModel : TimetableYearViewModel
    {
        public sealed record StageOption(Stage Stage, TimetableShape? Shape, int SlotCount);

        public IReadOnlyList<StageOption> Stages { get; set; } = Array.Empty<StageOption>();

        public StageOption? Stage { get; set; }

        public IReadOnlyList<DayOfWeek> WorkingDays { get; set; } = Array.Empty<DayOfWeek>();

        /// <summary>Working days first (in week order), then any other weekday that already has slots (a school that teaches on a configured weekend day still sees them).</summary>
        public IReadOnlyList<DayOfWeek> Days { get; set; } = Array.Empty<DayOfWeek>();

        public IReadOnlyList<PeriodSlot> Slots { get; set; } = Array.Empty<PeriodSlot>();

        public IReadOnlySet<int> SlotsInUse { get; set; } = new HashSet<int>();

        public int MaxSequence => Slots.Count == 0 ? 0 : Slots.Max(s => s.SequenceNumber);

        public IReadOnlyList<int> Sequences => Enumerable.Range(1, Math.Max(MaxSequence, 1)).ToList();

        public PeriodSlot? Slot(DayOfWeek day, int seq) => Slots.FirstOrDefault(s => s.DayOfWeek == day && s.SequenceNumber == seq);

        public int SequencesFor(DayOfWeek day) => Slots.Where(s => s.DayOfWeek == day).Select(s => s.SequenceNumber).DefaultIfEmpty(0).Max();
    }

    // 8.2 Timetable builder (section-week grid + teacher/room pivots)
    public sealed class BuilderViewModel : TimetableYearViewModel
    {
        public sealed record SectionOption(Section Section, GradeLevel Grade, int StageId);

        public sealed record TeacherOption(TeacherProfile Profile, Employee Employee);

        public sealed record OfferingRow(CurriculumOffering Offering, Subject Subject, int Required, int Placed, IReadOnlyList<TeacherOption> AssignedTeachers);

        public sealed record SectionMeter(SectionOption Section, int Required, int Placed);

        public string Mode { get; set; } = "section"; // section | teacher | room

        public IReadOnlyList<SectionOption> Sections { get; set; } = Array.Empty<SectionOption>();

        public SectionOption? Section { get; set; }

        public IReadOnlyList<TeacherOption> Teachers { get; set; } = Array.Empty<TeacherOption>();

        public TeacherOption? Teacher { get; set; }

        public IReadOnlyList<Room> Rooms { get; set; } = Array.Empty<Room>();

        public Room? Room { get; set; }

        public bool HasShape { get; set; }

        public IReadOnlyList<DayOfWeek> Days { get; set; } = Array.Empty<DayOfWeek>();

        public IReadOnlyList<int> Sequences { get; set; } = Array.Empty<int>();

        /// <summary>Slots of the grid's shape (section mode) or of every shape in the year (teacher/room pivots), keyed by (day, sequence).</summary>
        public IReadOnlyDictionary<(DayOfWeek Day, int Seq), PeriodSlot> Slots { get; set; } = new Dictionary<(DayOfWeek, int), PeriodSlot>();

        /// <summary>Placements shown in the grid, keyed by (day, sequence). Section mode → at most one; pivots may show several (different stages' shapes share day/sequence).</summary>
        public IReadOnlyDictionary<(DayOfWeek Day, int Seq), IReadOnlyList<PlacementCell>> Cells { get; set; } = new Dictionary<(DayOfWeek, int), IReadOnlyList<PlacementCell>>();

        public IReadOnlyList<OfferingRow> Offerings { get; set; } = Array.Empty<OfferingRow>();

        public IReadOnlyList<SectionMeter> Meters { get; set; } = Array.Empty<SectionMeter>();

        public IReadOnlyList<SectionOption> CopySources { get; set; } = Array.Empty<SectionOption>();

        public int QualityScore { get; set; } = 100;

        public int WarningCount { get; set; }

        public int HardConflictCount { get; set; }

        public bool Editable => Version?.Status == TimetableVersionStatus.Draft;

        public int RequiredTotal => Offerings.Sum(o => o.Required);

        public int PlacedTotal => Offerings.Sum(o => o.Placed);
    }

    // 8.3 Conflict & validation board
    public sealed class ValidationBoardViewModel : TimetableYearViewModel
    {
        public sealed record CompletenessRow(Section Section, Subject Subject, CurriculumOffering Offering, int Required, int Placed, IReadOnlyList<Employee> AssignedTeachers);

        public sealed record HardConflict(string Kind, PeriodSlot Slot, IReadOnlyList<PlacementCell> Cells);

        public sealed record SoftWarning(TimetableQualityEvaluator.Warning Warning, Section? Section, Employee? Teacher, Subject? Subject);

        public IReadOnlyList<CompletenessRow> Completeness { get; set; } = Array.Empty<CompletenessRow>();

        public IReadOnlyList<Section> SectionsWithoutShape { get; set; } = Array.Empty<Section>();

        public IReadOnlyList<HardConflict> HardConflicts { get; set; } = Array.Empty<HardConflict>();

        public IReadOnlyList<SoftWarning> Warnings { get; set; } = Array.Empty<SoftWarning>();

        public int QualityScore { get; set; } = 100;

        public int ShortfallRows => Completeness.Count(c => c.Placed != c.Required);

        public bool ReadyToValidate => Version != null && Version.Status == TimetableVersionStatus.Draft && ShortfallRows == 0 && HardConflicts.Count == 0 && Completeness.Count > 0;
    }

    // 8.4 Publication console
    public sealed class PublishConsoleViewModel : TimetableYearViewModel
    {
        public sealed record VersionRow(TimetableVersion Version, Term? Term, int Placements, int Sessions);

        public sealed record ChecklistItem(string Label, bool Ok, string? Detail = null);

        public sealed record DiffRow(string Kind, Section Section, PeriodSlot Slot, PlacementCell? Before, PlacementCell? After);

        public IReadOnlyList<VersionRow> Rows { get; set; } = Array.Empty<VersionRow>();

        public IReadOnlyList<Term> YearTerms { get; set; } = Array.Empty<Term>();

        public TimetableVersion? CurrentPublished { get; set; }

        public IReadOnlyList<ChecklistItem> Checklist { get; set; } = Array.Empty<ChecklistItem>();

        public IReadOnlyList<DiffRow> Diff { get; set; } = Array.Empty<DiffRow>();

        public DateTime RangeStart { get; set; }

        public DateTime RangeEnd { get; set; }

        public IReadOnlyList<DayOfWeek> WeekendDays { get; set; } = Array.Empty<DayOfWeek>();

        public bool CanPublish => Version?.Status == TimetableVersionStatus.Validated;
    }

    // 8.5 Daily cover console
    public sealed class CoverConsoleViewModel
    {
        public sealed record TeacherOption(TeacherProfile Profile, Employee Employee, int SessionsToday, int Uncovered);

        public sealed record Candidate(TeacherProfile Profile, Employee Employee, bool Qualified);

        public sealed record SessionRow(Session Session, PlacementCell Cell, Room? EffectiveRoom, Substitution? Substitution, Employee? Substitute, IReadOnlyList<Candidate> Candidates);

        public DateTime Date { get; set; }

        public bool IsWorkingDay { get; set; } = true;

        public IReadOnlyList<TeacherOption> Teachers { get; set; } = Array.Empty<TeacherOption>();

        public TeacherOption? AbsentTeacher { get; set; }

        /// <summary>The selected teacher's sessions on the date, with substitute suggestions.</summary>
        public IReadOnlyList<SessionRow> Affected { get; set; } = Array.Empty<SessionRow>();

        /// <summary>Every non-Held session of the day — the staff-room cover summary.</summary>
        public IReadOnlyList<SessionRow> Summary { get; set; } = Array.Empty<SessionRow>();

        public IReadOnlyList<Room> Rooms { get; set; } = Array.Empty<Room>();

        public int TotalSessions { get; set; }

        public bool Print { get; set; }
    }

    // 8.6 Session conflict queue
    public sealed class ConflictQueueViewModel
    {
        public sealed record Row(Session Session, PlacementCell Cell, string Kind, string Detail);

        public DateTime From { get; set; }

        public int Days { get; set; } = 30;

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public IReadOnlyList<Room> Rooms { get; set; } = Array.Empty<Room>();
    }

    // 8.7 Personal views (teacher week / section timetable / room schedule), also the portal's child timetable
    public sealed class PersonalTimetableViewModel
    {
        public string Kind { get; set; } = "section"; // section | teacher | room

        public string Title { get; set; } = string.Empty;

        public string Subtitle { get; set; } = string.Empty;

        public AcademicYear? Year { get; set; }

        public TimetableVersion? Version { get; set; }

        public IReadOnlyList<DayOfWeek> Days { get; set; } = Array.Empty<DayOfWeek>();

        public IReadOnlyList<int> Sequences { get; set; } = Array.Empty<int>();

        public IReadOnlyDictionary<(DayOfWeek Day, int Seq), PeriodSlot> Slots { get; set; } = new Dictionary<(DayOfWeek, int), PeriodSlot>();

        public IReadOnlyDictionary<(DayOfWeek Day, int Seq), IReadOnlyList<PlacementCell>> Cells { get; set; } = new Dictionary<(DayOfWeek, int), IReadOnlyList<PlacementCell>>();

        /// <summary>This week's dated overlays (substitution / room change / cancellation) keyed by placement id → session, so live changes show on the weekly pattern (BR-TTB-008 "visible on all affected views immediately").</summary>
        public IReadOnlyDictionary<int, Session> WeekSessions { get; set; } = new Dictionary<int, Session>();

        public IReadOnlyDictionary<int, Room> Rooms { get; set; } = new Dictionary<int, Room>();

        public IReadOnlyDictionary<int, Employee> Substitutes { get; set; } = new Dictionary<int, Employee>();

        public DateTime WeekStart { get; set; }

        public bool Print { get; set; }

        public int? BackId { get; set; }
    }
}
