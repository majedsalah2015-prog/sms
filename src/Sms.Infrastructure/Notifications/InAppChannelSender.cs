using System.Threading;
using System.Threading.Tasks;
using Sms.Application.Notifications;
using Sms.Domain.Notifications;

namespace Sms.Infrastructure.Notifications
{
    /// <summary>The only channel with no external system: the Delivery row already written to the database IS the in-app delivery (doc 09 §5 bell/list).</summary>
    public class InAppChannelSender : IChannelSender
    {
        public NotificationChannel Channel => NotificationChannel.InApp;

        public Task<ChannelSendOutcome> SendAsync(Delivery delivery, CancellationToken cancellationToken = default)
            => Task.FromResult(ChannelSendOutcome.Success());
    }
}
