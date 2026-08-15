using Sms.Domain.Common;

namespace Sms.Domain.Security
{
    /// <summary>
    /// sec.LoginAttempt (BR-SEC-002 lockout counting + BR-SEC-022 security
    /// reports). UserAccountId is null when the attempted username does not
    /// resolve — the row still records the attempted name for auditors.
    /// <see cref="AuditableEntity.CreatedAtUtc"/> is the attempt timestamp.
    /// </summary>
    public class LoginAttempt : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int? UserAccountId { get; set; }

        public string UserNameAttempted { get; set; } = string.Empty;

        public bool Succeeded { get; set; }

        public string? FailureReason { get; set; }

        public string? IpAddress { get; set; }
    }
}
