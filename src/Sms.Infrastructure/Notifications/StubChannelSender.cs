using System.Threading;
using System.Threading.Tasks;
using Sms.Application.Notifications;
using Sms.Domain.Notifications;

namespace Sms.Infrastructure.Notifications
{
    /// <summary>
    /// Email/SMS/WhatsApp all land here for now (WBS E-007: "adapters
    /// stubbed") — no live provider has been selected (doc 09 §9 Q1: WhatsApp
    /// availability is an open question; SMTP/SMS gateway choice isn't a
    /// toolchain decision yet either, doc 02). Registered once per external
    /// channel so the dispatch loop, retry policy, and budget counters are
    /// fully exercised end to end; swapping in a real transport per channel
    /// is a drop-in IChannelSender replacement once a provider is chosen.
    /// </summary>
    public class StubChannelSender : IChannelSender
    {
        public StubChannelSender(NotificationChannel channel)
        {
            Channel = channel;
        }

        public NotificationChannel Channel { get; }

        public Task<ChannelSendOutcome> SendAsync(Delivery delivery, CancellationToken cancellationToken = default)
            => Task.FromResult(ChannelSendOutcome.Success($"stub:{Channel}"));
    }
}
