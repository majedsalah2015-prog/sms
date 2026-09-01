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

        // ---------------------------------------------------------------- the parents
        //
        // Nothing about either parent is carried here any more. The mother's name, ID number, mobile,
        // occupation and qualification were five columns from 2026-08-21 until 2026-08-24, and the
        // father's and mother's life status two more until the owner ended those too the same day:
        // all of it is now the ordinary fields of a Parent row linked by StudentGuardianLink with
        // relationship "Father" or "Mother" — Parent.LifeStatus is the one that replaced the pair.
        //
        // The copy-per-child cost the original decision accepted is what ended it: siblings each held
        // their own copy, so a corrected mobile — or a father recorded as a martyr on one child and
        // alive on the next — had to be corrected once per child, and nothing in the product could
        // say which copy was right. One row, many links, does not have that problem.
        //
        // The case this gives up is a parent with no file at all. The school can still record the
        // status by opening one for them, which is where the rest of that parent's data has to go
        // anyway, so a status with nobody to attach it to is not a case worth two columns.

        // ---------------------------------------------------------------- social profile
        //
        // Every field below feeds a decision the school has to defend — a fee discount, a ministry
        // return, a religious-education stream — so all of them are T1-audited with a reason, like the
        // identity fields above and for the same purpose: someone will be asked why this changed.

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

        /// <summary>
        /// عدد الأخوة — how many siblings the child has, which is not <see cref="FamilySize"/>
        /// minus two. A household size counts whoever lives there; this counts brothers and
        /// sisters, and an only child answers 0 where a household never can. Module 22 reads the
        /// household figure for hardship, so folding the two together would move a fee discount.
        /// </summary>
        public int? SiblingCount { get; set; }

        /// <summary>
        /// The number the school rings to reach this child. Contact for the family lives on
        /// <c>Parent</c> and stays there — this is the student's own line, which older students
        /// have and which an old register keeps beside the child rather than beside the guardian.
        /// </summary>
        public string? Mobile { get; set; }

        // ---------------------------------------------------------------- residence
        //
        // Where this student lives, on the student's own record (owner request, 2026-08-31). It was
        // here until 2026-08-22, moved to Parent on the argument that an address is the family's
        // fact, and asked for back because the registrar reads it on the child's file and could not
        // find it there: a screen that makes you open a second person's file to learn where a pupil
        // lives is a screen that does not get filled in — 0 of 987 parent files carried a residence
        // on the day it was asked for.
        //
        // The cost of that move is real and is being accepted knowingly: two siblings can now be
        // recorded at two addresses, and nothing here says which is right. The parent keeps its own
        // residence (BR-PAR-001's "address") — these are independent, not a cache of it, so neither
        // silently overwrites the other.
        //
        // Two levels stored rather than the single neighbourhood this held before 2026-08-22, for
        // the reason Parent already gives: most localities have no quarters recorded at all (7
        // across 34 today), so a record that could only name a quarter would leave nearly every
        // family with no residence it was able to express.
        //
        // Not [RequiresAuditReason]: an address is a fact being recorded, not a decision being
        // defended, and it changes whenever a family moves. The change is still captured
        // field-level, because the class is T1.

        /// <summary>منطقة — the locality, referencing <c>core.ResidenceArea</c>. The governorate is walked up from it, never stored, so the two cannot disagree.</summary>
        public int? ResidenceAreaId { get; set; }

        /// <summary>حي — the quarter inside <see cref="ResidenceAreaId"/>, where that locality has any.</summary>
        public int? NeighbourhoodId { get; set; }
    }
}
