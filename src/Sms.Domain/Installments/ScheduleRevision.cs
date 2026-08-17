using System;
using Sms.Domain.Common;

namespace Sms.Domain.Installments
{
    /// <summary>
    /// BR-INS-003: every controlled recomputation (new charge appended,
    /// credit note/discount reduction, approved reschedule) logs a
    /// before/after snapshot of the schedule. Append-only log — never
    /// [Audited] itself (auditing an audit-like log is circular, same as
    /// TemplateVersion/PasswordHistory).
    /// </summary>
    public class ScheduleRevision : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int PlanAssignmentId { get; set; }

        public ScheduleRevisionCause Cause { get; set; }

        public string? Reason { get; set; }

        public string BeforeJson { get; set; } = string.Empty;

        public string AfterJson { get; set; } = string.Empty;

        public DateTime OccurredAtUtc { get; set; }
    }

    public enum ScheduleRevisionCause : short
    {
        Generated = 1,
        ChargeAppended = 2,
        Reduced = 3,
        Rescheduled = 4,
    }
}
