using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ERP2028.Application.Abstractions.Identity;
using Sms.Application.Security;

namespace Sms.Erp.Bridge.Identity
{
    /// <summary>
    /// Points the ERP's <see cref="IUserDirectory"/> at this system's accounts.
    /// The Organization module validates the user behind a branch manager and a
    /// branch assignment through it, and its seeder resolves users at startup —
    /// so without this adapter the module cannot be hosted here at all.
    /// <para>
    /// A pure shape translation: <see cref="UserAccountInfo"/> to
    /// <see cref="UserInfo"/>, same three facts. Nothing is filtered or
    /// enriched, so what Organization sees is what this system's own screens
    /// see, and there is no second answer to "who is user 42".
    /// </para>
    /// </summary>
    public sealed class ErpUserDirectoryAdapter : IUserDirectory
    {
        private readonly IUserAccountDirectory _accounts;

        public ErpUserDirectoryAdapter(IUserAccountDirectory accounts) => _accounts = accounts;

        public async Task<UserInfo?> FindAsync(int userId, CancellationToken cancellationToken = default)
        {
            var account = await _accounts.FindAsync(userId, cancellationToken);
            return account == null ? null : ToUserInfo(account);
        }

        public async Task<IReadOnlyDictionary<int, UserInfo>> GetByIdsAsync(
            IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default)
        {
            var accounts = await _accounts.GetByIdsAsync(userIds, cancellationToken);
            return accounts.ToDictionary(kv => kv.Key, kv => ToUserInfo(kv.Value));
        }

        public async Task<IReadOnlyList<UserInfo>> ListAsync(
            bool activeOnly = true, CancellationToken cancellationToken = default)
        {
            var accounts = await _accounts.ListAsync(activeOnly, cancellationToken);
            return accounts.Select(ToUserInfo).ToList();
        }

        private static UserInfo ToUserInfo(UserAccountInfo account)
            => new UserInfo(account.Id, account.UserName, account.DisplayName, account.IsActive);
    }
}
