using System;
using System.Collections.Generic;
using Sms.Web.Models;

namespace Sms.Web.Api.Models
{
    /// <summary>One row of the student directory (doc/Modules/10 §8).</summary>
    public sealed class ApiStudentRow
    {
        public int StudentId { get; set; }

        public string StudentNo { get; set; } = string.Empty;

        /// <summary>All four parts (BR-STU-001), joined — the picker prints three, the search reads all four.</summary>
        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        /// <summary>Enrolled / Suspended / Withdrawn / Graduated / Transferred / Alumni.</summary>
        public string Status { get; set; } = string.Empty;

        public string? GradeCode { get; set; }

        public string? GradeName { get; set; }

        public string? SectionName { get; set; }

        public string? Mobile { get; set; }
    }

    /// <summary>
    /// The student file's identity half. The social profile is deliberately not
    /// here: BR-GLB-072 makes it a restricted category with a screen permission
    /// of its own, and folding it into this response would hand it to every
    /// caller who may read a name.
    /// </summary>
    public sealed class ApiStudentFile
    {
        public int StudentId { get; set; }

        public string StudentNo { get; set; } = string.Empty;

        public string FirstNameAr { get; set; } = string.Empty;

        public string FatherNameAr { get; set; } = string.Empty;

        public string GrandfatherNameAr { get; set; } = string.Empty;

        public string FamilyNameAr { get; set; } = string.Empty;

        public string FirstNameEn { get; set; } = string.Empty;

        public string FatherNameEn { get; set; } = string.Empty;

        public string GrandfatherNameEn { get; set; } = string.Empty;

        public string FamilyNameEn { get; set; } = string.Empty;

        /// <summary>Male / Female.</summary>
        public string Gender { get; set; } = string.Empty;

        /// <summary>Gregorian, always. Hijri display is the client's own decision (ADR-4).</summary>
        public DateTime DateOfBirth { get; set; }

        public int NationalityLookupId { get; set; }

        public string? NationalityName { get; set; }

        public int? PrimaryIdTypeLookupId { get; set; }

        public string? PrimaryIdNo { get; set; }

        public DateTime? PrimaryIdExpiry { get; set; }

        public string Status { get; set; } = string.Empty;

        public string? Mobile { get; set; }

        public bool HasPhoto { get; set; }

        public ApiStudentPlacement? Placement { get; set; }

        public IReadOnlyList<ApiStudentGuardian> Guardians { get; set; } = Array.Empty<ApiStudentGuardian>();

        public IReadOnlyList<ApiEmergencyContact> EmergencyContacts { get; set; } = Array.Empty<ApiEmergencyContact>();
    }

    /// <summary>Where the student sits this year.</summary>
    public sealed class ApiStudentPlacement
    {
        public int EnrollmentId { get; set; }

        public int AcademicYearId { get; set; }

        public int? GradeLevelId { get; set; }

        public string? GradeCode { get; set; }

        public string? GradeName { get; set; }

        public int? SectionId { get; set; }

        public string? SectionName { get; set; }

        public DateTime EnrollmentDate { get; set; }
    }

    /// <summary>One live guardian link (BR-PAR-004).</summary>
    public sealed class ApiStudentGuardian
    {
        public int LinkId { get; set; }

        public int ParentId { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public string? Mobile { get; set; }

        public int RelationshipLookupId { get; set; }

        public string? Relationship { get; set; }

        public bool IsPrimaryContact { get; set; }

        /// <summary>BR-GLB-004: the last one of these cannot be unlinked.</summary>
        public bool IsFinanciallyResponsible { get; set; }

        public bool IsPickupAuthorized { get; set; }

        /// <summary>BR-SEC-011: whether this guardian's portal account may see the child.</summary>
        public bool IsPortalVisible { get; set; }

        public DateTime EffectiveFromUtc { get; set; }
    }

    /// <summary>Somebody to call who is not a guardian.</summary>
    public sealed class ApiEmergencyContact
    {
        public int Id { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;

        public bool IsPickupAuthorized { get; set; }

        public int? RelationshipLookupId { get; set; }
    }

    /// <summary>
    /// Register a student directly — the non-admissions path (transfers in,
    /// opening data). The permanent student number is issued by the numbering
    /// series on the same commit (BR-NUM-003) and is never supplied here.
    /// </summary>
    public class ApiRegisterStudentRequest
    {
        [RequiredField("Arabic first name", "الاسم الأول بالعربية")]
        public string FirstNameAr { get; set; } = string.Empty;

