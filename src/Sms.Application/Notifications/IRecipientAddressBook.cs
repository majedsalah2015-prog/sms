using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Notifications;

namespace Sms.Application.Notifications
{
    /// <summary>
    /// Where a given user is reachable on a given channel.
    /// <para>
    /// <see cref="Delivery"/> names a <c>RecipientUserId</c> and nothing else, which was
    /// enough while every external channel was a stub that discarded its argument. A real
    /// gateway needs an address, and the account row does not hold one: a parent's mobile
    /// lives on <c>Parent</c>, an employee's on <c>Employee</c>, and the account only
    /// knows which of those it belongs to. This port is that join, kept behind an
    /// interface so <see cref="INotificationPublisher"/> — which is Application-layer —
    /// can snapshot the address without reaching into the people modules itself.
    /// </para>
    /// <para>
    /// <b>It is asked once, at publish, not at send.</b> The answer is written onto the
    /// delivery row, so a number changed in March cannot rewrite where February's message
    /// went (BR-NOT-008's snapshot rule, BR-NTF-006's two-year evidence).
    /// </para>
    /// </summary>
    public interface IRecipientAddressBook
    {
        /// <summary>
        /// The address for each requested user on <paramref name="channel"/>, keyed by
        /// user id. A user with nothing usable is <b>absent from the dictionary</b> rather
        /// than present with a null: the publisher records the delivery either way and
        /// the missing address is what routes it to BR-NTF-005's data-quality queue.
        /// <para>
        /// Phone channels return E.164 (see <see cref="PhoneNumberRules"/>); a number the
        /// rules cannot normalise is treated as no number at all, because sending it would
        /// buy a provider rejection instead of a message.
        /// </para>
        /// </summary>
        Task<IReadOnlyDictionary<int, string>> ResolveAsync(
            IReadOnlyCollection<int> userIds,
            NotificationChannel channel,
            CancellationToken cancellationToken = default);
    }
}
