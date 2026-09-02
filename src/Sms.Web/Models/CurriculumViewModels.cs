using System;
using System.Collections.Generic;
using Sms.Domain.Classrooms;
using Sms.Domain.Grades;
using Sms.Domain.Schools;
using Sms.Domain.Subjects;

namespace Sms.Web.Models
{
    // ---------------------------------------------------------------- Subjects (doc/Modules/07 §8)

    public sealed class SubjectCatalogViewModel
    {
        public sealed record SubjectRow(Subject Subject, Department? Department, int Offerings, int QualifiedTeachers);

        public sealed record TeacherOption(int? UserAccountId, string NameAr, string NameEn);

        public IReadOnlyList<SubjectRow> Subjects { get; set; } = Array.Empty<SubjectRow>();

        public IReadOnlyList<Department> Departments { get; set; } = Array.Empty<Department>();

        public IReadOnlyList<TeacherOption> Teachers { get; set; } = Array.Empty<TeacherOption>();

        public IReadOnlyList<Stage> Stages { get; set; } = Array.Empty<Stage>();

        /// <summary>Qualification matrix: teacherUserId → subjectId → stage names.</summary>
        public IReadOnlyDictionary<int, IReadOnlyDictionary<int, IReadOnlyList<string>>> Matrix { get; set; } = new Dictionary<int, IReadOnlyDictionary<int, IReadOnlyList<string>>>();

        public string ActiveTab { get; set; } = "catalog";

        public int? DepartmentFilter { get; set; }

        public static readonly string[] Categories = { "core", "elective", "religious", "language", "activity" };

        /// <summary>Display label for a category code (the stored value stays the English code).</summary>
        public static string CategoryLabel(string? code, bool arabic) => (code ?? string.Empty).ToLowerInvariant() switch
        {
            "core" => arabic ? "أساسية" : "Core",
            "elective" => arabic ? "اختيارية" : "Elective",
            "religious" => arabic ? "دينية" : "Religious",
            "language" => arabic ? "لغات" : "Language",
            "activity" => arabic ? "نشاط" : "Activity",
            "arts" => arabic ? "فنون" : "Arts",
            "pe" => arabic ? "تربية بدنية" : "PE",
            _ => code ?? string.Empty,
        };

        // subject form
        public string? Code { get; set; }

        public string? NameAr { get; set; }

        public string? NameEn { get; set; }

        public string? Category { get; set; } = "core";

        public int? DepartmentId { get; set; }

        // department form
        public string? DeptNameAr { get; set; }

        public string? DeptNameEn { get; set; }

        public int? HeadTeacherUserId { get; set; }

        // qualification form
        public int? QTeacherUserId { get; set; }

        public int? QSubjectId { get; set; }

        public int? QStageId { get; set; }

        public QualificationSource QSource { get; set; } = QualificationSource.Qualification;
    }

    /// <summary>Edit form for a catalog subject.</summary>
    public sealed class SubjectEditViewModel
    {
        public int Id { get; set; }

        public string? Code { get; set; }

        public string? NameAr { get; set; }

        public string? NameEn { get; set; }

        public string? Category { get; set; }

        public int? DepartmentId { get; set; }

        public int CurrentOfferings { get; set; }

        public IReadOnlyList<Department> Departments { get; set; } = Array.Empty<Department>();
    }

    /// <summary>Edit form for a department.</summary>
    public sealed class DepartmentEditViewModel
    {
        public int Id { get; set; }

        public string? NameAr { get; set; }

        public string? NameEn { get; set; }

        public int? HeadTeacherUserId { get; set; }

        public int SubjectCount { get; set; }

        public IReadOnlyList<SubjectCatalogViewModel.TeacherOption> Teachers { get; set; } = Array.Empty<SubjectCatalogViewModel.TeacherOption>();
    }

    public sealed class CurriculumPlanViewModel
    {
        /// <summary>
        /// <paramref name="SubjectIsRetired"/> marks an offering whose subject has since been
        /// deactivated. The offering is still real and still counts toward the week — deactivating a
        /// subject stops new offerings, it does not erase the ones already made — so the row is shown
        /// and labelled rather than hidden or, as it was, thrown over.
        /// </summary>
        public sealed record OfferingRow(CurriculumOffering Offering, Subject Subject, bool SubjectIsRetired = false);

        /// <summary>
        /// One grade-year the plan can be written for. <paramref name="Stage"/> rides along because a
        /// grade code alone ("G3") identifies a grade only to whoever wrote the codes — the rest of the
        /// product names the stage beside it, and on this screen the grade *is* the axis being edited.
        /// Nullable so a stage row that has gone missing degrades to "no stage shown" rather than
        /// taking the picker down.
        /// </summary>
        public sealed record ProfileOption(int ProfileId, GradeLevel Grade, Stage? Stage);

        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();

        public AcademicYear? Year { get; set; }

        public IReadOnlyList<ProfileOption> Profiles { get; set; } = Array.Empty<ProfileOption>();

        public ProfileOption? Profile { get; set; }

        /// <summary>
        /// The <c>?year=</c> in the address named a year this school does not have, so the screen fell
        /// back to another one. Falling back is deliberate — a bookmarked id that has gone stale should
        /// not produce an error page — but a *silent* fallback is how somebody edits the wrong plan, so
        /// the screen says it happened (doc/Modules/07 §8.2).
        /// </summary>
        public bool YearFellBack { get; set; }

        /// <summary>
        /// Same for <c>?profile=</c>, and it is the common one: the two pickers submit together, so
        /// changing the year carries the old year's grade id into a year that has no profile for it.
        /// </summary>
        public bool ProfileFellBack { get; set; }