        [RequiredField("Arabic father's name", "اسم الأب بالعربية")]
        public string FatherNameAr { get; set; } = string.Empty;

        [RequiredField("Arabic grandfather's name", "اسم الجد بالعربية")]
        public string GrandfatherNameAr { get; set; } = string.Empty;

        [RequiredField("Arabic family name", "اسم العائلة بالعربية")]
        public string FamilyNameAr { get; set; } = string.Empty;

        [RequiredField("English first name", "الاسم الأول بالإنجليزية")]
        public string FirstNameEn { get; set; } = string.Empty;

        [RequiredField("English father's name", "اسم الأب بالإنجليزية")]
        public string FatherNameEn { get; set; } = string.Empty;

        [RequiredField("English grandfather's name", "اسم الجد بالإنجليزية")]
        public string GrandfatherNameEn { get; set; } = string.Empty;

        [RequiredField("English family name", "اسم العائلة بالإنجليزية")]
        public string FamilyNameEn { get; set; } = string.Empty;

        /// <summary>Male / Female.</summary>
        [RequiredField("gender", "الجنس")]
        public string Gender { get; set; } = string.Empty;

        public DateTime DateOfBirth { get; set; }

        public int NationalityLookupId { get; set; }

        public int? PrimaryIdTypeLookupId { get; set; }

        public string? PrimaryIdNo { get; set; }

        public DateTime? PrimaryIdExpiry { get; set; }
    }

    /// <summary>
    /// Correct identity fields. BR-STU-002 makes these T1-audited with a
    /// mandatory reason, so <see cref="Reason"/> is not optional in practice —
    /// omitting it is refused with <c>audit_reason_required</c>.
    /// </summary>
    public sealed class ApiUpdateStudentRequest : ApiRegisterStudentRequest
    {
        [RequiredField("reason", "السبب")]
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>BR-STU-002 / BR-WF-001: the allowed transitions are the engine's, not the caller's.</summary>
    public sealed class ApiChangeStudentStatusRequest
    {
        [RequiredField("status", "الحالة")]
        public string Status { get; set; } = string.Empty;

        /// <summary>Recorded against the change; the status column is audited.</summary>
        public string? Reason { get; set; }
    }

    /// <summary>Link a guardian to a student.</summary>
    public sealed class ApiLinkGuardianRequest
    {
        public int ParentId { get; set; }

        public int RelationshipLookupId { get; set; }

        public bool IsPrimaryContact { get; set; }

        public bool IsFinanciallyResponsible { get; set; }

        public bool IsPickupAuthorized { get; set; }

        /// <summary>BR-SEC-011: turns this family's portal view of the child on.</summary>
        public bool IsPortalVisible { get; set; } = true;

        /// <summary>Defaults to now when omitted.</summary>
        public DateTime? EffectiveFromUtc { get; set; }

        public int? GuardianshipDocAttachmentId { get; set; }
    }

    /// <summary>Add somebody to call. Not a guardian and carries no portal visibility.</summary>
    public sealed class ApiEmergencyContactRequest
    {
        [RequiredField("Arabic name", "الاسم بالعربية")]
        public string NameAr { get; set; } = string.Empty;

        [RequiredField("English name", "الاسم بالإنجليزية")]
        public string NameEn { get; set; } = string.Empty;

        [RequiredField("phone", "رقم الهاتف")]
        public string Phone { get; set; } = string.Empty;

        public bool IsPickupAuthorized { get; set; }

        public int? RelationshipLookupId { get; set; }
    }

    /// <summary>Put a student into a grade-year (BR-GLB-024 refuses a second live one).</summary>
    public sealed class ApiEnrollRequest
    {
        public int GradeYearProfileId { get; set; }

        public DateTime? EnrollmentDate { get; set; }

        /// <summary>Admission / Rollover / Reinstatement — the engine's own vocabulary. Defaults to Admission.</summary>
        public string? SourceType { get; set; }
    }

    /// <summary>Ending a guardian link. Omitting the date ends it now.</summary>
    public sealed class ApiUnlinkGuardianRequest
    {
        public DateTime? EffectiveToUtc { get; set; }
    }
}
