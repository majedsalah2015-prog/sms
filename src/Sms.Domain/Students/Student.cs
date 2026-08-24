using System;
using Sms.Domain.Audit;
using Sms.Domain.Parents;
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

        // ---------------------------------------------------------------- mother's particulars
        //
        // On the student, not on Parent, by owner decision (2026-08-21). The school records these for
        // every student whether or not the mother holds a guardian account, and many do not — putting
        // them on Parent would leave the commonest case with nowhere to write them.
        //
        // The cost is real and worth stating: siblings each carry their own copy, so a change to the
        // mother's mobile has to be made on each. If that becomes the daily complaint, the answer is to
        // promote her to a Parent row and point the siblings at it — not to sync copies.

        [RequiresAuditReason]
        public string? MotherName { get; set; }

        [RequiresAuditReason]
        public string? MotherNationalId { get; set; }

        public string? MotherOccupation { get; set; }

        /// <summary>References core.LookupValue, category "EducationLevel".</summary>
        public int? MotherEducationLookupId { get; set; }

        public string? MotherMobile { get; set; }

        // ---------------------------------------------------------------- social profile
        //
        // Every field below feeds a decision the school has to defend — a fee discount, a ministry
        // return, a religious-education stream — so all of them are T1-audited with a reason, like the
        // identity fields above and for the same purpose: someone will be asked why this changed.

        [RequiresAuditReason]
        public ParentLifeStatus? FatherStatus { get; set; }

        [RequiresAuditReason]
        public ParentLifeStatus? MotherStatus { get; set; }

        [RequiresAuditReason]
        public Religion? Religion { get; set; }

        [RequiresAuditReason]
        public ResidencyStatus? ResidencyStatus { get; set; }

        [RequiresAuditReason]
        public FinancialStatus? FinancialStatus { get; set; }

        /// <summary>رقم بطاقة التموين — a ration entitlement, so it is corroborating evidence for the means assessment above and audited with it.</summary>
        [RequiresAuditReason]
        public string? RationCardNo { get; set; }

        public string? PlaceOfBirth { get; set; }

        /// <summary>عدد أفراد الأسرة — household size, the denominator behind sibling and hardship rules (Module 22).</summary>
        public int? FamilySize { get; set; }

        /// <summary>ترتيب الطالب بين الأبناء, 1 = eldest.</summary>
        public int? BirthOrder { get; set; }

        // Residence (governorate → locality → quarter) lives on Parent, not here. Where a family
        // lives is the family's fact: a copy per child let three siblings disagree about one
        // address, and nothing in the product could say which of them was right.
    }
}
