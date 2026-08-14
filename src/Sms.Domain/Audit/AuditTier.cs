namespace Sms.Domain.Audit
{
    /// <summary>
    /// Entity write-audit tiers of doc 07 §3. Lower value = stricter capture,
    /// so "raising" a tier (BR-AUD-002) moves toward T1. T0 (read audit) is not
    /// an entity tier — view/print/export events are logged per action through
    /// <see cref="AuditAction"/>.
    /// </summary>
    public enum AuditTier : short
    {
        /// <summary>Field-level old/new capture; reason mandatory on fields marked <see cref="RequiresAuditReasonAttribute"/>.</summary>
        T1 = 1,

        /// <summary>Field-level old/new capture; reason optional.</summary>
        T2 = 2,

        /// <summary>Record-level create/modify/deactivate events only, no field diff.</summary>
        T3 = 3,
    }
}
