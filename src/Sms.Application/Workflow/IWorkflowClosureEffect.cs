using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Workflow;

namespace Sms.Application.Workflow
{
    /// <summary>
    /// A module's handler for a request that ends <em>without</em> being approved —
    /// rejected or cancelled. The counterpart of <see cref="IWorkflowFinalEffect"/>,
    /// and it exists because a record and its workflow must not disagree: when the
    /// engine closes an instance as Rejected, the module's own row has to stop
    /// saying "proposed", or two screens tell two different stories about the same
    /// request.
    /// <para>
    /// The engine calls this for a transition that lands on a final state and is not
    /// the one carrying the approval effect (BR-WF-009 owns that one). It runs in
    /// the same transaction as the transition, so the closure and the step trail
    /// commit together.
    /// </para>
    /// </summary>
    public interface IWorkflowClosureEffect
    {
        /// <summary>Catalog code this effect applies to (e.g. "WF-04").</summary>
        string WorkflowCode { get; }

        /// <summary>
        /// <paramref name="action"/> is what closed it — <see cref="WorkflowActionType.Reject"/>
        /// or <see cref="WorkflowActionType.Cancel"/> — and <paramref name="reason"/> is the one
        /// the engine already required for both (BR-WF-010, BR-GLB-032).
        /// </summary>
        Task ApplyAsync(WorkflowInstance instance, WorkflowActionType action, string? reason, CancellationToken cancellationToken = default);
    }
}
