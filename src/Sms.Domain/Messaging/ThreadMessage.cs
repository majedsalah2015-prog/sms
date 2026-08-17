using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Messaging
{
    /// <summary>msg.ThreadMessage (doc/Modules/32 §7): one post within a MessageThread.</summary>
    [Audited(AuditTier.T2)]
    public class ThreadMessage : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int ThreadId { get; set; }

        public int SenderUserId { get; set; }

        public string Body { get; set; } = string.Empty;

        public DateTime SentAtUtc { get; set; }
    }
}
