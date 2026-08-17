using System.Collections.Generic;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Installments
{
    /// <summary>
    /// ppl.PlanTemplate (doc/Modules/20 §7, BR-INS-001): a school-per-year
    /// installment plan (annual / 2 semesters / 4 installments / 10
    /// monthly…). Percentage splits only in this slice — a fixed-amount
    /// split is representable as a percentage of the scheduled total, and
    /// nothing downstream needs the distinction yet. Category
    /// applicability is a single optional FeeCategory (null = every
    /// posted charge of the student-year) — the doc's "category group"
    /// has no entity anywhere in Fees (E-303) to key on. Approval is P3
    /// with the fee structure per the doc; enforced as a status gate only
    /// (same status-only workflow substitution as FeeStructureLine).
    /// Down-payment enforcement at registration is the doc's own open
    /// question Q3 — the percentage is captured, nothing blocks on it.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class PlanTemplate : AuditableEntity, ISchoolScoped, IYearScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        /// <summary>Null = applies to all categories.</summary>
        public int? FeeCategoryId { get; set; }

        public decimal DownPaymentPercent { get; set; }

        /// <summary>BR-INS-007: days after DueDate before Due becomes Overdue.</summary>
        public int GraceDays { get; set; }

        public PlanTemplateStatus Status { get; set; } = PlanTemplateStatus.Draft;

        public bool IsActive { get; set; } = true;

        public List<TemplateInstallment> Installments { get; set; } = new();
    }
}
