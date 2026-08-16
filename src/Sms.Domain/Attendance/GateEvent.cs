using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Attendance
{
    /// <summary>
    /// core.GateEvent (doc/Modules/14 §7, BR-ATD-004): reception log of a
    /// late arrival or an early-leave release. Purely a log in this slice —
    /// it does not automatically flip the day's AttendanceDay.Status
    /// (that composition is deferred, same as this codebase's other
    /// partial cross-entity wiring).
    /// </summary>
    [Audited(AuditTier.T1)]
    public class GateEvent : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int EnrollmentId { get; set; }

        public GateEventType EventType { get; set; }

        public DateTime EventTimeUtc { get; set; }

        /// <summary>Early-leave release only.</summary>
        public string? PickupPersonName { get; set; }

        /// <summary>BR-ATD-004: true = released to someone not on the authorized-pickup list — an explicit, reasoned override.</summary>
        [RequiresAuditReason]
        public bool IsAuthorizedPickupOverride { get; set; }

        public int? ReleasedByUserId { get; set; }
    }
}
