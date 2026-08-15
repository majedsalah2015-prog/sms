using System;
using Sms.Domain.Common;

namespace Sms.Domain.Notifications
{
    /// <summary>
    /// msg.Delivery (doc 09 §2): one row per recipient per channel per event.
    /// For <see cref="NotificationChannel.InApp"/> this row IS the inbox
    /// entry (doc 09 §5 bell/list/mark-read), not just a log of one. Not
    /// <see cref="Audit.AuditedAttribute"/>-tagged — it is itself the log;
    /// double-auditing its own status churn would be circular.
    /// </summary>
    public class Delivery : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public string EventCode { get; set; } = string.Empty;

        public NotificationChannel Channel { get; set; }

        public int RecipientUserId { get; set; }

        /// <summary>The exact content snapshot sent (BR-NOT-008) — never re-derived from a possibly-edited template.</summary>
        public int TemplateVersionId { get; set; }

        public string RenderedSubject { get; set; } = string.Empty;

        public string RenderedBody { get; set; } = string.Empty;

        public DeliveryStatus Status { get; set; } = DeliveryStatus.Queued;

        public string? ProviderReference { get; set; }

        /// <summary>BR-NOT-006: capped at 3: the 3rd failure is terminal (Status = Failed).</summary>
        public int AttemptCount { get; set; }

        public DateTime? LastAttemptAtUtc { get; set; }

        public string? FailureReason { get; set; }

        /// <summary>In-app only; unused (stays false) for other channels.</summary>
        public bool IsRead { get; set; }

        public DateTime? ReadAtUtc { get; set; }
    }
}