        public IReadOnlyList<OfferingRow> Offerings { get; set; } = Array.Empty<OfferingRow>();

        /// <summary>Active subjects only: this is what the "add an offering" picker offers, and a retired subject is not something to start offering.</summary>
        public IReadOnlyList<Subject> Subjects { get; set; } = Array.Empty<Subject>();

        public int TotalPeriods { get; set; }

        public int AvailableSlots { get; set; } = 35;

        public int? PreviousYearProfileId { get; set; }

        public string? PreviousYearLabel { get; set; }

        // add offering form
        public int? SubjectId { get; set; }

        public int? WeeklyPeriods { get; set; } = 4;

        public bool IsAssessable { get; set; } = true;

        public decimal? GpaWeight { get; set; } = 1m;

        public bool IsElective { get; set; }

        public string? ElectiveGroupTag { get; set; }
    }

    // ---------------------------------------------------------------- Classrooms (doc/Modules/08 §8)

    public sealed class RoomCatalogViewModel
    {
        public sealed record RoomRow(Room Room, string TypeName, IReadOnlyList<string> Features, bool UnavailableNow, int? SectionsUsing);

        public sealed record FloorNode(Floor Floor, IReadOnlyList<RoomRow> Rooms);

        public sealed record BuildingNode(Building Building, IReadOnlyList<FloorNode> Floors);

        public IReadOnlyList<BuildingNode> Buildings { get; set; } = Array.Empty<BuildingNode>();

        public IReadOnlyList<(int Id, string Ar, string En)> RoomTypes { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<(int Id, string BuildingAr, string BuildingEn, string Ar, string En)> Floors { get; set; } = Array.Empty<(int, string, string, string, string)>();

        /// <summary>
        /// Floor id → the next free room code on it (BR-ROM-001). The form fills Code from
        /// this when a floor is chosen, so nobody types "A-101" thirty times; the field stays
        /// editable, and a blank one is filled server-side from the same map.
        /// </summary>
        public IReadOnlyDictionary<int, string> NextRoomCodes { get; set; } = new Dictionary<int, string>();

        public int TotalRooms { get; set; }

        public int TotalSeats { get; set; }

        // forms
        public string? BuildingNameAr { get; set; }

        public string? BuildingNameEn { get; set; }

        public int? FloorBuildingId { get; set; }

        public string? FloorNameAr { get; set; }

        public string? FloorNameEn { get; set; }

        public int? FloorOrder { get; set; }

        public int? RoomFloorId { get; set; }

        public string? RoomCode { get; set; }

        public string? RoomNameAr { get; set; }

        public string? RoomNameEn { get; set; }

        public int? RoomTypeId { get; set; }

        public int? StandardCapacity { get; set; } = 30;

        public int? ExamCapacity { get; set; } = 20;

        public GenderPolicy WingTag { get; set; } = GenderPolicy.Mixed;
    }

    /// <summary>Edit forms for the room catalog tree (building / floor / room).</summary>
    public sealed class RoomEditViewModel
    {
        public int Id { get; set; }

        /// <summary>"building" | "floor" | "room" — decides which fields the form shows.</summary>
        public string Kind { get; set; } = "room";

        public string? NameAr { get; set; }

        public string? NameEn { get; set; }

        // floor
        public int? BuildingId { get; set; }

        public int? Order { get; set; }

        // room
        public int? FloorId { get; set; }

        public string? Code { get; set; }

        public int? RoomTypeId { get; set; }

        public int? StandardCapacity { get; set; }

        public int? ExamCapacity { get; set; }

        public GenderPolicy WingTag { get; set; } = GenderPolicy.Mixed;

        public int ChildCount { get; set; }

        public IReadOnlyList<Building> Buildings { get; set; } = Array.Empty<Building>();

        public IReadOnlyList<(int Id, string BuildingAr, string BuildingEn, string Ar, string En)> Floors { get; set; } = Array.Empty<(int, string, string, string, string)>();

        public IReadOnlyList<(int Id, string Ar, string En)> RoomTypes { get; set; } = Array.Empty<(int, string, string)>();
    }

    public sealed class RoomDetailViewModel
    {
        public Room Room { get; set; } = null!;

        public Floor Floor { get; set; } = null!;

        public Building Building { get; set; } = null!;

        public string TypeName { get; set; } = string.Empty;

        public IReadOnlyList<(int Id, string Ar, string En)> Features { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<(int Id, string Ar, string En)> AvailableFeatures { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<RoomAvailabilityException> Unavailability { get; set; } = Array.Empty<RoomAvailabilityException>();

        /// <summary>
        /// doc/Modules/08 §8.3: how many timetabled sessions each maintenance window
        /// covers. Keyed by the window's id. The number is the whole point of the
        /// window — closing a room for a fortnight is a different decision when it
        /// costs forty sessions than when it costs none — and it was missing.
        /// </summary>
        public IReadOnlyDictionary<int, int> ImpactedSessions { get; set; } = new Dictionary<int, int>();

        /// <summary>
        /// doc/Modules/08 §8.2's "sessions from timetable overlaid read-only": the
        /// room's own week off the published version. Read-only here because a room
        /// does not own its timetable — Module 15 does.
        /// </summary>
        public IReadOnlyList<RoomWeekSlot> Week { get; set; } = Array.Empty<RoomWeekSlot>();

        public IReadOnlyList<RoomBooking> Bookings { get; set; } = Array.Empty<RoomBooking>();

        public IReadOnlyList<Sms.Domain.Sections.Section> SectionsUsing { get; set; } = Array.Empty<Sms.Domain.Sections.Section>();

        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();

        public int? WorkingYearId { get; set; }
    }
}
