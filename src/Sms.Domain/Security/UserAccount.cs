using System;
using Sms.Domain.Common;

namespace Sms.Domain.Security
{
    /// <summary>
    /// sec.UserAccount. One person = one account (BR-GLB-002, doc 06 §2);
    /// accounts are deactivated, never deleted. Credential fields below are
    /// the E-003 authentication slice (doc 06 §3). Deliberately not
    /// <see cref="Audit.AuditedAttribute"/>-tagged: AccessFailedCount churns
    /// on every attempt, and security events are captured explicitly via
    /// <see cref="Application.Audit.IAuditEventWriter"/> (AuditAction.Login*)
    /// instead of noisy generic field diffs.
    /// </summary>
    public class UserAccount : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public string UserName { get; set; } = string.Empty;

        public AccountType AccountType { get; set; }

        /// <summary>Link to the person entity once People modules (S2) exist.</summary>
        public int? PersonId { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>Null until a password is set (BR-SEC-005 one-time provisioning).</summary>
        public string? PasswordHash { get; set; }

        /// <summary>Changes with every credential-affecting event; not used for session invalidation yet (pends E-003 session-context slice).</summary>
        public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

        public DateTime? PasswordChangedAtUtc { get; set; }

        /// <summary>BR-SEC-005: first login and admin resets force a change before any other action.</summary>
        public bool MustChangePassword { get; set; }

        /// <summary>BR-SEC-002 counter; reset on any successful login.</summary>
        public int AccessFailedCount { get; set; }

        /// <summary>BR-SEC-002 timed unlock; null = not locked out.</summary>
        public DateTime? LockedOutUntilUtc { get; set; }

        /// <summary>BR-SEC-003: TOTP required at login once a confirmed enrollment exists.</summary>
        public bool TwoFactorEnabled { get; set; }
    }
}
