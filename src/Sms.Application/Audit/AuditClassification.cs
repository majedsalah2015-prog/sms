using System;
using Sms.Domain.Audit;

namespace Sms.Application.Audit
{
    /// <summary>
    /// Resolves the effective audit tier for an entity. BR-AUD-002: auditing is
    /// not optional — configuration can raise a tier (toward T1), never lower
    /// it below the module doc's assignment.
    /// </summary>
    public static class AuditClassification
    {
        public static AuditTier Effective(AuditTier assigned, AuditTier? configured)
        {
            if (configured == null)
            {
                return assigned;
            }

            // Lower enum value = stricter tier; a looser configured value is ignored.
            return (AuditTier)Math.Min((short)assigned, (short)configured.Value);
        }
    }
}
