using System;
using Sms.Domain.Common;

namespace Sms.Domain.Jobs
{
    /// <summary>
    /// ops.JobRun: one row per execution attempt. Not
    /// <see cref="Audit.AuditedAttribute"/>-tagged — high-frequency
    /// execution log, same reasoning as Delivery/LoginAttempt; the run
    /// itself is also raised as an explicit AuditAction.JobRun event via
    /// IAuditEventWriter for the doc 07 system-audit domain.
    /// </summary>
    public class JobRun : AuditableEntity
    {
        public int JobDefinitionId { get; set; }

        public JobStatus Status { get; set; } = JobStatus.Running;

        public JobTriggerType TriggerType { get; set; }

        public DateTime StartedAtUtc { get; set; }

        public DateTime? CompletedAtUtc { get; set; }

        public string? ErrorMessage { get; set; }
    }
}
