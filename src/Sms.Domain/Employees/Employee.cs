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

        /// <summary>
        /// The staff photograph, held as an attachment like every other file the product stores:
        /// the row keeps only the pointer, so the image goes through the same scan gate and the same
        /// storage abstraction as a contract or a certificate (doc 10). Mirrors Student.PhotoAttachmentId.
        /// </summary>
        public int? PhotoAttachmentId { get; set; }

        public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

        /// <summary>
        /// الحالة الاجتماعية. Optional, and no rule in Module 12 reads it — see
        /// <see cref="MaritalStatus"/> for why it is here at all.
        /// </summary>
        [RequiresAuditReason]
        public MaritalStatus? MaritalStatus { get; set; }

        /// <summary>
        /// اسم البنك — where this employee's salary is paid.
        /// <para>
        /// A deliberate extension beyond doc/Modules/12 §7, made at the owner's request
        /// (2026-08-23) and worth stating plainly: BR-EMP-007 holds that this system never
        /// computes a net salary and hands payroll to whoever does, as an export. Disbursement
        /// details therefore had no home here. They have one now because the school's own staff
        /// register carries them and the payroll export is the thing that will need them — but
        /// nothing in this product pays anybody, and adding these two columns does not change
        /// that.
        /// </para>
        /// <para>
        /// Audited with a mandatory reason, like <see cref="Contract.SalaryBasic"/>: a silent
        /// change of the account that receives someone's pay is the one edit on this record that
        /// nobody should be able to make without saying why.
        /// </para>
        /// </summary>
        [RequiresAuditReason]
        public string? BankName { get; set; }

        /// <summary>
        /// رقم الحساب البنكي / IBAN. Stored as written — the format differs by country and the
        /// country pack does not describe one, so validating it here would reject valid accounts
        /// in the next deployment. See <see cref="BankName"/> for why the pair exists.
        /// </summary>
        [RequiresAuditReason]
        public string? BankAccountNo { get; set; }
    }
}
