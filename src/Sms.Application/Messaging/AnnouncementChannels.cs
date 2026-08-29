using System.Collections.Generic;
using System.Linq;
using Sms.Domain.Notifications;

namespace Sms.Application.Messaging
{
    /// <summary>
    /// Packs the channel picker's ticked boxes into <c>Announcement.ChannelMask</c> and
    /// reads them back.
    /// <para>
    /// A mask rather than a child table because the set is four values wide and never
    /// grows at runtime, and because an announcement's channels are read on every row of
    /// the list — a join for four bits is a join per row for nothing.
    /// </para>
    /// <para>
    /// The bit is <c>1 &lt;&lt; (value - 1)</c>, not the enum value itself: the enum starts
    /// at 1 by the project's SMALLINT convention, so using it raw would make InApp(1) and
    /// Email(2) overlap on the same two bits as Sms(3).
    /// </para>
    /// </summary>
    public static class AnnouncementChannels
    {
        public static int ToMask(IEnumerable<NotificationChannel>? channels)
            => channels == null ? 0 : channels.Distinct().Aggregate(0, (mask, channel) => mask | Bit(channel));

        public static IReadOnlyList<NotificationChannel> FromMask(int mask)
            => NotificationEventCatalogChannels.Where(c => Includes(mask, c)).ToList();

        public static bool Includes(int mask, NotificationChannel channel) => (mask & Bit(channel)) != 0;

        /// <summary>
        /// The channels that cost money to send on — what the compose screen estimates and
        /// what BR-NTF-004's budget counts. In-app and email are not metered here: the first
        /// is a database row, the second is billed by mailbox rather than by message.
        /// </summary>
        public static IReadOnlyList<NotificationChannel> Costed { get; } =
            new[] { NotificationChannel.Sms, NotificationChannel.WhatsApp };

        public static bool IsCosted(NotificationChannel channel) => Costed.Contains(channel);

        /// <summary>How many metered messages sending to <paramref name="audienceSize"/> people on this mask would buy.</summary>
        public static int CostedMessageCount(int mask, int audienceSize)
            => Costed.Count(c => Includes(mask, c)) * audienceSize;

        private static int Bit(NotificationChannel channel) => 1 << ((int)channel - 1);

        private static readonly NotificationChannel[] NotificationEventCatalogChannels =
        {
            NotificationChannel.InApp,
            NotificationChannel.Email,
            NotificationChannel.Sms,
            NotificationChannel.WhatsApp,
        };
    }
}
