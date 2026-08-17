using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Messaging
{
    /// <summary>msg.OfficialLetter (doc/Modules/32 §7, BR-MSG-004): doc 08's "MSG" series (already seeded by E-010), template-rendered, per-recipient ack tracking. Template rendering itself reuses no engine yet — BodySnapshot is the caller-supplied rendered text, same "data ready, rendering deferred" posture as E-302/E-403's report/PDF gaps.</summary>
    [Audited(AuditTier.T1)]
    public class OfficialLetter : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        /// <summary>doc 08 MSG series.</summary>
        public string LetterNo { get; set; } = string.Empty;

        public string TemplateCode { get; set; } = string.Empty;

        public int RecipientUserId { get; set; }

        public string BodySnapshot { get; set; } = string.Empty;

        public bool RequiresAcknowledgment { get; set; }

        public DateTime? AcknowledgedAtUtc { get; set; }

        public DateTime IssuedAtUtc { get; set; }
    }
}
