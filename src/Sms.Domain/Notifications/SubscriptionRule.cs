using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Notifications
{
    /// <summary>
    /// msg.SubscriptionRule (doc 09 §2, BR-NOT-003): the school's on/off +
    /// timing switch per (event, channel). "Who" is resolved by the
    /// publishing module (doc 09's Recipient Resolution step, e.g.
    /// guardianship links) and passed to <see cref="Application.Notifications.INotificationPublisher"/> —
    /// this row never names recipients itself.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class SubscriptionRule : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public string EventCode { get; set; } = string.Empty;

        public NotificationChannel Channel { get; set; }

        public NotificationTiming Timing { get; set; } = NotificationTiming.Immediate;

        /// <summary>Disabling the rule is the enable/disable switch (BR-NOT-003) — no separate flag.</summary>
        public bool IsActive { get; set; } = true;
    }
}
