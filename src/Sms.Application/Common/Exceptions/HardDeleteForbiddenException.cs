using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>
    /// Thrown when code attempts to physically delete master data
    /// (ADR-7, BR-GLB-005: deactivate, never hard-delete; the only physical
    /// deletes are certified retention purges outside EF).
    /// </summary>
    public class HardDeleteForbiddenException : InvalidOperationException
    {
        public HardDeleteForbiddenException(string entityType)
            : base($"Entity '{entityType}' is master data and cannot be hard-deleted; set IsActive = false instead (BR-GLB-005).")
        {
        }
    }
}
