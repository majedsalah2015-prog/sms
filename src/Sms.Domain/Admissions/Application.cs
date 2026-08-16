using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Admissions
{
    /// <summary>
    /// ppl.Application (doc/Modules/09 §7, BR-ADM-002/011): applicant person
    /// data pre-student. Mirrors Student's quad-name shape (E-202) since
    /// registration copies these fields onto the new Student record.
    /// Application number (doc 08 APP series) ≠ student number (doc 08 §9
    /// Q3 decision) — they are deliberately different identities.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class Application : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int CampaignId { get; set; }

        /// <summary>doc 08 APP series.</summary>
        public string ApplicationNo { get; set; } = string.Empty;

        public string FirstNameAr { get; set; } = string.Empty;

        public string FatherNameAr { get; set; } = string.Empty;

        public string GrandfatherNameAr { get; set; } = string.Empty;

        public string FamilyNameAr { get; set; } = string.Empty;

        public string FirstNameEn { get; set; } = string.Empty;

        public string FatherNameEn { get; set; } = string.Empty;

        public string GrandfatherNameEn { get; set; } = string.Empty;

        public string FamilyNameEn { get; set; } = string.Empty;

        public Gender Gender { get; set; }

        public DateTime DateOfBirth { get; set; }

        public int NationalityLookupId { get; set; }

        /// <summary>BR-ADM-003: null until parent dedup resolves (matched or newly created) — the dedup engine itself is deferred (same as E-202's BR-PAR-002/003).</summary>
        public int? ParentId { get; set; }

        public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;

        /// <summary>Registration deadline once Approved (BR-ADM-007); null before approval.</summary>
        public DateTime? RegistrationDeadlineUtc { get; set; }

        /// <summary>Set once BR-ADM-007 registration completes.</summary>
        public int? RegisteredStudentId { get; set; }
    }
}
