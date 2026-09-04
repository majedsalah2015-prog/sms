using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Discounts;
using Sms.Application.Workflow;
using Sms.Domain.Discounts;
using Sms.Domain.Workflow;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;

namespace Sms.Web.Finance
{
    /// <summary>
    /// WF-04 (doc 05 §5, BR-DIS-003) around one discount grant, in the one place both screens that
    /// decide a grant read it from.
    /// <para>
    /// The grant desk (<c>DiscountsController</c>) owned this logic privately until the student fee
    /// file grew its own grant panel (doc/Modules/22 §8.3 is a *student* position, and a clerk sitting
    /// with a family should not have to leave the child's file to give them a discount). Two copies of
    /// "is a chain open, and if so approve through it" is exactly the drift that ends with one screen
    /// approving a grant directly while a request for it sits open in somebody's inbox forever — so
    /// the copies were made one.
    /// </para>
    /// <para>
    /// Static and service-taking rather than injected: this is composition glue over ports that are
    /// already registered, and registering a Web-layer coordinator in <c>Startup.cs</c> to hold three
    /// method bodies would buy nothing.
    /// </para>
    /// </summary>
    public static class GrantChain
    {
        /// <summary>
        /// The open WF-04 request for a grant, or null when it has none — automatic batch proposals
        /// (BR-DIS-002 decides an enumerated batch under one approval) and scholarship nominations
        /// (BR-DIS-004 routes those to a committee the seeded chain does not model) keep the direct
        /// path.
        /// </summary>
        public static async Task<WorkflowInstance?> OpenAsync(AppDbContext db, int grantId, CancellationToken cancellationToken)
            => await db.WorkflowInstances.AsNoTracking()
                .Where(i => !i.IsClosed && i.EntityTypeName == DiscountWorkflow.EntityTypeName && i.EntityId == grantId)
                .OrderByDescending(i => i.Id)
                .FirstOrDefaultAsync(cancellationToken);

        /// <summary>
        /// Raises the grant into WF-04 and submits it in one step. The routing value is the grant's
        /// own percentage equivalent — the same number BR-DIS-003 routed the tier from, read back
        /// through the port so the chain and the recorded tier cannot disagree about who signs.
        /// </summary>
        /// <returns>
        /// Null when the chain started. A bilingual warning when the school's catalogue has no WF-04:
        /// the grant is still proposed and still approvable from the screen, so a missing definition
        /// degrades to the old behaviour instead of losing the operator's work — but it is reported,
        /// because a chain nobody notices is missing is how an approval requirement quietly stops
        /// being enforced.
        /// </returns>
        public static async Task<string?> StartAsync(
            IDiscountAdmin discounts, IWorkflowService workflow, DiscountGrant grant, string reason, bool arabic, CancellationToken cancellationToken)
        {
            try
            {
                var percent = await discounts.GetGrantPercentEquivalentAsync(grant.Id, cancellationToken);
                var label = string.Format(
                    CultureInfo.InvariantCulture, "{0} {1}% · student #{2}",
                    DiscountWorkflow.Code, percent.ToString("0.##", CultureInfo.InvariantCulture), grant.StudentId);

                var instance = await workflow.StartAsync(
                    DiscountWorkflow.Code, DiscountWorkflow.EntityTypeName, grant.Id, label, percent, cancellationToken);
                await workflow.ExecuteAsync(instance.Id, WorkflowActionType.Submit, reason, cancellationToken: cancellationToken);
                return null;
            }
            catch (WorkflowDefinitionMissingException)
            {
                return arabic
                    ? "اقتُرحت المنحة، لكن لا يوجد مسار WF-04 معرَّف لهذه المدرسة، فهي ليست في طابور موافقات أحد — اعتمدها من هذه الشاشة."
                    : "The grant was proposed, but no WF-04 workflow is defined for this school, so it is not on anyone's approvals queue — approve it from this screen.";
            }
        }

        /// <summary>
        /// What the operator is told a proposal was routed to. BR-DIS-003 sends anything over 25% to
        /// the Owner, and the tier is recorded on the grant — but doc 06 §4.3 seeds no Owner role, so
        /// WF-04's chain stops at the principal. Naming an approver who will never be asked would be
        /// the worse half of that gap: the register keeps the tier, the message says who is actually
        /// going to sign, and says the rest is missing.
        /// </summary>
        public static string Routed(ApprovalTier tier, bool arabic)
            => tier == ApprovalTier.Owner
                ? (arabic
                    ? "اقتُرحت المنحة. وهي تتجاوز حدّ المدير، فتوجّهها BR-DIS-003 إلى المالك — وهو دور غير موجود في هذا التركيب، فتنتظر عند المدير ويُسجَّل مستوى المالك على المنحة."
                    : "Grant proposed. It exceeds the principal's threshold, so BR-DIS-003 routes it to the Owner — a role this deployment does not have, so it waits with the principal and the owner tier is recorded on the grant.")
                : (arabic
                    ? $"اقتُرحت المنحة — وُجّهت إلى {DiscountLabels.Tier(tier, true)} للاعتماد."
                    : $"Grant proposed — routed to {DiscountLabels.Tier(tier, false)} for approval.");

        public static string SelfApproval(bool arabic) => arabic
            ? "أنت من اقترح هذه المنحة، فلا يمكنك اعتمادها (BR-WF-003). تنتظر شخصاً آخر يحمل دور الاعتماد."
            : "You proposed this grant, so you cannot approve it (BR-WF-003). It waits for another holder of the approving role.";

        public static string NotTheApprover(bool arabic) => arabic
            ? "هذا الطلب ينتظر عند مستوى لا تبتّ فيه أدوارك."
            : "This request is waiting at a level your roles do not decide.";
    }
}
