using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Discounts
{
    /// <summary>
    /// ppl.ScholarshipProgram + Envelope (doc/Modules/22 §7, BR-DIS-004):
    /// a named school-defined program with a per-year budget envelope —
    /// count cap and/or amount cap. Consumption is derived from Approved
    /// grants (count, and Σ AppliedAmount), never stored. Committee
    /// decision (P5/P3) is a status gate on the grant.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class ScholarshipProgram : AuditableEntity, ISchoolScoped, IYearScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        /// <summary>The discount type awards are instances of (100%-or-% per BR-DIS-004).</summary>
        public int DiscountTypeId { get; set; }

        public int? MaxAwards { get; set; }

        public decimal? MaxTotalAmount { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
