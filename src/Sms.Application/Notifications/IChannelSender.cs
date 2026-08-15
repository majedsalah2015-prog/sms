using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Notifications;

namespace Sms.Application.Notifications
{
    /// <summary>
    /// One implementation per <see cref="NotificationChannel"/>, resolved by
    /// <see cref="INotificationDispatcher"/> from an injected collection (the
    /// same fan-out shape as <see cref="Workflow.IWorkflowFinalEffect"/>).
    /// Must never throw for an ordinary provider failure — return a Failure
    /// outcome instead, so one bad send can't sink the dispatch batch
    /// (BR-NOT-009).
    /// </summary>
    public interface IChannelSender
    {
        NotificationChannel Channel { get; }

        Task<ChannelSendOutcome> SendAsync(Delivery delivery, CancellationToken cancellationToken = default);
    }
}
