using System;
using Sms.Domain.Common;

namespace Sms.Domain.Security
{
    /// <summary>
    /// sec.UserSession (BR-SEC-004). One row per active login; the opaque
    /// <see cref="SessionToken"/> is what the auth cookie carries so idle and
    /// absolute expiry, and single-session revocation, are checked
    /// server-side. Not <see cref="Audit.AuditedAttribute"/>-tagged: every
    /// request touches <see cref="LastActivityAtUtc"/>, which would flood the
    /// audit store with field diffs for no security value.
    /// </summary>
    public class UserSession : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int UserAccountId { get; set; }

        public string SessionToken { get; set; } = Guid.NewGuid().ToString("N");

        public DateTime LastActivityAtUtc { get; set; }

        /// <summary>Absolute timeout ceiling (default 12h) — never extended by activity.</summary>
        public DateTime ExpiresAtUtc { get; set; }

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        public DateTime? RevokedAtUtc { get; set; }

        public string? RevokedReason { get; set; }
    }
}
