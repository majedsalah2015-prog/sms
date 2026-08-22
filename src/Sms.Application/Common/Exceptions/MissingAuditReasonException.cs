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
            EntityType = entityType;
            FieldName = fieldName;
        }

        /// <summary>
        /// What was being changed, exposed so a screen can say it in the reader's language. The
        /// message itself stays English — it is what the log reads, and a log should read the same
        /// in every deployment.
        /// </summary>
        public string EntityType { get; }

        public string FieldName { get; }
    }
}
