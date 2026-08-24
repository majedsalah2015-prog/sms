using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Application.Common.Interfaces;
using Sms.Application.Discounts;
using Sms.Application.Workflow;
using Sms.Domain.Workflow;

namespace Sms.Infrastructure.Discounts
{
    /// <summary>
    /// What WF-04 does to a discount grant when its chain ends (doc 05 §5's
    /// "Final effect: discount applied to student fees", BR-DIS-005).
    /// <para>
    /// Both effects delegate to <see cref="IDiscountAdmin"/> rather than touching
    /// the grant themselves. That is the point of the arrangement: approval still
    /// means exactly what it meant before — numbered discount documents against the
    /// applicable charges, capped so nothing goes below zero, forward installments
    /// reduced — and the workflow decides <em>when</em> and <em>by whom</em>, not
    /// what. A second implementation of BR-DIS-005 living in a workflow effect is
    /// how the two would drift apart.
    /// </para>
    /// <para>
    /// They may save, and they do: <c>ApproveGrantAsync</c> saves twice and then
    /// calls the installment engine. The workflow service owns a transaction around
    /// the transition and its effects, so all of it commits with the step trail or
    /// none of it does (BR-WF-009).
    /// </para>
    /// </summary>
    public class DiscountGrantApprovalEffect : IWorkflowFinalEffect
    {
        private readonly IDiscountAdmin _discounts;
        private readonly ICurrentUser _user;

        public DiscountGrantApprovalEffect(IDiscountAdmin discounts, ICurrentUser user)
        {
            _discounts = discounts;
            _user = user;
        }

        public string WorkflowCode => DiscountWorkflow.Code;

        /// <summary>
        /// Applies the grant as the approver whose action completed the chain.
        /// <para>
        /// A scholarship award whose envelope is exhausted throws here, which rolls
        /// the whole transition back and leaves the request waiting — deliberately.
        /// The override is a T1 reason on the grant (BR-DIS-004) and it belongs on
        /// the module's own screen, where there is a field to write it in; a generic
        /// approvals inbox has no business carrying an override nobody typed.
        /// </para>
        /// </summary>
        public async Task ApplyAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
        {
            if (!DiscountWorkflow.IsGrant(instance))
            {
                return;
            }

            await _discounts.ApproveGrantAsync((int)instance.EntityId, _user.UserId, cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// The other end of WF-04: a grant whose request was rejected or cancelled must
    /// stop reading as "proposed" in the discount register, or the register and the
    /// approvals inbox describe the same request differently.
    /// </summary>
    public class DiscountGrantClosureEffect : IWorkflowClosureEffect
    {
        private readonly IDiscountAdmin _discounts;
        private readonly ICurrentUser _user;

        public DiscountGrantClosureEffect(IDiscountAdmin discounts, ICurrentUser user)
        {
            _discounts = discounts;
            _user = user;
        }

        public string WorkflowCode => DiscountWorkflow.Code;

        /// <summary>
        /// Cancellation lands on Rejected as well: <c>DiscountGrantStatus</c> has no
        /// Cancelled member, and inventing one would change a persisted enum for a
        /// distinction the reason text already carries. The reason is mandatory for
        /// both actions (BR-WF-010, BR-GLB-032), so what actually happened is on the
        /// record either way.
        /// </summary>
        public async Task ApplyAsync(WorkflowInstance instance, WorkflowActionType action, string? reason, CancellationToken cancellationToken = default)
        {
            if (!DiscountWorkflow.IsGrant(instance))
            {
                return;
            }

            await _discounts.RejectGrantAsync(
                (int)instance.EntityId, _user.UserId, reason ?? action.ToString(), cancellationToken);
        }
    }
}
