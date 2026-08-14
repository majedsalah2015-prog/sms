using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>
    /// Raised when a T1 field marked [RequiresAuditReason] is changed without
    /// an ambient audit reason (doc 07 §3). Fails the whole save.
    /// </summary>
    public class MissingAuditReasonException : InvalidOperationException
    {
        public MissingAuditReasonException(string entityType, string fieldName)
            : base($"Changing '{entityType}.{fieldName}' requires a reason (audit tier T1).")
        {
        }
    }
}
