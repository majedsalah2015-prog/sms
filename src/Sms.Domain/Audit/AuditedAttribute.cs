using System;

namespace Sms.Domain.Audit
{
    /// <summary>
    /// Assigns the entity's audit tier. Every module doc assigns each entity a
    /// tier (doc 07 §3); the persistence layer enforces the tier's behavior
    /// centrally. Configuration may raise the tier, never lower it (BR-AUD-002).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class AuditedAttribute : Attribute
    {
        public AuditedAttribute(AuditTier tier)
        {
            Tier = tier;
        }

        public AuditTier Tier { get; }
    }
}
