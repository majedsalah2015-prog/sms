using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sms.Application.Security
{
    /// <summary>
    /// A minimal read surface over <c>sec.UserAccount</c>: validate an id, show
    /// a name, gate on active. Deliberately not a window onto the account —
    /// nothing here exposes credentials, lockout state, or grants, because the
    /// callers are screens that need to render a name and services that need to
    /// reject an id that does not resolve.
    /// <para>
    /// Introduced for the embedded-accounting bridge
    /// (docs/Integration/01-Embedded-Accounting-Plan.md §5): the ERP's
    /// Organization module validates the user behind a branch manager and a
    /// branch assignment through its own <c>IUserDirectory</c>, and that has to
    /// resolve against this system's users rather than a second user store.
    /// The bridge adapts this port; it does not reach into
    /// <c>Sms.Infrastructure</c>, which is why the port exists here rather than
    /// the adapter simply taking <c>AppDbContext</c>.
    /// </para>
    /// <para>
    /// School-scoped like everything else (ADR-2): the ambient tenant filter
    /// applies, so an id from another school does not resolve.
    /// </para>
    /// </summary>
    public interface IUserAccountDirectory
    {
        /// <summary>The account, or <c>null</c> when no active-or-inactive account has this id. Never throws — a caller validating input needs a null it can turn into a field error.</summary>
        Task<UserAccountInfo?> FindAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>Batch resolve. Ids that do not exist are simply absent, so a caller compares counts rather than handling per-id nulls.</summary>
        Task<IReadOnlyDictionary<int, UserAccountInfo>> GetByIdsAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default);

        /// <summary>Accounts ordered by user name — what a picker offers.</summary>
        Task<IReadOnlyList<UserAccountInfo>> ListAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    }

    /// <summary>What a caller outside Security may know about an account.</summary>
    public sealed class UserAccountInfo
    {
        public UserAccountInfo(int id, string userName, string? displayName, bool isActive)
        {
            Id = id;
            UserName = userName;
            DisplayName = displayName;
            IsActive = isActive;
        }

        public int Id { get; }

        public string UserName { get; }

        /// <summary>The person's name where one is linked; <c>null</c> when the account stands alone (a system or service account).</summary>
        public string? DisplayName { get; }

        public bool IsActive { get; }
    }
}
