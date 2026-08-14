using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>
    /// Thrown when a write targets a school other than the ambient tenant
    /// (BR-GLB-010: no cross-school data access without explicit scope).
    /// </summary>
    public class CrossSchoolWriteException : InvalidOperationException
    {
        public CrossSchoolWriteException(string entityType, int entitySchoolId, int tenantSchoolId)
            : base($"Entity '{entityType}' targets SchoolId {entitySchoolId} but the current tenant is SchoolId {tenantSchoolId} (BR-GLB-010).")
        {
        }
    }
}
