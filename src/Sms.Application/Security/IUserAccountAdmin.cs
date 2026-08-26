using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Security;

namespace Sms.Application.Security
{
    /// <summary>
    /// Module 36 §8.1 — the user directory and the account lifecycle: who has an account, what state
    /// it is in, and the things an administrator does to one (provision it, deactivate it, put a new
    /// one-time password on it, and clear whatever is blocking a sign-in).
    /// <para>
    /// Until this existed there was no way to create an account at all. The seeders made three, the
    /// role screen handed roles to whatever accounts happened to be there, and a new employee's
    /// login had to be inserted with SQL — which also meant the password policy, the audit trail and
    /// the one-person-one-account rule were whatever the person writing the INSERT remembered.
    /// </para>
    /// <para>
    /// <b>The invariants this port enforces beyond ordinary validation:</b>
    /// </para>
    /// <list type="bullet">
    /// <item>An account exists only against a person (BR-GLB-002, BR-SYS-001). There is no
    /// free-standing login, which is what makes "who is this?" answerable a year later.</item>
    /// <item>No password is ever chosen by an administrator or shown twice. Provisioning and
    /// resetting both mint a one-time password, return it once, and force a change at the next
    /// sign-in (BR-SEC-005).</item>
    /// <item>Nobody deactivates their own account, and no deactivation may remove the last account
    /// able to administer permissions — the same refusal <see cref="ISecurityAdmin"/> makes about
    /// roles, arriving from the other direction.</item>
    /// </list>
    /// <para>
    /// <b>Not here, deliberately:</b> batch provisioning with activation links (doc 06 §8,
    /// BR-SEC-006) needs a delivery channel, and the e-mail/SMS provider is an owner decision still
    /// open in <c>docs/Status</c>; scope grants (<c>sec.ScopeGrant</c>) are written by nothing yet;
    /// and <see cref="AccountType.System"/> integration accounts are key-based rather than
    /// person-linked, which is why <see cref="ProvisionableAccountType"/> cannot express one.
    /// </para>
    /// </summary>
    public interface IUserAccountAdmin
    {
        /// <summary>
        /// The directory. Inactive accounts are included — this is the one screen that has to see
        /// past the soft-active filter, because an administrator cannot reactivate what is invisible.
        /// </summary>
        Task<IReadOnlyList<UserAccountRow>> ListAsync(
            UserAccountFilter filter, CancellationToken cancellationToken = default);

        /// <summary>One account with its roles, its live sessions and its recent sign-ins; null when no account of this school has that id.</summary>
        Task<UserAccountDetail?> GetAsync(int userAccountId, CancellationToken cancellationToken = default);

