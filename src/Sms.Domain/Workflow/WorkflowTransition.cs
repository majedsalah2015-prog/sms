using Sms.Domain.Security;

namespace Sms.Domain.Workflow
{
    /// <summary>
    /// Allowed movement state→state bound to an action, an approver role,
    /// an optional permission (whose scopes gate the approver, BR-WF-004),
    /// and an optional threshold range for P4 routing (doc 05 §2, §4).
    /// P3 multi-level chains are modeled as intermediate states with one
    /// transition per level; P5 committees use the same shape sequentially
    /// (v1 emulation per §4).
    /// </summary>
    public class WorkflowTransition : Common.AuditableEntity
    {
        public int WorkflowDefinitionId { get; set; }

        public int FromStateId { get; set; }

        public int ToStateId { get; set; }

        public WorkflowActionType Action { get; set; }

        /// <summary>Role that may act; null = any holder of the bound permission (e.g. submitter actions).</summary>
        public int? RequiredRoleId { get; set; }

        public ReasonPolicy ReasonPolicy { get; set; } = ReasonPolicy.Optional;

        /// <summary>P4 routing (BR-WF-005): applies when RoutingValue &gt; Min (exclusive). Null = no lower bound.</summary>
        public decimal? MinRoutingValue { get; set; }

        /// <summary>P4 routing (BR-WF-005): applies when RoutingValue ≤ Max (inclusive). Null = no upper bound.</summary>
        public decimal? MaxRoutingValue { get; set; }

        /// <summary>Marks the transition whose completion applies the workflow's final effect (BR-WF-009).</summary>
        public bool TriggersFinalEffect { get; set; }

        // Optional bound permission (doc 05 §2 actor resolution + doc 06 scopes).
        public string? PermissionModuleCode { get; set; }

        public string? PermissionScreenCode { get; set; }

        public ActionVerb? PermissionAction { get; set; }
    }
}
