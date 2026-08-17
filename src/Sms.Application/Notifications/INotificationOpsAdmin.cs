using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Notifications;

namespace Sms.Application.Notifications
{
    /// <summary>
    /// doc/Modules/33 (M33, "Notifications Administration") — the
    /// operational rules layered on top of doc 09's engine (E-007):
    /// versioned template publish lifecycle (BR-NTF-001), the statutory
    /// subscription floor (BR-NTF-002), and budget threshold checks
    /// (BR-NTF-004) against the existing BudgetCounter (E-007). Provider
    /// credential/failover management (BR-NTF-003) and the delivery-
    /// failure ops queue (BR-NTF-005) are deferred — separate,
    /// screens-heavy surfaces not needed to make the operational rules
    /// above real.
    /// </summary>
    public interface INotificationOpsAdmin
    {
        /// <summary>BR-NTF-001: Draft -&gt; TestSent — mandatory before PublishTemplateVersionAsync.</summary>
        Task MarkTemplateVersionTestSentAsync(int templateVersionId, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.InvalidTemplatePublishTransitionException"/> unless the version was test-sent first.</summary>
        Task PublishTemplateVersionAsync(int templateVersionId, CancellationToken cancellationToken = default);

        Task SetSubscriptionStatutoryAsync(int subscriptionRuleId, bool isStatutory, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.StatutorySubscriptionChangeDeniedException"/> for a statutory rule unless principalApprovalGranted is set (BR-NTF-002).</summary>
        Task DisableSubscriptionAsync(int subscriptionRuleId, bool principalApprovalGranted = false, CancellationToken cancellationToken = default);

        /// <summary>BR-NTF-004: reads the period's existing BudgetCounter against the given limit — never mutates the counter itself (that's the dispatcher's job, E-007).</summary>
        Task<BudgetCheckResult> EvaluateBudgetAsync(
            NotificationChannel channel, string periodKey, int budgetLimit, bool isSafetyClass, CancellationToken cancellationToken = default);
    }
}
