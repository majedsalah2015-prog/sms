using System;
using Sms.Domain.Common;

namespace Sms.Domain.Workflow
{
    /// <summary>
    /// The per-instance transition trail (DB/02 ER: instance ||--o{ step)
    /// feeding the workflow history panel and throughput reports (doc 05 §7/§9).
    /// The tamper-evident copy of the same facts lives in the audit store
    /// (BR-WF-002 via E-004).
    /// </summary>
    public class WorkflowStep : AuditableEntity
    {
        public int WorkflowInstanceId { get; set; }

        public int FromStateId { get; set; }

        public int ToStateId { get; set; }

        public WorkflowActionType Action { get; set; }

        public int ActorUserId { get; set; }

        /// <summary>Populated when acting under delegation (BR-WF-006, later slice).</summary>
        public bool IsDelegated { get; set; }

        public string? Reason { get; set; }

        public DateTime OccurredAtUtc { get; set; }
    }
}
