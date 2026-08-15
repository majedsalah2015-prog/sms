using System.Threading;
using System.Threading.Tasks;

namespace Sms.Application.Notifications
{
    /// <summary>
    /// Drains queued deliveries through the matching <see cref="IChannelSender"/>
    /// (BR-NOT-006 retry-with-backoff, up to 3 attempts). A standalone
    /// operation — saves its own batch, unlike the publisher. Runs inline for
    /// now; recurring invocation via a job scheduler is E-011's job (same
    /// deferral as E-004's checkpoint job and E-005's SLA escalation).
    /// </summary>
    public interface INotificationDispatcher
    {
        /// <summary>Returns how many queued deliveries were attempted this pass.</summary>
        Task<int> DispatchQueuedAsync(CancellationToken cancellationToken = default);
    }
}
