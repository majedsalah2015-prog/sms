using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Security;
using Sms.Domain.Audit;
using Sms.Domain.Employees;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Security
{
    /// <summary>
    /// The account lifecycle of doc 06 §8 / Module 36 §8.1 over a real <see cref="AppDbContext"/> —
    /// until this existed the only way to give a new employee a login was an INSERT, and with it went
    /// the password policy, the one-person-one-account rule, and any record that it happened.
    /// <para>
    /// Two things here are not ordinary CRUD, and both are about what cannot be undone from inside
    /// the product. Nobody deactivates the last account able to administer permissions, and nobody
    /// deactivates their own — the first closes the screen that would reopen it, and the second is
    /// the one mistake on this screen a person cannot fix for themselves.
    /// </para>
    /// <para>
    /// Accounts are read past the soft-active filter throughout. This is the one place that has to
    /// see a deactivated account: an administrator cannot reactivate what is invisible, and a user
    /// name held by a retired account is still taken.
    /// </para>
    /// </summary>
    public class UserAccountAdmin : IUserAccountAdmin
    {
        private readonly AppDbContext _db;
        private readonly IAuthenticationService _authentication;
        private readonly IAuditEventWriter _auditEvents;
        private readonly ICurrentUser _currentUser;
        private readonly IClock _clock;

        public UserAccountAdmin(
            AppDbContext db,
            IAuthenticationService authentication,
            IAuditEventWriter auditEvents,
            ICurrentUser currentUser,
            IClock clock)
        {
            _db = db;
            _authentication = authentication;
            _auditEvents = auditEvents;
            _currentUser = currentUser;
            _clock = clock;
        }

        // ------------------------------------------------------------------ the directory

        public async Task<IReadOnlyList<UserAccountRow>> ListAsync(
            UserAccountFilter filter, CancellationToken cancellationToken = default)
        {
            var accounts = await Accounts().AsNoTracking().ToListAsync(cancellationToken);
            if (filter.AccountType is { } type)
            {
                accounts = accounts.Where(a => a.AccountType == type).ToList();
            }

            var rows = await BuildRowsAsync(accounts, cancellationToken);

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                // Matched against the person's name as well as the user name: a colleague is looked
                // for by the name on their door, and "emp-1042" is not it.
                var term = filter.Search.Trim();
                rows = rows.Where(r =>
                        AccountPeople.Contains(r.UserName, term)
                        || AccountPeople.Contains(r.PersonNameAr, term)
                        || AccountPeople.Contains(r.PersonNameEn, term)
                        || AccountPeople.Contains(r.PersonReference, term))
                    .ToList();
            }

            rows = filter.Status switch
            {
                AccountStatusFilter.Active => rows.Where(r => r.IsActive).ToList(),
                AccountStatusFilter.Inactive => rows.Where(r => !r.IsActive).ToList(),
                AccountStatusFilter.Dormant => rows.Where(r => r.IsActive && r.IsDormant).ToList(),
                AccountStatusFilter.LockedOut => rows.Where(r => r.IsLockedOut).ToList(),
                AccountStatusFilter.NeverSignedIn => rows.Where(r => r.LastSignInAtUtc == null).ToList(),
                _ => rows,
            };

            return rows
                .OrderByDescending(r => r.IsActive)
                .ThenBy(r => r.UserName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<UserAccountDetail?> GetAsync(int userAccountId, CancellationToken cancellationToken = default)
        {
            var account = await Accounts().AsNoTracking()
                .SingleOrDefaultAsync(a => a.Id == userAccountId, cancellationToken);
            if (account == null)
            {
                return null;
            }

            var row = (await BuildRowsAsync(new[] { account }, cancellationToken)).Single();

            var roles = await _db.RoleAssignments.AsNoTracking()
                .Where(a => a.UserAccountId == userAccountId)
                .Include(a => a.Role).ThenInclude(r => r!.Permissions).ThenInclude(p => p.Permission)
                .ToListAsync(cancellationToken);

            var sessions = await _db.UserSessions.AsNoTracking()
                .Where(s => s.UserAccountId == userAccountId && s.RevokedAtUtc == null && s.ExpiresAtUtc > _clock.UtcNow)
                .OrderByDescending(s => s.LastActivityAtUtc)
                .ToListAsync(cancellationToken);

            var signIns = await _db.LoginAttempts.AsNoTracking()
                .Where(a => a.UserAccountId == userAccountId)
                .OrderByDescending(a => a.CreatedAtUtc)
                .Take(20)
                .ToListAsync(cancellationToken);

            return new UserAccountDetail(
                row,
                roles.Where(a => a.Role != null)
                    .Select(a => new UserRoleGrant(
                        a.Role!.Id, a.Role.Code, a.Role.Name.NameAr, a.Role.Name.NameEn, Administers(a.Role)))
                    .OrderBy(r => r.Code, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                sessions
                    .Select(s => new AccountSession(
                        s.Id, s.CreatedAtUtc, s.LastActivityAtUtc, s.ExpiresAtUtc, s.IpAddress, s.UserAgent))
                    .ToList(),
                signIns
                    .Select(a => new AccountSignIn(a.CreatedAtUtc, a.Succeeded, a.FailureReason, a.IpAddress))
                    .ToList());
        }

        // ------------------------------------------------------------------ provisioning

        public async Task<IReadOnlyList<PersonWithoutAccount>> ListProvisionableAsync(
            ProvisionableAccountType accountType, string? search = null, CancellationToken cancellationToken = default)
        {
            var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

            // The people lists are read through the ordinary filters, not past them: this is the
            // picker, and what it offers is who may be given an account now. A withdrawn student and
            // a terminated employee are deliberately not on it.
            switch (accountType)
            {
                case ProvisionableAccountType.Staff:
                {
                    var employees = await _db.Employees.AsNoTracking()
                        .Where(e => e.UserAccountId == null && e.Status != EmployeeStatus.Terminated)
                        .ToListAsync(cancellationToken);

                    return employees
                        .Select(e => new PersonWithoutAccount(
                            e.Id,
                            AccountPeople.Join(e.FirstNameAr, e.FatherNameAr, e.FamilyNameAr),
                            AccountPeople.Join(e.FirstNameEn, e.FatherNameEn, e.FamilyNameEn),
                            e.EmployeeNo,
                            UserNameRules.Propose(accountType, e.EmployeeNo)))
                        .Pick(term);
                }

                case ProvisionableAccountType.Parent:
                {
                    var parents = await _db.Parents.AsNoTracking()
                        .Where(p => p.UserAccountId == null)
                        .ToListAsync(cancellationToken);

                    return parents
                        .Select(p => new PersonWithoutAccount(
                            p.Id, p.NameAr, p.NameEn, p.ParentFileNo,
                            UserNameRules.Propose(accountType, p.ParentFileNo)))
                        .Pick(term);
                }

                case ProvisionableAccountType.Student:
                {
                    var students = await _db.Students.AsNoTracking()
                        .Where(s => s.UserAccountId == null)
                        .ToListAsync(cancellationToken);

                    return students
                        .Select(s => new PersonWithoutAccount(
                            s.Id,
                            AccountPeople.Join(s.FirstNameAr, s.FatherNameAr, s.FamilyNameAr),
                            AccountPeople.Join(s.FirstNameEn, s.FatherNameEn, s.FamilyNameEn),
                            s.StudentNo,
                            UserNameRules.Propose(accountType, s.StudentNo)))
                        .Pick(term);
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(accountType), accountType, null);
            }
        }

        public async Task<ProvisionedAccount> ProvisionAsync(
            NewUserAccount definition, CancellationToken cancellationToken = default)
        {
            var userName = UserNameRules.Normalize(definition.UserName);
            if (!UserNameRules.IsWellFormed(userName))
            {
                throw new InvalidUserNameException(definition.UserName ?? string.Empty);
            }

            // Deactivated accounts count. The name still belongs to whoever had it, and reissuing it
            // would make a year of audit entries read as though they were the new holder's.
            var taken = await Accounts().AnyAsync(a => a.UserName == userName, cancellationToken);
            if (taken)
            {
                throw new DuplicateUserNameException(userName);
            }

            var accountType = Map(definition.AccountType);
            await GuardOnePersonOneAccountAsync(definition, accountType, cancellationToken);

            var account = new UserAccount
            {
                UserName = userName,
                AccountType = accountType,
                PersonId = definition.PersonId,
                IsActive = true,
            };

            _db.UserAccounts.Add(account);
            _auditEvents.Log(AuditAction.Create, nameof(UserAccount), businessKey: userName);
            await _db.SaveChangesAsync(cancellationToken);

            await LinkPersonAsync(definition, account.Id, cancellationToken);
            await GrantPortalRoleAsync(account, cancellationToken);

            // The password is minted here rather than taken from the caller: an administrator who
            // chooses it chooses the same one for everybody, and BR-SEC-005 wants a value that is
            // used once and replaced at the first sign-in.
            var temporaryPassword = OneTimePassword.Generate();
            await _authentication.SetTemporaryPasswordAsync(account.Id, temporaryPassword, cancellationToken);

            return new ProvisionedAccount(account.Id, account.UserName, temporaryPassword);
        }

        // ------------------------------------------------------------------ lifecycle

        public async Task DeactivateAsync(int userAccountId, string? reason, CancellationToken cancellationToken = default)
        {
            var account = await LoadAsync(userAccountId, cancellationToken);
            if (!account.IsActive)
            {
                return;
            }

            if (userAccountId == _currentUser.UserId)
            {
                throw new SelfAccountDeactivationException();
            }

            await EnsureAdministrationSurvivesAsync(
                $"Deactivating account {account.UserName}", userAccountId, cancellationToken);

            account.IsActive = false;
            RevokeSessions(await LiveSessionsAsync(userAccountId, cancellationToken), reason);
            _auditEvents.Log(AuditAction.StatusChange, nameof(UserAccount), account.Id, account.UserName, reason);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task ReactivateAsync(int userAccountId, CancellationToken cancellationToken = default)
        {
            var account = await LoadAsync(userAccountId, cancellationToken);
            if (account.IsActive)
            {
                return;
            }

            account.IsActive = true;
            _auditEvents.Log(AuditAction.StatusChange, nameof(UserAccount), account.Id, account.UserName);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<string> ResetPasswordAsync(int userAccountId, CancellationToken cancellationToken = default)
        {
            var account = await LoadAsync(userAccountId, cancellationToken);
            if (!account.IsActive)
            {
                // Resetting the password of a deactivated account only looks like it restored access.
                throw new InactiveAccountException(account.UserName);
            }

            var temporaryPassword = OneTimePassword.Generate();
            _auditEvents.Log(AuditAction.Update, nameof(UserAccount), account.Id, account.UserName, "Password reset");
            await _authentication.SetTemporaryPasswordAsync(account.Id, temporaryPassword, cancellationToken);
            return temporaryPassword;
        }

        public async Task UnlockAsync(int userAccountId, CancellationToken cancellationToken = default)
        {
            var account = await LoadAsync(userAccountId, cancellationToken);
            account.AccessFailedCount = 0;
            account.LockedOutUntilUtc = null;
            _auditEvents.Log(AuditAction.Update, nameof(UserAccount), account.Id, account.UserName, "Lockout cleared");
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task ResetTwoFactorAsync(int userAccountId, CancellationToken cancellationToken = default)
        {
            var account = await LoadAsync(userAccountId, cancellationToken);

            // The secret is discarded rather than kept: a device nobody has is not a second factor.
            var enrollments = await _db.TwoFactorEnrollments
                .Where(e => e.UserAccountId == userAccountId)
                .ToListAsync(cancellationToken);
            _db.TwoFactorEnrollments.RemoveRange(enrollments);

            account.TwoFactorEnabled = false;
            _auditEvents.Log(AuditAction.Update, nameof(UserAccount), account.Id, account.UserName, "Two-factor reset");
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task EndSessionsAsync(int userAccountId, string? reason, CancellationToken cancellationToken = default)
        {
            var account = await LoadAsync(userAccountId, cancellationToken);
            var sessions = await LiveSessionsAsync(userAccountId, cancellationToken);
            if (sessions.Count == 0)
            {
                return;
            }

            RevokeSessions(sessions, reason);
            _auditEvents.Log(AuditAction.Update, nameof(UserAccount), account.Id, account.UserName, reason ?? "Sessions ended");
            await _db.SaveChangesAsync(cancellationToken);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Accounts of this school, active and inactive alike. <c>IgnoreQueryFilters</c> lifts every
        /// filter in the query rather than only the soft-active one, so the school scope it would
        /// otherwise inherit is restated here (ADR-2, BR-GLB-010).
        /// </summary>
        private IQueryable<UserAccount> Accounts()
            => _db.UserAccounts.IgnoreQueryFilters().Where(a => a.SchoolId == _db.CurrentSchoolId);

        private async Task<UserAccount> LoadAsync(int userAccountId, CancellationToken cancellationToken)
            => await Accounts().SingleOrDefaultAsync(a => a.Id == userAccountId, cancellationToken)
               ?? throw new InvalidOperationException($"User account {userAccountId} was not found.");

        private Task<List<UserSession>> LiveSessionsAsync(int userAccountId, CancellationToken cancellationToken)
            => _db.UserSessions
                .Where(s => s.UserAccountId == userAccountId && s.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);

        private void RevokeSessions(IEnumerable<UserSession> sessions, string? reason)
        {
            foreach (var session in sessions)
            {
                session.RevokedAtUtc = _clock.UtcNow;
                session.RevokedReason = reason;
            }
        }

        private async Task<List<UserAccountRow>> BuildRowsAsync(
            IReadOnlyCollection<UserAccount> accounts, CancellationToken cancellationToken)
        {
            if (accounts.Count == 0)
            {
                return new List<UserAccountRow>();
            }

            var ids = accounts.Select(a => a.Id).ToList();
            var now = _clock.UtcNow;

            var roleCounts = await _db.RoleAssignments.AsNoTracking()
                .Where(a => ids.Contains(a.UserAccountId))
                .GroupBy(a => a.UserAccountId)
                .Select(g => new { UserAccountId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserAccountId, x => x.Count, cancellationToken);

            var sessionCounts = await _db.UserSessions.AsNoTracking()
                .Where(s => ids.Contains(s.UserAccountId) && s.RevokedAtUtc == null && s.ExpiresAtUtc > now)
                .GroupBy(s => s.UserAccountId)
                .Select(g => new { UserAccountId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserAccountId, x => x.Count, cancellationToken);

            // Materialized before the Max: aggregates are computed in memory here as everywhere else
            // in this codebase, because the two providers do not agree about them.
            var successes = await _db.LoginAttempts.AsNoTracking()
                .Where(a => a.UserAccountId != null && ids.Contains(a.UserAccountId.Value) && a.Succeeded)
                .Select(a => new { UserAccountId = a.UserAccountId!.Value, a.CreatedAtUtc })
                .ToListAsync(cancellationToken);

            var lastSignIn = successes
                .GroupBy(a => a.UserAccountId)
                .ToDictionary(g => g.Key, g => g.Max(a => a.CreatedAtUtc));

            var people = await AccountPeople.LoadAsync(_db, accounts, cancellationToken);

            return accounts.Select(account =>
            {
                var person = people.Of(account);

                var signedInAt = lastSignIn.TryGetValue(account.Id, out var at) ? at : (DateTime?)null;

                return new UserAccountRow(
                    account.Id,
                    account.UserName,
                    account.AccountType,
                    account.IsActive,
                    person.NameAr,
                    person.NameEn,
                    person.Reference,
                    account.MustChangePassword,
                    account.TwoFactorEnabled,
                    account.LockedOutUntilUtc > now,
                    account.LockedOutUntilUtc,
                    signedInAt,
                    account.CreatedAtUtc,
                    AccountDormancy.DaysSinceUse(signedInAt, account.CreatedAtUtc, now),
                    AccountDormancy.IsDormant(signedInAt, account.CreatedAtUtc, now),
                    roleCounts.TryGetValue(account.Id, out var roles) ? roles : 0,
                    sessionCounts.TryGetValue(account.Id, out var sessions) ? sessions : 0);
            }).ToList();
        }

        /// <summary>
        /// BR-GLB-002 / BR-SYS-001. Both directions are checked: the person's own link, and an
        /// account already pointing at them. They can only disagree if a row was written outside this
        /// service, which is exactly the history this port exists to end.
        /// </summary>
        private async Task GuardOnePersonOneAccountAsync(
            NewUserAccount definition, AccountType accountType, CancellationToken cancellationToken)
        {
            var alreadyLinked = await Accounts()
                .AnyAsync(a => a.AccountType == accountType && a.PersonId == definition.PersonId, cancellationToken);
            if (alreadyLinked)
            {
                throw new PersonAlreadyHasAccountException(definition.AccountType, definition.PersonId);
            }

            var held = definition.AccountType switch
            {
                ProvisionableAccountType.Staff => await _db.Employees.IgnoreQueryFilters()
                    .Where(e => e.SchoolId == _db.CurrentSchoolId && e.Id == definition.PersonId)
                    .Select(e => e.UserAccountId)
                    .SingleOrDefaultAsync(cancellationToken),
                ProvisionableAccountType.Parent => await _db.Parents.IgnoreQueryFilters()
                    .Where(p => p.SchoolId == _db.CurrentSchoolId && p.Id == definition.PersonId)
                    .Select(p => p.UserAccountId)
                    .SingleOrDefaultAsync(cancellationToken),
                ProvisionableAccountType.Student => await _db.Students.IgnoreQueryFilters()
                    .Where(s => s.SchoolId == _db.CurrentSchoolId && s.Id == definition.PersonId)
                    .Select(s => s.UserAccountId)
                    .SingleOrDefaultAsync(cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(definition), definition.AccountType, null),
            };

            if (held != null)
            {
                throw new PersonAlreadyHasAccountException(definition.AccountType, definition.PersonId);
            }
        }

        /// <summary>
        /// Writes the person's side of the link. The account row already carries
        /// <see cref="UserAccount.PersonId"/>; the person carries the account id because that is the
        /// direction every other module already reads it in (<c>Employee.UserAccountId</c>).
        /// </summary>
        private async Task LinkPersonAsync(NewUserAccount definition, int userAccountId, CancellationToken cancellationToken)
        {
            switch (definition.AccountType)
            {
                case ProvisionableAccountType.Staff:
                {
                    var employee = await _db.Employees.IgnoreQueryFilters()
                        .SingleOrDefaultAsync(e => e.SchoolId == _db.CurrentSchoolId && e.Id == definition.PersonId, cancellationToken);
                    if (employee == null)
                    {
                        throw new InvalidOperationException($"Employee {definition.PersonId} was not found.");
                    }

                    employee.UserAccountId = userAccountId;
                    break;
                }

                case ProvisionableAccountType.Parent:
                {
                    var parent = await _db.Parents.IgnoreQueryFilters()
                        .SingleOrDefaultAsync(p => p.SchoolId == _db.CurrentSchoolId && p.Id == definition.PersonId, cancellationToken);
                    if (parent == null)
                    {
                        throw new InvalidOperationException($"Parent {definition.PersonId} was not found.");
                    }

                    parent.UserAccountId = userAccountId;
                    break;
                }

                case ProvisionableAccountType.Student:
                {
                    var student = await _db.Students.IgnoreQueryFilters()
                        .SingleOrDefaultAsync(s => s.SchoolId == _db.CurrentSchoolId && s.Id == definition.PersonId, cancellationToken);
                    if (student == null)
                    {
                        throw new InvalidOperationException($"Student {definition.PersonId} was not found.");
                    }

                    student.UserAccountId = userAccountId;
                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(definition), definition.AccountType, null);
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Gives a portal account the one role that opens the portal (doc 06 §1's account types,
        /// BR-SEC-006). Staff are untouched: which roles a colleague holds is the school's decision,
        /// the screen says so on the next page, and doc 06 §7 keeps that authority separate from
        /// this one on purpose.
        /// <para>
        /// Without this a provisioned parent signed in successfully and then met a bare not-found at
        /// <c>/portal</c> — deny-by-default (BR-GLB-070) refusing an account that held no permission
        /// at all, with nothing on any screen to say why. Nothing is widened by granting it: the
        /// account type was chosen by the same act that created the account, the portal role reaches
        /// only portal screens, and <c>PortalAreaFilter</c> confines the account there regardless
        /// (BR-SEC-010).
        /// </para>
        /// </summary>
        private async Task GrantPortalRoleAsync(UserAccount account, CancellationToken cancellationToken)
        {
            var roleCode = RoleTemplates.ForPortalAccount(account.AccountType);
            if (roleCode == null)
            {
                return;
            }

            // Through the soft-active filter deliberately. A school that retired its parent template
            // has said something, and reviving it as a side effect of creating an account is not this
            // method's call — the account is still created, and the assignment screen can still give
            // it a role by hand.
            var roleId = await _db.Roles
                .Where(r => r.Code == roleCode)
                .Select(r => (int?)r.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (roleId == null)
            {
                return;
            }

            _db.RoleAssignments.Add(new RoleAssignment { UserAccountId = account.Id, RoleId = roleId.Value, IsActive = true });
            await _db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// <see cref="ISecurityAdmin"/>'s refusal arriving from the other direction: a role can be
        /// narrowed until nobody administers permissions, and so can the set of accounts holding it.
        /// The rule is "do not remove the last one", not "there must always be one" — a school where
        /// nobody administers yet can still be bootstrapped.
        /// </summary>
        private async Task EnsureAdministrationSurvivesAsync(
            string what, int userAccountBeingDeactivated, CancellationToken cancellationToken)
        {
            var assignments = await _db.RoleAssignments.AsNoTracking()
                .Include(a => a.Role).ThenInclude(r => r!.Permissions).ThenInclude(p => p.Permission)
                .ToListAsync(cancellationToken);

            var active = new HashSet<int>(
                await Accounts().Where(a => a.IsActive).Select(a => a.Id).ToListAsync(cancellationToken));

            bool Holds(RoleAssignment assignment, bool afterTheChange)
            {
                if (assignment.Role == null || !Administers(assignment.Role))
                {
                    return false;
                }

                if (afterTheChange && assignment.UserAccountId == userAccountBeingDeactivated)
                {
                    return false;
                }

                return active.Contains(assignment.UserAccountId);
            }

            if (!assignments.Any(a => Holds(a, afterTheChange: false)))
            {
                return;
            }

            if (!assignments.Any(a => Holds(a, afterTheChange: true)))
            {
                throw new LastPermissionAdministratorException(what);
            }
        }

        private static bool Administers(Role role) =>
            role.Permissions.Any(p =>
                p.Permission != null
                && p.Permission.ModuleCode == SecurityAdmin.Administration.ModuleCode
                && p.Permission.ScreenCode == SecurityAdmin.Administration.ScreenCode
                && p.Permission.Action == SecurityAdmin.Administration.Action);

        private static AccountType Map(ProvisionableAccountType accountType) => accountType switch
        {
            ProvisionableAccountType.Staff => AccountType.Staff,
            ProvisionableAccountType.Parent => AccountType.Parent,
            ProvisionableAccountType.Student => AccountType.Student,
            _ => throw new ArgumentOutOfRangeException(nameof(accountType), accountType, null),
        };
    }

    internal static class ProvisionablePeople
    {
        /// <summary>How many people a picker offers before it stops being a picker. It is a search box, not a report.</summary>
        private const int Limit = 50;

        /// <summary>
        /// The picker's tail: filter by what was typed, order by the name it will be read under, and
        /// cap it. Applied in memory because the names are composed from four columns and the
        /// proposed user name from a numbering series — neither is a thing to ask the database about.
        /// </summary>
        internal static IReadOnlyList<PersonWithoutAccount> Pick(
            this IEnumerable<PersonWithoutAccount> people, string? term)
            => people
                .Where(p => term == null
                            || p.NameAr.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0
                            || p.NameEn.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0
                            || p.Reference.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(p => p.NameEn, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.Reference, StringComparer.OrdinalIgnoreCase)
                .Take(Limit)
                .ToList();
    }
}
