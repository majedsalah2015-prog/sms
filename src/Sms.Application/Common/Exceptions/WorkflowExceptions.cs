using System;
using Sms.Domain.Workflow;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>No defined transition matches — state moves only through workflow actions (BR-WF-001).</summary>
    public class WorkflowTransitionNotAllowedException : InvalidOperationException
    {
        public WorkflowTransitionNotAllowedException(string workflowCode, string fromStateCode, WorkflowActionType action)
            : base($"Workflow {workflowCode}: no transition allows '{action}' from state '{fromStateCode}' (BR-WF-001).")
        {
        }
    }

    /// <summary>The actor lacks the role, permission, or data scope for the transition (BR-WF-004).</summary>
    public class WorkflowActorNotAuthorizedException : InvalidOperationException
    {
        public WorkflowActorNotAuthorizedException(string workflowCode, WorkflowActionType action, string detail)
            : base($"Workflow {workflowCode}: '{action}' denied — {detail}.")
        {
        }
    }

    /// <summary>An approver cannot approve their own submission, even when roles overlap (BR-WF-003).</summary>
    public class WorkflowSelfApprovalException : InvalidOperationException
    {
        public WorkflowSelfApprovalException(string workflowCode)
            : base($"Workflow {workflowCode}: self-approval is blocked (BR-WF-003).")
        {
        }
    }

    /// <summary>Reason missing where the policy or the hard rules demand one (BR-WF-010, BR-GLB-032).</summary>
    public class WorkflowReasonRequiredException : InvalidOperationException
    {
        public WorkflowReasonRequiredException(string workflowCode, WorkflowActionType action)
            : base($"Workflow {workflowCode}: '{action}' requires a reason.")
        {
        }
    }
}
