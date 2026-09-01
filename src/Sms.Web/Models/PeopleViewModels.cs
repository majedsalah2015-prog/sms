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
        /// <summary>
        /// Nationality was a column here until 2026-08-26 (owner request). It stays on the record
        /// and on the file — certificates and ministry returns read it — but a directory row is
        /// read to find and reach a child, and a nationality answers neither question. The
        /// student's own mobile took the width instead.
        /// </summary>
        public sealed record Row(Student Student, string? GradeName, string? SectionName, string? PrimaryParent);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        /// <summary>
        /// Whether to offer the placement screen from a row (BR-SEC-010 — the button disappears
        /// rather than refusing). It opens on the student file's own View right, and what can be
        /// done there is gated again by the two rights that screen's forms carry.
        /// </summary>
        public bool CanPlace { get; set; }

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

        // ---- social profile ----
        // Nullable throughout: this is a section a registrar fills over time, often from a document
        // that arrives after the student does. A required field here would block registration on
        // paperwork the school does not have yet. The mother's own particulars left this form on
        // 2026-08-24 (owner request), and the father's and mother's life status with them — all of it
        // is guardian data now, edited on each parent's own file as Parent.LifeStatus.

        public Religion? Religion { get; set; }

        public ResidencyStatus? ResidencyStatus { get; set; }

        public FinancialStatus? FinancialStatus { get; set; }

        public string? RationCardNo { get; set; }

        public string? PlaceOfBirth { get; set; }

        public int? FamilySize { get; set; }

        public int? BirthOrder { get; set; }

        /// <summary>عدد الأخوة — the child's brothers and sisters, not the household size above.</summary>
        public int? SiblingCount { get; set; }

        /// <summary>The student's own line; the family's numbers live on each guardian's file.</summary>
        public string? Mobile { get; set; }

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

        // ---- guardians tab ----
        //
        // Read-only here: a guardian's qualification is named on the student file so the registrar can
        // see it beside the child, and edited on the parent file, which is the one place it lives.

        public IReadOnlyList<(int Id, string Ar, string En)> EducationLevels { get; set; } = Array.Empty<(int, string, string)>();

        // ---- residence (personal tab) ----
        //
        // Only the top level is carried into the page. The two beneath it are fetched as the one
        // above them changes, because 34 localities and their quarters written into every student
        // file would be a page four times its size, almost none of it looked at.

        public IReadOnlyList<Governorate> Governorates { get; set; } = Array.Empty<Governorate>();

        /// <summary>Walked up from the stored locality, so the picker opens where the record points.</summary>
        public int? CurrentGovernorateId { get; set; }

        public int? CurrentResidenceAreaId { get; set; }

        /// <summary>محافظة · منطقة · حي, in the reading culture — null when nothing is recorded.</summary>
        public string? CurrentResidencePath { get; set; }

        /// <summary>
        /// Whether this reader may open the screen the three drop-downs are filled from
        /// (System Setup → Residence areas), which is what decides whether the link beside them is
        /// drawn at all. BR-SEC-010: unauthorized surface disappears rather than refusing on click.
        /// <para>
        /// The link exists because the address a registrar is typing is regularly the first mention
        /// of a quarter the seeded pack never listed, and a picker with no way to add to it is where
        /// "أخرى" comes from.
        /// </para>
        /// </summary>
        public bool CanManageResidenceLists { get; set; }

        public IReadOnlyList<(int Id, string Ar, string En)> Relationships { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<(int Id, string Ar, string En)> Nationalities { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<(int Id, string Ar, string En)> IdTypes { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<(int ProfileId, string GradeAr, string GradeEn, string YearAr, string YearEn)> Profiles { get; set; } = Array.Empty<(int, string, string, string, string)>();

        public IReadOnlyDictionary<string, int> ReadThroughCounts { get; set; } = new Dictionary<string, int>();

        /// <summary>doc 10 §5: the entity documents tab, drawn by the same partial the employee file uses.</summary>
        public EntityDocumentsViewModel Documents { get; set; } = new();

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

    /// <summary>
    /// One child's placement — the grade-year they are enrolled in and the section they sit in
    /// (doc/Modules/10 §8, doc/Modules/06 §8.2, BR-SCN-005/006, BR-GLB-024).
    /// <para>
    /// Both halves already existed and neither could be reached from the student. Enrolling was a
    /// form at the bottom of the file's academic tab; seating was on the section's own page, which
    /// asks "who is in this section" and answers it with a picker of every unseated child in the
    /// grade. Moving one student meant knowing which section they were in before you could find
    /// them — the one fact the person asking usually does not have. This screen asks the question
    /// from the child's end instead, and writes through the same two services.
    /// </para>
    /// </summary>
    public sealed class StudentPlacementViewModel
    {
        /// <summary>A section of the student's own grade-year, with what is left of its seats.</summary>
        public sealed record SectionOption(Section Section, int Members, bool IsCurrent);

        public Student Student { get; set; } = null!;

        /// <summary>The open enrollment (no exit date). Null = the student is registered but sits in no grade yet.</summary>
        public Enrollment? Enrollment { get; set; }

        public GradeLevel? Grade { get; set; }

        public AcademicYear? Year { get; set; }

        /// <summary>The open membership; null when the student is enrolled in a grade but not yet seated.</summary>
        public SectionMembership? Membership { get; set; }

        public Section? Section { get; set; }

        /// <summary>Sections of <see cref="Enrollment"/>'s own grade-year only — a section of another grade is not a placement, it is a mistake.</summary>
        public IReadOnlyList<SectionOption> Sections { get; set; } = Array.Empty<SectionOption>();

        /// <summary>Grade-years available to enroll into, newest year first.</summary>
        public IReadOnlyList<(int ProfileId, string GradeAr, string GradeEn, string YearAr, string YearEn)> Profiles { get; set; } = Array.Empty<(int, string, string, string, string)>();

        /// <summary>STU/Enrollment/Create — putting the child in a grade.</summary>
        public bool CanEnroll { get; set; }

        /// <summary>SEC/Roster/Edit — seating them in a section, and moving them between sections.</summary>
        public bool CanSeat { get; set; }
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

        /// <summary>المؤهل العلمي — the "EducationLevel" lookup category, which the student register used to draw on for the mother.</summary>
        public int? EducationLookupId { get; set; }

        public IReadOnlyList<(int Id, string Ar, string En)> EducationLevels { get; set; } = Array.Empty<(int, string, string)>();

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

        /// <summary>The "EducationLevel" lookup, beside it.</summary>
        public IReadOnlyList<(int Id, string Ar, string En)> EducationLevels { get; set; } = Array.Empty<(int, string, string)>();

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
