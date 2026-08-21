using System;
using System.Collections.Generic;
using Sms.Domain.Common;
using Sms.Domain.Grades;
using Sms.Domain.Parents;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Geography;
using Sms.Domain.Students;

namespace Sms.Web.Models
{
    // ---------------------------------------------------------------- Students (doc/Modules/10 §8)

    public sealed class StudentListViewModel
    {
        public sealed record Row(Student Student, string? GradeName, string? SectionName, string? PrimaryParent, string NationalityName);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public string? Query { get; set; }

        public StudentStatus? Status { get; set; }

        public int? GradeId { get; set; }

        public IReadOnlyList<GradeLevel> Grades { get; set; } = Array.Empty<GradeLevel>();

        public int Total { get; set; }
    }

    public sealed class StudentFormViewModel
    {
        public int? Id { get; set; }

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

        public string? Reason { get; set; }

        // ---- mother's particulars + social profile (owner request, 2026-08-21) ----
        // Nullable throughout: this is a section a registrar fills over time, often from a document
        // that arrives after the student does. A required field here would block registration on
        // paperwork the school does not have yet.

        public string? MotherName { get; set; }

        public string? MotherNationalId { get; set; }

        public string? MotherOccupation { get; set; }

        public int? MotherEducationLookupId { get; set; }

        public string? MotherMobile { get; set; }

        public ParentLifeStatus? FatherStatus { get; set; }

        public ParentLifeStatus? MotherStatus { get; set; }

        public Religion? Religion { get; set; }

        public ResidencyStatus? ResidencyStatus { get; set; }

        public FinancialStatus? FinancialStatus { get; set; }

        public string? RationCardNo { get; set; }

        public string? PlaceOfBirth { get; set; }

        public int? FamilySize { get; set; }

        public int? BirthOrder { get; set; }

        /// <summary>
        /// The two levels above the neighbourhood are posted so the cascading
        /// picker can be re-rendered as the registrar left it when the form comes
        /// back with an error. Only <see cref="NeighbourhoodId"/> is stored.
        /// </summary>
        public int? GovernorateId { get; set; }

        public int? ResidenceAreaId { get; set; }

        public int? NeighbourhoodId { get; set; }

        // register-time extras
        public int? GradeYearProfileId { get; set; }

        public int? ParentId { get; set; }

        public int? RelationshipLookupId { get; set; }

