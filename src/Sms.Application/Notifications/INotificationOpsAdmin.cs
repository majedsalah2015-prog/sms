using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Notifications;

namespace Sms.Application.Notifications
{
    /// <summary>
    /// doc/Modules/33 (M33, "Notifications Administration") — the operational rules
    /// layered on top of doc 09's engine (E-007): the versioned template publish
    /// lifecycle (BR-NTF-001), the statutory subscription floor (BR-NTF-002), the
    /// provider registry and its credentials (BR-NTF-003), budget thresholds
    /// (BR-NTF-004), and the delivery-failure operations queue (BR-NTF-005).
    /// <para>
    /// The provider half deliberately never returns a secret. It takes one in on the
    /// way down, hands it to <c>ISecretProtector</c>, and there is no method here that
    /// gives it back — a Sys Admin who has forgotten a token re-enters it, and no
    /// screen, log or export can be made to print one.
    /// </para>
    /// </summary>
    public interface INotificationOpsAdmin
    {
        // ------------------------------------------------------------------ templates

        /// <summary>BR-NTF-001: Draft -&gt; TestSent — mandatory before PublishTemplateVersionAsync.</summary>
        Task MarkTemplateVersionTestSentAsync(int templateVersionId, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.InvalidTemplatePublishTransitionException"/> unless the version was test-sent first.</summary>
        Task PublishTemplateVersionAsync(int templateVersionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-NTF-001's test send, for real: renders the version with sample values and
        /// queues one delivery to <paramref name="recipientUserId"/> on the template's own
        /// channel, then marks the version TestSent. The delivery goes through the ordinary
        /// dispatcher, so a WhatsApp template is proved against the actual gateway rather
        /// than against a preview pane.
        /// <para>
        /// Throws <see cref="Common.Exceptions.UnknownTemplateException"/> for a version that
        /// is not this school's, and
        /// <see cref="Common.Exceptions.RecipientUnreachableException"/> when the tester has
        /// no address on that channel — which is the point of testing.
        /// </para>
        /// </summary>
        Task<int> TestSendTemplateVersionAsync(int templateVersionId, int recipientUserId, CancellationToken cancellationToken = default);

        // ------------------------------------------------------------------ subscriptions

        Task SetSubscriptionStatutoryAsync(int subscriptionRuleId, bool isStatutory, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.StatutorySubscriptionChangeDeniedException"/> for a statutory rule unless principalApprovalGranted is set (BR-NTF-002).</summary>
        Task DisableSubscriptionAsync(int subscriptionRuleId, bool principalApprovalGranted = false, CancellationToken cancellationToken = default);

        // ------------------------------------------------------------------ providers (BR-NTF-003)

        /// <summary>Every registered gateway, deactivated ones included, in failover order within a channel.</summary>
        Task<IReadOnlyList<Provider>> ListProvidersAsync(CancellationToken cancellationToken = default);

        Task<Provider?> GetProviderAsync(int providerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Registers a gateway or amends one. <paramref name="secret"/> is protected before
        /// it is stored; passing null leaves whatever is already there, which is how the
        /// console lets a Sys Admin change a sender number without retyping a token it
        /// cannot show them.
        /// <para>
        /// Throws <see cref="Common.Exceptions.UnknownProviderCodeException"/> for a code
        /// <see cref="ProviderCatalog"/> does not define, and
        /// <see cref="Common.Exceptions.ProviderChannelMismatchException"/> for a gateway
        /// that does not serve the chosen channel.
        /// </para>
        /// </summary>
        Task<Provider> SaveProviderAsync(
            int? providerId,
            NotificationChannel channel,
            string providerCode,
            string displayName,
            string? accountIdentifier,
            string? secret,
            string? senderId,
            string? apiBaseUrl,
            int failoverOrder,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-NTF-003's verify action: asks the gateway whether the stored credentials are
        /// accepted, and records the answer on the row. Never throws for a rejection — a
        /// wrong token is an answer, not a fault.
        /// </summary>
        Task<ProviderTestOutcome> TestProviderAsync(int providerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retires a gateway. Throws
        /// <see cref="Common.Exceptions.ProviderInUseException"/> when it is the only active
        /// one on a channel that still has enabled subscription rules — BR-NTF-003's
        /// "deletion blocked while referenced", which here means: do not let a school
        /// silently stop reaching its parents by deactivating the only way it could.
        /// </summary>
        Task DeactivateProviderAsync(int providerId, CancellationToken cancellationToken = default);

        Task ReactivateProviderAsync(int providerId, CancellationToken cancellationToken = default);

        // ------------------------------------------------------------------ delivery operations (BR-NTF-005)

        /// <summary>
        /// The delivery log, newest first, filtered as the operations screen asks.
        /// <paramref name="take"/> caps the page — the table grows without bound and a
        /// screen that reads all of it is a screen that stops opening in year two.
        /// </summary>
        Task<IReadOnlyList<DeliveryRow>> ListDeliveriesAsync(
            DeliveryStatus? status = null,
            NotificationChannel? channel = null,
            string? eventCode = null,
            int take = 200,
            CancellationToken cancellationToken = default);

        /// <summary>Counts by status for the header, computed in the database rather than off the capped page.</summary>
        Task<DeliveryTotals> CountDeliveriesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-NTF-005: puts a terminally-failed delivery back in the queue with its attempt
        /// count reset, so the dispatcher's three-strike rule applies to the retry rather
        /// than treating it as a fourth strike and failing it unsent. Returns how many rows
        /// moved; a delivery that was not Failed is skipped, not an error.
        /// </summary>
        Task<int> RetryDeliveriesAsync(IReadOnlyCollection<int> deliveryIds, CancellationToken cancellationToken = default);

        // ------------------------------------------------------------------ the notification centre (§8.6)

        /// <summary>
        /// One user's in-app inbox, newest first — doc 09 §5's list, off the same
        /// <c>Delivery</c> rows the log shows, because for in-app the row is the inbox entry.
        /// <paramref name="includeRead"/> false is the bell's unread list.
        /// </summary>
        Task<IReadOnlyList<InboxItem>> ListInboxAsync(int userId, bool includeRead, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks one notification read. <paramref name="userId"/> is not decoration: it is the
        /// scope, and a delivery belonging to anybody else is ignored rather than read on their
        /// behalf — this is the only screen in the module with no permission behind it, so the
        /// port has to be the thing that refuses.
        /// </summary>
        Task MarkInAppReadAsync(int deliveryId, int userId, CancellationToken cancellationToken = default);

        /// <summary>Marks everything unread in this user's inbox read; returns how many.</summary>
        Task<int> MarkAllInAppReadAsync(int userId, CancellationToken cancellationToken = default);

        // ------------------------------------------------------------------ budgets (BR-NTF-004)

        /// <summary>BR-NTF-004: reads the period's existing BudgetCounter against the given limit — never mutates the counter itself (that's the dispatcher's job, E-007).</summary>
        Task<BudgetCheckResult> EvaluateBudgetAsync(
            NotificationChannel channel, string periodKey, int budgetLimit, bool isSafetyClass, CancellationToken cancellationToken = default);

        /// <summary>
        /// The costed channels' spend for <paramref name="periodKey"/> (a "yyyy-MM"), each
        /// against the ceiling configured in settings. A channel with no counter row yet
        /// still returns a row at zero — a console that hides an unused channel cannot be
        /// used to set its budget before it is used.
        /// </summary>
        Task<IReadOnlyList<BudgetRow>> ListBudgetsAsync(string periodKey, CancellationToken cancellationToken = default);
    }
}
