using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Employees
{
    /// <summary>
    /// ppl.Employee (doc/Modules/12 §7, BR-EMP-001): one permanent record +
    /// Employee No. (doc 08) across contract renewals/rehires. Mirrors
    /// Student's quad-name shape (E-202) — the established person-entity
    /// pattern in this codebase. Identity T1-audited per BR-EMP-001.
    /// UserAccountId links to the employee's login (BR-EMP-001: "employee
    /// != user account, but offboarding auto-deactivates the account") —
    /// nullable since account provisioning (Module 36) isn't wired here.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class Employee : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        /// <summary>doc 08 EMP series.</summary>
        public string EmployeeNo { get; set; } = string.Empty;

        public int? UserAccountId { get; set; }

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

        public Gender Gender { get; set; }

        public DateTime DateOfBirth { get; set; }

        public int NationalityLookupId { get; set; }

        /// <summary>ID/Iqama per BR-EMP-009/doc §9 — mandatory in the real product, not enforced here (content/config concern).</summary>
        public int? PrimaryIdTypeLookupId { get; set; }

        public string? PrimaryIdNo { get; set; }

        public DateTime? PrimaryIdExpiry { get; set; }

        public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    }
}