        public IReadOnlyList<(int Id, string Ar, string En)> Nationalities { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<(int Id, string Ar, string En)> IdTypes { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<(int Id, string Ar, string En)> Relationships { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<(int ProfileId, string GradeAr, string GradeEn, string YearAr, string YearEn)> Profiles { get; set; } = Array.Empty<(int, string, string, string, string)>();

        public IReadOnlyList<Parent> Parents { get; set; } = Array.Empty<Parent>();
    }

    public sealed class StudentFileViewModel
    {
        public sealed record GuardianRow(StudentGuardianLink Link, Parent Parent, string Relationship);

        public sealed record EnrollmentRow(Enrollment Enrollment, AcademicYear Year, GradeLevel Grade, Section? Section);

        public Student Student { get; set; } = null!;

        public string NationalityName { get; set; } = string.Empty;

        public string? IdTypeName { get; set; }

        public IReadOnlyList<GuardianRow> Guardians { get; set; } = Array.Empty<GuardianRow>();

        public IReadOnlyList<GuardianRow> PastGuardians { get; set; } = Array.Empty<GuardianRow>();

        public IReadOnlyList<EmergencyContact> EmergencyContacts { get; set; } = Array.Empty<EmergencyContact>();

        public IReadOnlyList<EnrollmentRow> Enrollments { get; set; } = Array.Empty<EnrollmentRow>();

        public IReadOnlyList<StudentStatus> AllowedTransitions { get; set; } = Array.Empty<StudentStatus>();

        public IReadOnlyList<(string Action, string? Field, string? Old, string? New, DateTime At, int Actor, string? Reason)> Audit { get; set; } = Array.Empty<(string, string?, string?, string?, DateTime, int, string?)>();

        public IReadOnlyList<Parent> Parents { get; set; } = Array.Empty<Parent>();

        // ---- social profile tab ----

        public IReadOnlyList<(int Id, string Ar, string En)> EducationLevels { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<Governorate> Governorates { get; set; } = Array.Empty<Governorate>();

        /// <summary>Pre-selects the picker's top level when a neighbourhood is already recorded — found by walking up from it, never stored beside it.</summary>
        public int? CurrentGovernorateId { get; set; }

        /// <summary>The middle level, resolved the same way — so the picker reopens on all three without the browser guessing which locality owns the neighbourhood.</summary>
        public int? CurrentResidenceAreaId { get; set; }

        /// <summary>"غزة ← مدينة غزة ← حي الرمال" — the whole address on one line, so the reader is not left assembling it from three dropdowns.</summary>
        public string? CurrentResidencePath { get; set; }

        public IReadOnlyList<(int Id, string Ar, string En)> Relationships { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<(int Id, string Ar, string En)> Nationalities { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<(int Id, string Ar, string En)> IdTypes { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<(int ProfileId, string GradeAr, string GradeEn, string YearAr, string YearEn)> Profiles { get; set; } = Array.Empty<(int, string, string, string, string)>();

        public IReadOnlyDictionary<string, int> ReadThroughCounts { get; set; } = new Dictionary<string, int>();

        public string ActiveTab { get; set; } = "personal";
    }

    // ---------------------------------------------------------------- Parents (doc/Modules/11 §8)

    public sealed class ParentDirectoryViewModel
    {
        public sealed record Row(Parent Parent, int Children, bool HasPortalAccount, IReadOnlyList<string> Flags);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public string? Query { get; set; }

        public string? Filter { get; set; }

        public int Total { get; set; }
    }

    public sealed class ParentFormViewModel
    {
        public int? Id { get; set; }

        public string? NameAr { get; set; }

        public string? NameEn { get; set; }

        public string? PrimaryMobile { get; set; }

        public string? Email { get; set; }

        public string? Address { get; set; }

        public string? OccupationEmployer { get; set; }

        public string PreferredLanguage { get; set; } = "ar";

        public string? Reason { get; set; }
    }

    public sealed class ParentFileViewModel
    {
        public sealed record ChildRow(StudentGuardianLink Link, Student Student, string Relationship, string? GradeName);

        public Parent Parent { get; set; } = null!;

        public IReadOnlyList<ChildRow> Children { get; set; } = Array.Empty<ChildRow>();

        public IReadOnlyList<ChildRow> PastChildren { get; set; } = Array.Empty<ChildRow>();

        public IReadOnlyList<Parent> PossibleDuplicates { get; set; } = Array.Empty<Parent>();

        public IReadOnlyList<(string Action, string? Field, string? Old, string? New, DateTime At, int Actor, string? Reason)> Audit { get; set; } = Array.Empty<(string, string?, string?, string?, DateTime, int, string?)>();

        public string? PortalUserName { get; set; }

        public string ActiveTab { get; set; } = "identity";

        public IReadOnlyList<FamilyStatementLine> FamilyStatement { get; set; } = Array.Empty<FamilyStatementLine>();
    }

    public sealed class DedupWorkbenchViewModel
    {
        public sealed record Pair(Parent A, Parent B, string Reason, int ChildrenA, int ChildrenB);

        public IReadOnlyList<Pair> Pairs { get; set; } = Array.Empty<Pair>();
    }
}

namespace Sms.Web.Models
{
    /// <summary>doc/Modules/11 §8.2 "Family statement": consolidated finance read-through per child (posted charges only, BR-SEC-012-style scoping; discounts shown separately per BR-DIS-010).</summary>
    public sealed record FamilyStatementLine(Sms.Domain.Students.Student Student, decimal Gross, decimal CreditNotes, decimal Discounts, decimal Paid, decimal Position, int ChargeCount);
}
