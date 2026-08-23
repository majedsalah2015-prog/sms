using System;
using System.Collections.Generic;
using Sms.Domain.Common;
using Sms.Domain.Employees;
using Sms.Domain.Grades;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Subjects;
using Sms.Domain.Teachers;

namespace Sms.Web.Models
{
    // ---------------------------------------------------------------- Employees (doc/Modules/12 §8, E-203)

    public sealed class EmployeeListViewModel
    {
        public sealed record Row(Employee Employee, string? Position, string? OrgUnit, Contract? CurrentContract, bool IsTeacher, string NationalityName);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public string? Query { get; set; }

        public EmployeeStatus? Status { get; set; }

        public int? OrgUnitId { get; set; }

        public bool? TeachersOnly { get; set; }

        public IReadOnlyList<OrgUnit> OrgUnits { get; set; } = Array.Empty<OrgUnit>();

        public int Total { get; set; }
    }

    public sealed class EmployeeFormViewModel
    {
        public string? FirstNameAr { get; set; }
        public string? FatherNameAr { get; set; }
        public string? GrandfatherNameAr { get; set; }
        public string? FamilyNameAr { get; set; }
        public string? FirstNameEn { get; set; }
        public string? FatherNameEn { get; set; }
        public string? GrandfatherNameEn { get; set; }
        public string? FamilyNameEn { get; set; }
        public Gender Gender { get; set; } = Gender.Male;
        public DateTime? DateOfBirth { get; set; }
        public int? NationalityLookupId { get; set; }
        public int? PrimaryIdTypeLookupId { get; set; }
        public string? PrimaryIdNo { get; set; }
        public DateTime? PrimaryIdExpiry { get; set; }
        public int? UserAccountId { get; set; }
        public string? Reason { get; set; }

        // first position (optional at registration)
        public int? OrgUnitId { get; set; }
        public int? PositionLookupId { get; set; }
        public int? ManagerEmployeeId { get; set; }

        public IReadOnlyList<(int Id, string Ar, string En)> Nationalities { get; set; } = Array.Empty<(int, string, string)>();
        public IReadOnlyList<(int Id, string Ar, string En)> IdTypes { get; set; } = Array.Empty<(int, string, string)>();
        public IReadOnlyList<(int Id, string Ar, string En)> Positions { get; set; } = Array.Empty<(int, string, string)>();
        public IReadOnlyList<OrgUnit> OrgUnits { get; set; } = Array.Empty<OrgUnit>();
        public IReadOnlyList<Employee> Managers { get; set; } = Array.Empty<Employee>();
    }

    public sealed class EmployeeFileViewModel
    {
        public sealed record AssignmentRow(EmployeeAssignment Assignment, OrgUnit? OrgUnit, string? Position, Employee? Manager);

        public sealed record TeachingRow(TeacherAssignment Assignment, Subject? Subject, Section? Section, CurriculumOffering? Offering);

        public Employee Employee { get; set; } = null!;

        public string ActiveTab { get; set; } = "personal";

        public string NationalityName { get; set; } = "?";

        public string? IdTypeName { get; set; }

        public IReadOnlyList<AssignmentRow> Assignments { get; set; } = Array.Empty<AssignmentRow>();

        public IReadOnlyList<Contract> Contracts { get; set; } = Array.Empty<Contract>();

        public IReadOnlyList<Qualification> Qualifications { get; set; } = Array.Empty<Qualification>();

        public TeacherProfile? TeacherProfile { get; set; }

        public IReadOnlyList<TeachingRow> Teaching { get; set; } = Array.Empty<TeachingRow>();

        public int CurrentLoad { get; set; }

        public bool HasActiveContract { get; set; }

        public IReadOnlyList<EmployeeStatus> AllowedTransitions { get; set; } = Array.Empty<EmployeeStatus>();

        public IReadOnlyList<(string Action, string? Field, string? Old, string? New, DateTime At, int Actor, string? Reason)> Audit { get; set; } = Array.Empty<(string, string?, string?, string?, DateTime, int, string?)>();

        public IReadOnlyList<(int Id, string Ar, string En)> Nationalities { get; set; } = Array.Empty<(int, string, string)>();
        public IReadOnlyList<(int Id, string Ar, string En)> IdTypes { get; set; } = Array.Empty<(int, string, string)>();
        public IReadOnlyList<(int Id, string Ar, string En)> Positions { get; set; } = Array.Empty<(int, string, string)>();
        public IReadOnlyList<OrgUnit> OrgUnits { get; set; } = Array.Empty<OrgUnit>();
        public IReadOnlyList<Employee> Managers { get; set; } = Array.Empty<Employee>();
    }

    public sealed class OrgChartViewModel
    {
        public sealed record Node(OrgUnit Unit, int Depth, int HeadCount, int ChildCount, IReadOnlyList<(Employee Employee, string? Position)> Current);

        public IReadOnlyList<Node> Nodes { get; set; } = Array.Empty<Node>();

        public IReadOnlyList<OrgUnit> All { get; set; } = Array.Empty<OrgUnit>();

        public int Unassigned { get; set; }
    }

    public sealed class ContractManagerViewModel
    {
        public sealed record Row(Contract Contract, Employee Employee, int DaysToEnd, bool IsExpired, bool HasSuccessor);

        public IReadOnlyList<Row> Drafts { get; set; } = Array.Empty<Row>();

