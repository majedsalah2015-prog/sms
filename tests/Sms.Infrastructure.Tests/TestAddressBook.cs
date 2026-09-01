using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sms.Application.Notifications;
using Sms.Domain.Notifications;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// A stand-in <see cref="IRecipientAddressBook"/> for the many tests that publish a
    /// notification while caring about something else entirely.
    /// <para>
    /// Shared rather than repeated as a private nested class in each of the sixteen files
    /// that construct a publisher: those tests are not testing addressing, and a double
    /// copied sixteen times is sixteen places to change when the port grows.
    /// <c>FixedClock</c> and friends stay per-file because each test mutates them; this one
    /// is configured once at construction and never touched again.
    /// </para>
    /// <para>
    /// Empty by default, which is the honest default: a test fixture that has not set up a
    /// parent's mobile number does not have one, and a delivery with no address is exactly
    /// what the product does with such a recipient.
    /// </para>
    /// </summary>
    internal sealed class TestAddressBook : IRecipientAddressBook
    {
        private readonly Dictionary<(int UserId, NotificationChannel Channel), string> _addresses = new();

        /// <summary>Give one user an address on one channel, so a test can prove the publisher snapshots it.</summary>
        public TestAddressBook With(int userId, NotificationChannel channel, string address)
        {
            _addresses[(userId, channel)] = address;
            return this;
        }

        public Task<IReadOnlyDictionary<int, string>> ResolveAsync(
            IReadOnlyCollection<int> userIds,
            NotificationChannel channel,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<int, string> resolved = userIds
                .Where(id => _addresses.ContainsKey((id, channel)))
                .ToDictionary(id => id, id => _addresses[(id, channel)]);

            return Task.FromResult(resolved);
        }
    }
}
