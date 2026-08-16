using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Classrooms
{
    /// <summary>
    /// core.RoomAvailabilityException (BR-ROM-004): a room under maintenance
    /// (or reserved) for a date range is excluded from placement. This is
    /// the single source of truth for "is this room available" — no
    /// separate Room.Status field, to avoid two places that can drift out
    /// of sync. BR-ROM-008: maintenance status changes are T2-audited
    /// (they disrupt operations) — stricter than the T3 catalog default.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class RoomAvailabilityException : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int RoomId { get; set; }

        public RoomAvailabilityReason Reason { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public string? Notes { get; set; }
    }
}
