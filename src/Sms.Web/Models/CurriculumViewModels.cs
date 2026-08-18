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

    public sealed class CurriculumPlanViewModel
    {
        public sealed record OfferingRow(CurriculumOffering Offering, Subject Subject);

        public sealed record ProfileOption(int ProfileId, GradeLevel Grade);

        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();

        public AcademicYear? Year { get; set; }

        public IReadOnlyList<ProfileOption> Profiles { get; set; } = Array.Empty<ProfileOption>();

        public ProfileOption? Profile { get; set; }

        public IReadOnlyList<OfferingRow> Offerings { get; set; } = Array.Empty<OfferingRow>();

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

    public sealed class RoomDetailViewModel
    {
        public Room Room { get; set; } = null!;

        public Floor Floor { get; set; } = null!;

        public Building Building { get; set; } = null!;

        public string TypeName { get; set; } = string.Empty;

        public IReadOnlyList<(int Id, string Ar, string En)> Features { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<(int Id, string Ar, string En)> AvailableFeatures { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<RoomAvailabilityException> Unavailability { get; set; } = Array.Empty<RoomAvailabilityException>();

        public IReadOnlyList<RoomBooking> Bookings { get; set; } = Array.Empty<RoomBooking>();

        public IReadOnlyList<Sms.Domain.Sections.Section> SectionsUsing { get; set; } = Array.Empty<Sms.Domain.Sections.Section>();

        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();

        public int? WorkingYearId { get; set; }
    }
}
