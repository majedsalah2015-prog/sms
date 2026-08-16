using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Grading
{
    /// <summary>core.ScaleBand (doc/Modules/17 §7, BR-GRA-001): one min-max % row within a GradingScale. Contiguous/non-overlapping ranges are a validation concern, not enforced by the shape itself.</summary>
    [Audited(AuditTier.T2)]
    public class ScaleBand : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int GradingScaleId { get; set; }

        public decimal MinPercent { get; set; }

        public decimal MaxPercent { get; set; }

        public string BandCode { get; set; } = string.Empty;

        public string LabelAr { get; set; } = string.Empty;

        public string LabelEn { get; set; } = string.Empty;

        public decimal? GpaPoints { get; set; }

        public bool IsPassing { get; set; }

        public int SortOrder { get; set; }
    }
}
