using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Notifications
{
    /// <summary>
    /// msg.Provider (BR-NOT-009, doc/Modules/33 §8.3): per-school registry of
    /// which gateway serves a channel, and — since this slice — what it takes
    /// to reach it.
    /// <para>
    /// <b>The secret is never on this row in the clear.</b> <see cref="SecretCipher"/>
    /// holds what <c>ISecretProtector</c> produced; nothing reads it back except the
    /// sender at dispatch time, and no screen ever renders it. BR-NTF-003 asks for
    /// encrypted credentials entered by a Sys Admin and verifiable by a test action:
    /// that is exactly these three fields plus <see cref="LastTestOutcome"/>.
    /// </para>
    /// <para>
    /// T1-audited per BR-NTF-006 — changing which gateway a school's parents are
    /// reached through is a change to what families legally receive, and the field-level
    /// trail is the evidence of who changed it. The cipher itself is excluded from the
    /// diff by <c>AuditCaptor</c>'s secret-field rule, so rotating a token is recorded
    /// as having happened without writing the token into the audit log.
    /// </para>
    /// </summary>
    [Audited(AuditTier.T1)]
    public class Provider : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public NotificationChannel Channel { get; set; }

        /// <summary>The gateway, e.g. "TWILIO" or "360DIALOG" — matched against <c>IChannelSender.ProviderCode</c> at dispatch.</summary>
        public string ProviderCode { get; set; } = string.Empty;

        /// <summary>What the console calls this row when a school registers two gateways on one channel.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// The non-secret half of the credentials — Twilio's Account SID, 360dialog's
        /// channel id. Shown in the console; useful in a support call, useless alone.
        /// </summary>
        public string? AccountIdentifier { get; set; }

        /// <summary>The protected auth token. Never rendered, never logged, never returned by a port.</summary>
        [SecretField]
        public string? SecretCipher { get; set; }

        /// <summary>The number messages are sent from, E.164 (BR-NOT-009). For WhatsApp this is the WABA-registered number.</summary>
        public string? SenderId { get; set; }

        /// <summary>Overridable so a 360dialog or a sandbox base URL can be pointed at without a code change.</summary>
        public string? ApiBaseUrl { get; set; }

        /// <summary>BR-NTF-003: lowest first. Two active providers on one channel means the second is the failover.</summary>
        public int FailoverOrder { get; set; } = 1;

        public DateTime? LastTestedAtUtc { get; set; }

        public ProviderTestOutcome LastTestOutcome { get; set; } = ProviderTestOutcome.NeverTested;

        /// <summary>Why the last test failed, in the gateway's own words — a support detail, not a user-facing sentence.</summary>
        public string? LastTestDetail { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>Whether this row carries enough to attempt a send at all — the console's "configured" light and the sender's own precondition.</summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(AccountIdentifier)
            && !string.IsNullOrWhiteSpace(SecretCipher)
            && !string.IsNullOrWhiteSpace(SenderId);
    }
}
