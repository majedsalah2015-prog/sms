using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sms.Application.Notifications
{
    /// <summary>
    /// BR-NOT-002: events fire only on committed business facts. Queues one
    /// <see cref="Domain.Notifications.Delivery"/> row per recipient per
    /// enabled channel — mutates the ambient DbContext and never saves
    /// itself, so the queueing commits atomically with the business event
    /// that raised it (same composition rule as
    /// <see cref="Workflow.IWorkflowFinalEffect"/> / numbering's
    /// INumberIssuer). Actually reaching a provider is
    /// <see cref="INotificationDispatcher"/>'s separate, decoupled job
    /// (BR-NOT-009: a provider outage must never block the transaction that
    /// raised the event).
    /// </summary>
    public interface INotificationPublisher
    {
        Task PublishAsync(
            string eventCode,
            IReadOnlyCollection<NotificationRecipient> recipients,
            IReadOnlyDictionary<string, string> payload,
            CancellationToken cancellationToken = default);
    }
}
