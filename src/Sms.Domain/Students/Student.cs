using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Students
{
    /// <summary>
    /// ppl.Student (DB doc A2 pivotal spec; doc/Modules/10, BR-STU-001): one
    /// permanent record + number (BR-GLB-002, BR-NUM-004) across years,
    /// withdrawal, and re-admission. Identity fields are T1-audited with
    /// reason — they're what official documents display, same reasoning as
    /// School's identity fields (E-102).
    /// </summary>
    [Audited(AuditTier.T1)]
    public class Student : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        /// <summary>doc 08 STU series — permanent, never re-issued (BR-NUM-004).</summary>
        public string StudentNo { get; set; } = string.Empty;

        /// <summary>BR-SEC-010's portal identity bridge — nullable since student self-service login (older grades) isn't provisioned by any admin service yet (Module 36). Mirrors Employee.UserAccountId's E-203 precedent.</summary>
        public int? UserAccountId { get; set; }

        // Full quad name both languages (Gulf convention): given, father's, grandfather's, family.
        [RequiresAuditReason]
        public string FirstNameAr { get; set; } = string.Empty;

        [RequiresAuditReason]
        public string FatherNameAr { get; set; } = string.Empty;

        [RequiresAuditReason]
        public string GrandfatherNameAr { get; set; } = string.Empty;

        [RequiresAuditReason]
        public string FamilyNameAr { get; set; } = string.Empty;

        [RequiresAuditReason]
        public string FirstNameEn { get; set; } = string.Empty;

        [RequiresAuditReason]
        public string FatherNameEn { get; set; } = string.Empty;

        [RequiresAuditReason]
        public string GrandfatherNameEn { get; set; } = string.Empty;

        [RequiresAuditReason]
        public string FamilyNameEn { get; set; } = string.Empty;

        [RequiresAuditReason]
        public Gender Gender { get; set; }

        [RequiresAuditReason]
        public DateTime DateOfBirth { get; set; }

        /// <summary>References core.LookupValue, category "Nationality" (seeded by E-010).</summary>
        public int NationalityLookupId { get; set; }

        /// <summary>References core.LookupValue, category "IdType" (seeded by E-010).</summary>
        public int? PrimaryIdTypeLookupId { get; set; }

        public string? PrimaryIdNo { get; set; }

        public DateTime? PrimaryIdExpiry { get; set; }

        /// <summary>References doc.Attachment (E-008); consent-governed (BR-STU-008).</summary>
        public int? PhotoAttachmentId { get; set; }

        [RequiresAuditReason]
        public StudentStatus Status { get; set; } = StudentStatus.Enrolled;

        public bool IsActive { get; set; } = true;
    }
}
