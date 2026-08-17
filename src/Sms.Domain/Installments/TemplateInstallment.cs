using System;
using Sms.Domain.Common;

namespace Sms.Domain.Installments
{
    /// <summary>
    /// BR-INS-001 split rule row: percentage of the scheduled total plus a
    /// due-date rule — either an absolute date or an offset in days from
    /// the academic year start (term-start offsets need the Term to be
    /// picked per template row; not modeled, absolute dates cover it).
    /// Child of PlanTemplate — carries its own SchoolId so the tenant
    /// filter works at every level.
    /// </summary>
    public class TemplateInstallment : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int PlanTemplateId { get; set; }

        public int SequenceNumber { get; set; }

        public decimal SplitPercent { get; set; }

        /// <summary>Absolute due date; wins over the offset when set.</summary>
        public DateTime? DueDate { get; set; }

        /// <summary>Days from AcademicYear.StartDate; used when DueDate is null.</summary>
        public int? OffsetDaysFromYearStart { get; set; }
    }
}
