using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Workflow;

namespace Sms.Application.Workflow
{
    /// <summary>
    /// Orchestration port: starts instances on the active definition version
    /// and executes transitions atomically (state + step trail + audit + final
    /// effects in one transaction, BR-WF-002/009).
    /// </summary>
    public interface IWorkflowService
    {
        Task<WorkflowInstance> StartAsync(
            string workflowCode,
            string entityTypeName,
            long entityId,
            string? businessKey = null,
            decimal? routingValue = null,
            CancellationToken cancellationToken = default);

        Task<WorkflowTransitionResult> ExecuteAsync(
            int instanceId,
            WorkflowActionType action,
            string? reason = null,
            WorkflowRecordScope? recordScope = null,
            CancellationToken cancellationToken = default);
    }
}
