using Sms.Domain.Common;

namespace Sms.Domain.Notifications
{
    /// <summary>
    /// msg.BudgetCounter (BR-NOT-006): per-school SMS/WhatsApp send count per
    /// period. Tracking only in v1 — hard-cap/alert enforcement is an open
    /// question (doc 09 §9 Q3), not a decided rule.
    /// </summary>
    public class BudgetCounter : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public NotificationChannel Channel { get; set; }

        /// <summary>"yyyy-MM", school-TZ-agnostic for v1 (no School/timezone entity yet).</summary>
        public string PeriodKey { get; set; } = string.Empty;

        public int MessageCount { get; set; }
    }
}