        public IReadOnlyList<Row> Expiring { get; set; } = Array.Empty<Row>();

        public IReadOnlyList<Row> Active { get; set; } = Array.Empty<Row>();

        public IReadOnlyList<Row> Expired { get; set; } = Array.Empty<Row>();

        public int WindowDays { get; set; } = 90;
    }

    // ---------------------------------------------------------------- Teachers (doc/Modules/13 §8, E-203)

    public sealed class TeacherDirectoryViewModel
    {
        public sealed record Row(TeacherProfile Profile, Employee Employee, int Load, IReadOnlyList<string> Subjects, int SectionCount, string? Homeroom, bool HasActiveContract);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public IReadOnlyList<Employee> Designatable { get; set; } = Array.Empty<Employee>();

        public AcademicYear? Year { get; set; }

        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();
    }

    public sealed class AssignmentMatrixViewModel
    {
        public sealed record ProfileOption(int ProfileId, GradeLevel Grade);

        public sealed record TeacherOption(TeacherProfile Profile, Employee Employee, int Load, bool QualifiedUnknown, IReadOnlyList<int> QualifiedSubjectIds);

        public sealed record Cell(TeacherAssignment Assignment, TeacherOption Teacher);

        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();

        public AcademicYear? Year { get; set; }

        public IReadOnlyList<ProfileOption> Profiles { get; set; } = Array.Empty<ProfileOption>();

        public ProfileOption? Profile { get; set; }

        public IReadOnlyList<(CurriculumOffering Offering, Subject Subject)> Offerings { get; set; } = Array.Empty<(CurriculumOffering, Subject)>();

        public IReadOnlyList<Section> Sections { get; set; } = Array.Empty<Section>();

        /// <summary>Current assignments keyed by (offeringId, sectionId).</summary>
        public IReadOnlyDictionary<(int, int), IReadOnlyList<Cell>> Cells { get; set; } = new Dictionary<(int, int), IReadOnlyList<Cell>>();

        public IReadOnlyList<TeacherOption> Teachers { get; set; } = Array.Empty<TeacherOption>();

        public int? PreviousYearProfileId { get; set; }
    }

    public sealed class LoadBoardViewModel
    {
        public sealed record Row(TeacherProfile Profile, Employee Employee, int Load, IReadOnlyList<(Subject Subject, Section Section, int Periods, TeacherRole Role)> Assignments);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public AcademicYear? Year { get; set; }

        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();
    }

    public static class StaffLabels
    {
        public static string EmployeeStatus(Sms.Domain.Employees.EmployeeStatus s, bool ar) => s switch
        {
            Sms.Domain.Employees.EmployeeStatus.Active => ar ? "نشط" : "Active",
            Sms.Domain.Employees.EmployeeStatus.Suspended => ar ? "موقوف" : "Suspended",
            Sms.Domain.Employees.EmployeeStatus.Terminated => ar ? "منتهية خدمته" : "Terminated",
            _ => s.ToString(),
        };

        public static string EmployeeBadge(Sms.Domain.Employees.EmployeeStatus s) => s switch
        {
            Sms.Domain.Employees.EmployeeStatus.Active => "text-bg-success",
            Sms.Domain.Employees.EmployeeStatus.Suspended => "text-bg-warning",
            _ => "text-bg-secondary",
        };

        public static string ContractStatus(Sms.Domain.Employees.ContractStatus s, bool ar) => s switch
        {
            Sms.Domain.Employees.ContractStatus.Draft => ar ? "مسودة" : "Draft",
            Sms.Domain.Employees.ContractStatus.Active => ar ? "ساري" : "Active",
            Sms.Domain.Employees.ContractStatus.Terminated => ar ? "منتهٍ" : "Terminated",
            _ => s.ToString(),
        };

        public static string ContractType(Sms.Domain.Employees.ContractType t, bool ar) => t switch
        {
            Sms.Domain.Employees.ContractType.FullTime => ar ? "دوام كامل" : "Full-time",
            Sms.Domain.Employees.ContractType.PartTime => ar ? "دوام جزئي" : "Part-time",
            Sms.Domain.Employees.ContractType.Term => ar ? "محدد المدة" : "Term",
            _ => t.ToString(),
        };

        /// <summary>
        /// Rendered in the reader's language, and gendered in Arabic the way a form is — the
        /// record does not decide which half applies, so both are shown.
        /// </summary>
        public static string MaritalStatus(Sms.Domain.Employees.MaritalStatus m, bool ar) => m switch
        {
            Sms.Domain.Employees.MaritalStatus.Single => ar ? "أعزب / عزباء" : "Single",
            Sms.Domain.Employees.MaritalStatus.Married => ar ? "متزوج / متزوجة" : "Married",
            Sms.Domain.Employees.MaritalStatus.Divorced => ar ? "مطلق / مطلقة" : "Divorced",
            Sms.Domain.Employees.MaritalStatus.Widowed => ar ? "أرمل / أرملة" : "Widowed",
            _ => m.ToString(),
        };

        public static string TeacherRole(Sms.Domain.Teachers.TeacherRole r, bool ar) => r switch
        {
            Sms.Domain.Teachers.TeacherRole.Primary => ar ? "أساسي" : "Primary",
            Sms.Domain.Teachers.TeacherRole.CoTeacher => ar ? "مساعد" : "Co-teacher",
            _ => r.ToString(),
        };
    }
}