        /// <summary>
        /// People of this kind who have no account yet — the picker behind "provision an account".
        /// Ordered by name and capped, because it is a search box rather than a report.
        /// </summary>
        Task<IReadOnlyList<PersonWithoutAccount>> ListProvisionableAsync(
            ProvisionableAccountType accountType, string? search = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates the account, links it to the person, and mints its one-time password.
        /// <para>
        /// Throws <see cref="Common.Exceptions.InvalidUserNameException"/> for a name this product
        /// will not accept, <see cref="Common.Exceptions.DuplicateUserNameException"/> when it is
        /// taken — by a deactivated account too, since a retired name is still that person's — and
        /// <see cref="Common.Exceptions.PersonAlreadyHasAccountException"/> for the
        /// one-person-one-account rule (BR-GLB-002).
        /// </para>
        /// </summary>
        Task<ProvisionedAccount> ProvisionAsync(
            NewUserAccount definition, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deactivates the account (BR-GLB-005 — there is no delete) and ends every live session it
        /// holds, so the change takes effect on the next request rather than whenever a cookie
        /// happens to expire.
        /// <para>
        /// Throws <see cref="Common.Exceptions.SelfAccountDeactivationException"/> for one's own
        /// account and <see cref="Common.Exceptions.LastPermissionAdministratorException"/> when it
        /// would leave nobody able to administer permissions.
        /// </para>
        /// </summary>
        Task DeactivateAsync(int userAccountId, string? reason, CancellationToken cancellationToken = default);

        Task ReactivateAsync(int userAccountId, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-SEC-005: mints a new one-time password, forces a change at the next sign-in, and
        /// returns the password <b>once</b>. Nothing stores it and no screen can show it again.
        /// Throws <see cref="Common.Exceptions.InactiveAccountException"/> for a deactivated account.
        /// </summary>
        Task<string> ResetPasswordAsync(int userAccountId, CancellationToken cancellationToken = default);

        /// <summary>BR-SEC-002: clears the failed-attempt count and the timed lockout, without touching the password.</summary>
        Task UnlockAsync(int userAccountId, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-SEC-003: turns two-factor off and discards the enrollment, so the holder of a lost
        /// authenticator can sign in and enroll again. The secret is discarded rather than kept —
        /// a device nobody has is not a second factor.
        /// </summary>
        Task ResetTwoFactorAsync(int userAccountId, CancellationToken cancellationToken = default);

        /// <summary>BR-SEC-004: revokes every live session, signing the account out everywhere on its next request.</summary>
        Task EndSessionsAsync(int userAccountId, string? reason, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// The account kinds an administrator can provision, which is every kind that belongs to a
    /// person. <see cref="AccountType.System"/> is absent on purpose: integration accounts are
    /// key-based and non-interactive (doc 06 §2), so a screen offering one would be offering
    /// something this port cannot correctly make.
    /// </summary>
    public enum ProvisionableAccountType : short
    {
        Staff = 1,
        Parent = 2,
        Student = 3,
    }

    /// <summary>Which slice of the directory to show. The status facets are the queues BR-SEC-022 and BR-SEC-002 create.</summary>
    public enum AccountStatusFilter : short
    {
        All = 1,
        Active = 2,
        Inactive = 3,

        /// <summary>BR-SEC-022 — no sign-in for more than <see cref="AccountDormancy.DormantAfterDays"/> days.</summary>
        Dormant = 4,

        /// <summary>BR-SEC-002 — inside a timed lockout right now.</summary>
        LockedOut = 5,

        /// <summary>Provisioned and never used — the one-time password was very likely never collected.</summary>
        NeverSignedIn = 6,
    }

    public sealed class UserAccountFilter
    {
        /// <summary>Matches the user name or either form of the person's name.</summary>
        public string? Search { get; init; }

        public AccountType? AccountType { get; init; }

        public AccountStatusFilter Status { get; init; } = AccountStatusFilter.All;
    }

    /// <summary>
    /// One row of the directory. The person fields are null for an account nothing points at — the
    /// three seeded ones, and any account whose person was linked before this port existed.
    /// </summary>
    public sealed record UserAccountRow(
        int Id,
        string UserName,
        AccountType AccountType,
        bool IsActive,
        string? PersonNameAr,
        string? PersonNameEn,
        string? PersonReference,
        bool MustChangePassword,
        bool TwoFactorEnabled,
        bool IsLockedOut,
        DateTime? LockedOutUntilUtc,
        DateTime? LastSignInAtUtc,
        DateTime ProvisionedAtUtc,
        int DaysSinceUse,
        bool IsDormant,
        int RoleCount,
        int LiveSessionCount);

    public sealed record UserAccountDetail(
        UserAccountRow Account,
        IReadOnlyList<UserRoleGrant> Roles,
        IReadOnlyList<AccountSession> Sessions,
        IReadOnlyList<AccountSignIn> RecentSignIns);

    /// <summary>A live session (BR-SEC-004) — what "signed in on two machines" looks like from here.</summary>
    public sealed record AccountSession(
        int Id, DateTime StartedAtUtc, DateTime LastActivityAtUtc, DateTime ExpiresAtUtc,
        string? IpAddress, string? UserAgent);

    /// <summary>One row of sec.LoginAttempt, successes and failures alike — the first thing to read when somebody says they cannot get in.</summary>
    public sealed record AccountSignIn(DateTime AtUtc, bool Succeeded, string? FailureReason, string? IpAddress);

    /// <summary>A person with no account yet, and the user name this product would give them.</summary>
    public sealed record PersonWithoutAccount(
        int PersonId, string NameAr, string NameEn, string Reference, string SuggestedUserName);

    public sealed record NewUserAccount(ProvisionableAccountType AccountType, int PersonId, string UserName);

    /// <summary>
    /// The one and only time the password is readable. It is returned rather than stored so the
    /// screen can show it once; nothing persists it, and BR-SEC-005 forces it to be replaced at the
    /// first sign-in anyway.
    /// </summary>
    public sealed record ProvisionedAccount(int UserAccountId, string UserName, string TemporaryPassword);
}
