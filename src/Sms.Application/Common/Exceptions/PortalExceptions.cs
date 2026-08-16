using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-SEC-011: the requesting account has no portal visibility into this student — surfaced as not-found (BR-SEC-010's own-area posture), not a permission error, by the caller.</summary>
    public class PortalAccessDeniedException : InvalidOperationException
    {
        public PortalAccessDeniedException(int studentId)
            : base($"No portal visibility into student {studentId} (BR-SEC-011).")
        {
        }
    }
}
