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

        /// <summary>
        /// The exact content snapshot sent (BR-NOT-008) — never re-derived from a
        /// possibly-edited template.
        /// <para>
        /// Null for human-composed messages, which is what Module 32 is: an announcement
        /// has an author, not a template, and its text is already snapshotted on the
        /// delivery itself. Everything doc 09 raises still carries one.
        /// </para>
        /// </summary>
        public int? TemplateVersionId { get; set; }

        /// <summary>The announcement this delivery carries, when it carries one (doc/Modules/32 §8.1's reach reporting). Null for every doc 09 event.</summary>
        public int? AnnouncementId { get; set; }

        public string RenderedSubject { get; set; } = string.Empty;

        public string RenderedBody { get; set; } = string.Empty;

        /// <summary>
        /// Where it actually went — the E.164 number or the mailbox, snapshotted at
        /// publish time for the same reason the body is (BR-NOT-008, BR-NTF-006's
        /// two-year dispute evidence). A parent who changes their number in March must
        /// not be able to turn February's "we notified you on the 4th" into a message
        /// addressed to a number they never had.
        /// <para>
        /// Null for <see cref="NotificationChannel.InApp"/>, which has no address: the
        /// row itself is the destination. Null on an external channel means the address
        /// book had nothing for that recipient — the dispatcher fails the delivery
        /// rather than guessing, and the bounce lands in the data-quality queue
        /// (BR-NTF-005).
        /// </para>
        /// </summary>
        public string? RecipientAddress { get; set; }

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
