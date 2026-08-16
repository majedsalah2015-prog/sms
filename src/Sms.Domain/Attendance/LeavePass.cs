using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Attendance
{
    /// <summary>
    /// core.LeavePass (doc/Modules/14 §7, BR-ATD-006): in-day short leave
    /// request — distinct from EarlyLeave/GateEvent (a leave pass always
    /// has a logged return; early leave does not).
    /// </summary>
    [Audited(AuditTier.T2)]
    public class LeavePass : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int EnrollmentId { get; set; }

        public DateTime RequestedAtUtc { get; set; }

        public string Reason { get; set; } = string.Empty;

        public LeavePassStatus Status { get; set; } = LeavePassStatus.Requested;

        public int? ApprovedByUserId { get; set; }

        public DateTime? ReleasedAtUtc { get; set; }

        public DateTime? ReturnedAtUtc { get; set; }
    }
}
