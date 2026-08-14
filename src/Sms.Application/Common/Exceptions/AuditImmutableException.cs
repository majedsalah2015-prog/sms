using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>
    /// Raised when any code path attempts to update or delete audit storage —
    /// no such path may exist for any role (BR-AUD-001, BR-GLB-081).
    /// </summary>
    public class AuditImmutableException : InvalidOperationException
    {
        public AuditImmutableException(string entityType)
            : base($"Audit storage is append-only; '{entityType}' rows cannot be modified or deleted (BR-AUD-001).")
        {
        }
    }
}
