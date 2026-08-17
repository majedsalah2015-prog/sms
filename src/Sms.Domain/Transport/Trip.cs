using System;
using System.Collections.Generic;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Transport
{
    /// <summary>
    /// svc.Trip (doc/Modules/23 §7, BR-TRN-005): route × date (direction
    /// comes from the route). Opened only on a roadworthy bus with a
    /// licence-valid driver (or a logged Principal override); closed only
    /// when every roster student is resolved AND the "bus empty" sweep is
    /// confirmed. T2 per BR-TRN-009 (safety events are T1 separately).
    /// DriverId/AttendantId are per-trip so a substitution is just a
    /// different id than the route's default (BR-TRN-002).
    /// </summary>
    [Audited(AuditTier.T2)]
    public class Trip : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int RouteId { get; set; }

        public DateTime Date { get; set; }

        public RouteDirection Direction { get; set; }

        public int BusId { get; set; }

        public int DriverId { get; set; }

        public int? AttendantId { get; set; }

        public TripStatus Status { get; set; } = TripStatus.InProgress;

        public DateTime OpenedAtUtc { get; set; }

        public DateTime? ClosedAtUtc { get; set; }

        public bool SweepConfirmed { get; set; }

        /// <summary>Roster size at open — the students with an active subscription on this route's stops for the date.</summary>
        public int RosterCount { get; set; }

        public List<TripLog> Logs { get; set; } = new();
    }

    /// <summary>BR-TRN-005 per-student event log; BR-TRN-006 handover captures who received the child at a PM stop.</summary>
    public class TripLog : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int TripId { get; set; }

        public int StudentId { get; set; }

        public TripLogEvent Event { get; set; }

        public DateTime AtUtc { get; set; }

        public int ActorUserId { get; set; }

        public string? ReceivedByName { get; set; }

        public bool HandoverConfirmed { get; set; }
    }

    /// <summary>svc.SafetyEvent (BR-TRN-005/006/009): T1 — the safety record and its escalation state.</summary>
    [Audited(AuditTier.T1)]
    public class SafetyEvent : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int? TripId { get; set; }

        public int? StudentId { get; set; }

        public SafetyEventKind Kind { get; set; }

        public SafetyEventState State { get; set; } = SafetyEventState.Open;

        public DateTime OccurredAtUtc { get; set; }

        public string? Note { get; set; }

        [RequiresAuditReason]
        public DateTime? ResolvedAtUtc { get; set; }
    }
}
