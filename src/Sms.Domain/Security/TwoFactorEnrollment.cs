using System;
using Sms.Domain.Common;

namespace Sms.Domain.Security
{
    /// <summary>
    /// sec.TwoFactorEnrollment (BR-SEC-003). One row per account per method;
    /// unconfirmed enrollments (secret issued, first code not yet verified)
    /// do not enforce 2FA at login — only <see cref="UserAccount.TwoFactorEnabled"/>
    /// does, flipped on once <see cref="ConfirmedAtUtc"/> is set.
    /// </summary>
    public class TwoFactorEnrollment : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int UserAccountId { get; set; }

        public TwoFactorMethod Method { get; set; }

        /// <summary>Base32 TOTP secret. Rests on TDE at the storage layer (BR-SEC-024) like the rest of the database — no separate field-level encryption in v1.</summary>
        public string SecretKey { get; set; } = string.Empty;

        public DateTime? ConfirmedAtUtc { get; set; }
    }
}
