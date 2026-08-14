using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Workflow;

namespace Sms.Application.Workflow
{
    /// <summary>
    /// A module's final effect for a workflow (doc 05 §5 "Final effect" —
    /// create student, post discount, lock marks…). Runs inside the same
    /// save/transaction as the final transition, so approved-but-not-applied
    /// states are impossible (BR-WF-009). Implementations mutate through the
    /// ambient DbContext and must not save themselves.
    /// </summary>
    public interface IWorkflowFinalEffect
    {
        /// <summary>Catalog code this effect applies to (e.g. "WF-01").</summary>
        string WorkflowCode { get; }

        Task ApplyAsync(WorkflowInstance instance, CancellationToken cancellationToken = default);
    }
}
