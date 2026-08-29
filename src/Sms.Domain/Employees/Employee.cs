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

        /// <summary>
        /// رقم الجوال — the number the school rings to reach this employee.
        /// <para>
        /// doc/Modules/12 §8.1 calls the directory "the basic contact card for all staff", and a
        /// contact card with no way to make contact is not one. Mirrors
        /// <c>Student.Mobile</c> exactly, down to the column length, because the same registrar
        /// types the same shape of number into both.
        /// </para>
        /// <para>
        /// Field-audited like the rest of the record (T1) but deliberately not
        /// <c>[RequiresAuditReason]</c>: a phone number changes when a person changes phones, and
        /// demanding a written justification for that would teach everyone here to type a full
        /// stop into the reason box — which is how a mandatory reason stops meaning anything on
        /// the fields that need one (the name, the ID, the bank account).
        /// </para>
        /// </summary>
        public string? Mobile { get; set; }

        /// <summary>
        /// رقم الواتس اب — owner request, 2026-08-27.
        /// <para>
        /// A column of its own rather than an assumption that <see cref="Mobile"/> reaches the same
        /// application: plenty of staff carry a second line for it, and a school that messages its
        /// teachers on the wrong number learns so one absence at a time. Same length and same audit
        /// treatment as the mobile beside it — a number that changes with the handset.
        /// </para>
        /// </summary>
        public string? WhatsAppNumber { get; set; }

        public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

        /// <summary>
        /// الحالة الاجتماعية. Optional, and no rule in Module 12 reads it — see
        /// <see cref="MaritalStatus"/> for why it is here at all.
        /// </summary>
        [RequiresAuditReason]
        public MaritalStatus? MaritalStatus { get; set; }

        /// <summary>
        /// نوع هوية الزوج/الزوجة — core.LookupValue, category "IdType", the same catalogue the
        /// employee's own document is chosen from (owner request, 2026-08-27).
        /// <para>
        /// A spouse is not a person this system keeps a record of, and this pair is not the start of
        /// one: it is two cells off the school's staff register, recorded because allowances and
        /// ministry returns are filed against them. Nothing here reads the number, and no rule
        /// requires it — including when <see cref="MaritalStatus"/> says married, because a register
        /// being typed up months after the fact frequently has the status and not the document.
        /// </para>
        /// </summary>
        public int? SpouseIdTypeLookupId { get; set; }

        /// <summary>رقم هوية الزوج/الزوجة. See <see cref="SpouseIdTypeLookupId"/>.</summary>
        public string? SpouseIdNo { get; set; }

        /// <summary>
        /// العنوان — where this employee lives now, as one written line.
        /// <para>
        /// Free text rather than the governorate → area → neighbourhood hierarchy
        /// <c>Parent</c> points at (<c>Sms.Domain.Geography</c>). That hierarchy exists to answer
        /// "which students live in this area", which drives transport routing and catchment
        /// reporting; nothing asks it of staff, and pointing an employee at it would oblige every
        /// school to catalogue its teachers' neighbourhoods before it could record an address at
        /// all. If a staff-by-area question ever arrives, this is the field that gets promoted.
        /// </para>
        /// </summary>
        public string? Address { get; set; }

        /// <summary>
        /// البلدة الأصلية — the town or village the employee's family is from, which in this
        /// product's first deployment is a distinct question from where they live now and is asked
        /// on every staff form (owner request, 2026-08-27).
        /// <para>
        /// Free text for the same reason as <see cref="Address"/>, and one more: the list a school
        /// would pick from is a historical gazetteer, not the current residence catalogue, and
        /// authoring one is content work rather than an engineering decision.
        /// </para>
        /// </summary>
        public string? OriginTown { get; set; }

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

        /// <summary>
        /// رقم محفظة بالي بي — the mobile wallet a school pays into when the employee has no bank
        /// account, or is paid outside the payroll run (owner request, 2026-08-27).
        /// <para>
        /// Audited with a mandatory reason for exactly the reason <see cref="BankAccountNo"/> is:
        /// this is a destination for money. That the amount is smaller and the rail is a phone
        /// rather than a bank changes nothing about who should be able to alter it quietly.
        /// </para>
        /// <para>
        /// Stored as written. The wallet is keyed by a mobile number today, but the field is not
        /// declared as one — a school that records an account reference instead should not have its
        /// entry refused by a validator this system invented.
        /// </para>
        /// </summary>
        [RequiresAuditReason]
        public string? PalPayWalletNo { get; set; }

        /// <summary>رقم محفظة جوال بي. The second wallet in the same market; see <see cref="PalPayWalletNo"/>.</summary>
        [RequiresAuditReason]
        public string? JawwalPayWalletNo { get; set; }
    }
}
