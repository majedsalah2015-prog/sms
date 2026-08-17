using System;
using Sms.Domain.Common;

namespace Sms.Domain.Installments
{
    /// <summary>
    /// ppl.DunningEvent (doc/Modules/20 §7, BR-INS-008/010): one ladder
    /// step fired for one installment. Append-only send log (BR-NOT-006
    /// spirit) — not [Audited]. The notification itself goes through
    /// E-007's INotificationPublisher; this row is what makes the ladder
    /// idempotent (a step fires once per installment).
    /// </summary>
    public class DunningEvent : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int InstallmentId { get; set; }

        public DunningStep Step { get; set; }

        public DateTime FiredAtUtc { get; set; }

        /// <summary>BR-INS-006: set when a broken promise pushed the ladder forward.</summary>
        public bool TriggeredByBrokenPromise { get; set; }
    }
}
