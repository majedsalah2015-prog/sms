using Sms.Domain.Common;

namespace Sms.Domain.Audit
{
    /// <summary>
    /// aud.AnomalyRule (doc/Modules/34 §7, BR-AUM-002): a configurable
    /// detection over the audit stream. Global catalog, not ISchoolScoped —
    /// matches AuditEntry/IntegrityCheckpoint's own unfiltered reasoning
    /// (auditors work cross-school under their own permission gate).
    /// </summary>
    [Audited(AuditTier.T2)]
    public class AnomalyRule : AuditableEntity, ISoftActiveFiltered
    {
        public string Code { get; set; } = string.Empty;

        public string DescriptionAr { get; set; } = string.Empty;

        public string DescriptionEn { get; set; } = string.Empty;

        public AnomalySeverity Severity { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
