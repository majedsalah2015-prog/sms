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

        // Residence is the family's, so it is read and edited on the parent file rather than here.

        public IReadOnlyList<(int Id, string Ar, string En)> Relationships { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<(int Id, string Ar, string En)> Nationalities { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<(int Id, string Ar, string En)> IdTypes { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<(int ProfileId, string GradeAr, string GradeEn, string YearAr, string YearEn)> Profiles { get; set; } = Array.Empty<(int, string, string, string, string)>();

        public IReadOnlyDictionary<string, int> ReadThroughCounts { get; set; } = new Dictionary<string, int>();

        public string ActiveTab { get; set; } = "personal";

        /// <summary>
        /// BR-GLB-072: the social profile is a restricted category with its own permission
        /// (<c>STU/SocialProfile</c>), and it renders inside this screen rather than on one of its
        /// own. Gating only the actions would leave the data on the page for anyone who may open the
        /// file at all, which is the whole thing the separate permission exists to prevent.
        /// </summary>
        public bool CanSeeSocialProfile { get; set; }

        /// <summary>False leaves the section readable and its form absent — a reader is not an editor.</summary>
        public bool CanEditSocialProfile { get; set; }
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

        /// <summary>References the "IdType" lookup category, same catalogue the student and employee registers use.</summary>
        public int? PrimaryIdTypeLookupId { get; set; }

        /// <summary>BR-PAR-002's strongest deduplication signal, which the parent register had no field for.</summary>
        public string? PrimaryIdNo { get; set; }

        /// <summary>حالة ولي الأمر — defaults to Alive so the common case costs no clicks.</summary>
        public ParentLifeStatus LifeStatus { get; set; } = ParentLifeStatus.Alive;

        /// <summary>Only meaningful for <see cref="ParentLifeStatus.Other"/>; the admin drops it otherwise.</summary>
        public string? LifeStatusNote { get; set; }

        public IReadOnlyList<(int Id, string Ar, string En)> IdTypes { get; set; } = Array.Empty<(int, string, string)>();

        public string? Email { get; set; }

        public string? Address { get; set; }

        public string? OccupationEmployer { get; set; }

        public string PreferredLanguage { get; set; } = "ar";

        /// <summary>منطقة — recorded on the family, not once per child.</summary>
        public int? ResidenceAreaId { get; set; }

        /// <summary>حي — only where the locality has quarters recorded.</summary>
        public int? NeighbourhoodId { get; set; }

        /// <summary>Top level of the residence picker; the lower two are fetched as it changes.</summary>
        public IReadOnlyList<Governorate> Governorates { get; set; } = Array.Empty<Governorate>();

        public string? Reason { get; set; }
    }

    public sealed class ParentFileViewModel
    {
        public sealed record ChildRow(StudentGuardianLink Link, Student Student, string Relationship, string? GradeName);

        public Parent Parent { get; set; } = null!;

        public IReadOnlyList<ChildRow> Children { get; set; } = Array.Empty<ChildRow>();

        public IReadOnlyList<ChildRow> PastChildren { get; set; } = Array.Empty<ChildRow>();

        /// <summary>The "IdType" lookup, for the identity tab's picker.</summary>
        public IReadOnlyList<(int Id, string Ar, string En)> IdTypes { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<Parent> PossibleDuplicates { get; set; } = Array.Empty<Parent>();

        public IReadOnlyList<(string Action, string? Field, string? Old, string? New, DateTime At, int Actor, string? Reason)> Audit { get; set; } = Array.Empty<(string, string?, string?, string?, DateTime, int, string?)>();

        public string? PortalUserName { get; set; }

        public string ActiveTab { get; set; } = "identity";

        public IReadOnlyList<FamilyStatementLine> FamilyStatement { get; set; } = Array.Empty<FamilyStatementLine>();

        /// <summary>Governorate · locality · quarter, joined for reading; null when nothing is recorded.</summary>
        public string? ResidencePath { get; set; }
    }

    public sealed class DedupWorkbenchViewModel
    {
        public sealed record Pair(Parent A, Parent B, string Reason, int ChildrenA, int ChildrenB);

        public IReadOnlyList<Pair> Pairs { get; set; } = Array.Empty<Pair>();
    }

    /// <summary>
    /// Backing model for <c>Views/Shared/_ParentPicker.cshtml</c>: the one parent dropdown used
    /// everywhere a parent is chosen — a filter box over the list, and a button through to that
    /// parent's residence.
    /// <para>
    /// A partial rather than four near-identical selects, because the list is the same list on all
    /// four screens and the day it grows past a screenful it has to stop being scrollable on all four
    /// at once.
    /// </para>
    /// </summary>
    public sealed class ParentPickerViewModel
    {
        /// <summary>Posted field name — it differs by screen (<c>ParentId</c>, <c>parentId</c>).</summary>
        public string Name { get; set; } = "parentId";

        /// <summary>DOM id of the select; must be unique per page.</summary>
        public string Id { get; set; } = "parent-picker";

        public string? Label { get; set; }

        public int? SelectedId { get; set; }

        public IReadOnlyList<Parent> Parents { get; set; } = Array.Empty<Parent>();

        /// <summary>Caption of the leading empty option. Null renders no empty option at all.</summary>
        public string? EmptyLabel { get; set; } = "—";

        /// <summary>Where the residence editor should come back to. Null falls back to the parent file.</summary>
        public string? ReturnUrl { get; set; }

        /// <summary>Hides the residence button — for screens where the picker is only being read.</summary>
        public bool ShowResidenceButton { get; set; } = true;
    }

    /// <summary>The residence editor reached from the picker's button (governorate → locality → quarter).</summary>
    public sealed class ParentResidenceViewModel
    {
        public Parent Parent { get; set; } = null!;

        public IReadOnlyList<Governorate> Governorates { get; set; } = Array.Empty<Governorate>();

        /// <summary>Walked up from the stored locality, never stored beside it.</summary>
        public int? CurrentGovernorateId { get; set; }

        public int? CurrentAreaId { get; set; }

        public int? CurrentNeighbourhoodId { get; set; }

        /// <summary>The three levels joined for reading, or null when nothing is recorded.</summary>
        public string? CurrentPath { get; set; }

        public string? ReturnUrl { get; set; }

        public bool CanEdit { get; set; }
    }
}

namespace Sms.Web.Models
{
    /// <summary>doc/Modules/11 §8.2 "Family statement": consolidated finance read-through per child (posted charges only, BR-SEC-012-style scoping; discounts shown separately per BR-DIS-010).</summary>
    public sealed record FamilyStatementLine(Sms.Domain.Students.Student Student, decimal Gross, decimal CreditNotes, decimal Discounts, decimal Paid, decimal Position, int ChargeCount);
}

namespace Sms.Web.Models
{
    /// <summary>
    /// Backing model for <c>Views/Shared/_PhotoPanel.cshtml</c>: the one photograph a person's file
    /// carries, with its upload and remove actions. Students and staff use the same panel because
    /// the thing on screen is the same thing — a face, a frame, and two buttons.
    /// </summary>
    public sealed class PhotoPanelViewModel
    {
        /// <summary>DOM id prefix; must be unique per page.</summary>
        public string Id { get; set; } = "photo";

        /// <summary>Named in the image's alt text, so a screen reader says whose face it is.</summary>
        public string PersonName { get; set; } = string.Empty;

        public bool HasPhoto { get; set; }

        public string? PhotoUrl { get; set; }

        public string? UploadUrl { get; set; }

        public string? RemoveUrl { get; set; }

        /// <summary>False renders the frame alone — the portal shows a photo, it does not set one.</summary>
        public bool CanEdit { get; set; }
    }
}
