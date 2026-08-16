using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Grading
{
    /// <summary>core.BlueprintComponent (doc/Modules/17 §7, BR-GRA-003): one weighted piece of a Blueprint's term calculation.</summary>
    [Audited(AuditTier.T2)]
    public class BlueprintComponent : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int BlueprintId { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        /// <summary>Percent of the term score this component contributes; all of a Blueprint's components must sum to 100 before it can be finalized.</summary>
        public decimal Weight { get; set; }

        public decimal MaxScore { get; set; }
    }
}
