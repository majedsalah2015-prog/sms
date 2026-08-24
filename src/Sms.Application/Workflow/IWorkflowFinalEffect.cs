using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Workflow;

namespace Sms.Application.Workflow
{
    /// <summary>
    /// A module's final effect for a workflow (doc 05 §5 "Final effect" —
    /// create student, post discount, lock marks…). Runs inside the same
    /// transaction as the final transition, so approved-but-not-applied
    /// states are impossible (BR-WF-009).
    /// <para>
    /// An implementation may save: the service opens a transaction around the
    /// transition and every effect it runs, and <c>SmsDbContext</c> joins an
    /// ambient transaction rather than starting its own. That matters because a
    /// real final effect is rarely one <c>SaveChanges</c> — applying a discount
    /// issues numbered documents and then recomputes an installment schedule —
    /// and forcing those through a single save would mean rewriting the module's
    /// own operation instead of calling it.
    /// </para>
    /// <para>
    /// See <see cref="IWorkflowClosureEffect"/> for the other end: what a module
    /// does when its request is rejected or cancelled instead.
    /// </para>
    /// </summary>
    public interface IWorkflowFinalEffect
    {
        /// <summary>Catalog code this effect applies to (e.g. "WF-01").</summary>
        string WorkflowCode { get; }

        Task ApplyAsync(WorkflowInstance instance, CancellationToken cancellationToken = default);
    }
}
