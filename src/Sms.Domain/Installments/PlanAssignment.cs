using System.Collections.Generic;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Installments
{
    /// <summary>
    /// ppl.PlanAssignment (doc/Modules/20 §7, BR-INS-002): student-year ×
    /// (optional) fee category × template. IsException marks a per-family
    /// deviation from the grade default (permission-gated, reason
    /// mandatory — the "default template per grade" config itself has no
    /// home until School settings exist; callers pass the template).
    /// T1 per BR-INS-010 (money-adjacent).
    /// </summary>
    [Audited(AuditTier.T1)]
    public class PlanAssignment : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int StudentId { get; set; }

        public int PayerId { get; set; }

        public int PlanTemplateId { get; set; }

        /// <summary>Null = all categories (mirrors PlanTemplate.FeeCategoryId).</summary>
        public int? FeeCategoryId { get; set; }

        public bool IsException { get; set; }

        public string? ExceptionReason { get; set; }

        /// <summary>BR-INS-005: reported per family as an abuse signal.</summary>
        public int RescheduleCount { get; set; }

        public List<Installment> Installments { get; set; } = new();
    }
}
