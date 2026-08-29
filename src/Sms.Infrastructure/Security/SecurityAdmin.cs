using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Security;
using Sms.Domain.Common;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Security
{
    /// <summary>
    /// Module 36's role designer (doc 06 §4) — the screen <c>RoleTemplateSeedContributor</c> named as
    /// deferred, and until now the reason a school could only change permissions with SQL.
    /// <para>
    /// Roles, assignments and grants are all tenant-scoped and soft-deleted, so the query filters do
    /// the scoping; <c>sec.Permission</c> is product data with no <c>SchoolId</c>, which is why the
    /// catalogue below is read unfiltered and only the <c>RolePermission</c> rows that point at it
    /// belong to a school.
    /// </para>
    /// </summary>
    public class SecurityAdmin : ISecurityAdmin
    {
        /// <summary>
        /// The permission that can reach every other permission. Every refusal in this class is about
        /// keeping at least one active account holding it.
        /// </summary>
        public static readonly PermissionKey Administration = new(
            ScreenCatalog.Modules.SystemAdministration,
            ScreenCatalog.SystemAdministration.Roles,
            ActionVerb.Configure);

        private readonly AppDbContext _db;

        public SecurityAdmin(AppDbContext db)
        {
            _db = db;
        }

        // ------------------------------------------------------------------ roles

        public async Task<IReadOnlyList<RoleSummary>> ListRolesAsync(
            bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            // IgnoreQueryFilters only for the inactive case: the soft-active filter is what makes
            // "active roles" the default everywhere else, and this screen is the one place that has
            // to be able to look past it — an administrator cannot reactivate what they cannot see.
            var roles = await (includeInactive ? _db.Roles.IgnoreQueryFilters().Where(r => r.SchoolId == SchoolId()) : _db.Roles)
                .AsNoTracking()
                .Include(r => r.Permissions).ThenInclude(p => p.Permission)
                .ToListAsync(cancellationToken);

            var holders = await _db.RoleAssignments.AsNoTracking()
                .GroupBy(a => a.RoleId)
                .Select(g => new { RoleId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);

            return roles
                .Select(r => Summarize(r, holders.TryGetValue(r.Id, out var n) ? n : 0))
                .OrderByDescending(r => r.IsActive)
                .ThenBy(r => r.Code, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<RoleDetail> GetRoleAsync(int roleId, CancellationToken cancellationToken = default)
        {
            var role = await LoadRoleAsync(roleId, tracking: false, cancellationToken);

            var granted = new HashSet<(string, string, ActionVerb)>(
                role.Permissions
                    .Where(p => p.Permission != null)
                    .Select(p => (p.Permission!.ModuleCode, p.Permission.ScreenCode, p.Permission.Action)));

            // The catalogue, not the sec.Permission table, decides what the designer offers: a screen
            // added to ScreenCatalog is grantable on the next deploy, whether or not the seeder has
            // catalogued it yet. SetRolePermissionsAsync creates the row it needs.
            var screens = ScreenCatalog.Screens
                .Select(s => new RoleScreenGrants(
                    s.ModuleCode, s.ScreenCode, s.TitleEn, s.TitleAr,
                    s.Verbs,
                    s.Verbs.Where(v => granted.Contains((s.ModuleCode, s.ScreenCode, v))).ToList()))
                .ToList();

            var holderCount = await _db.RoleAssignments.CountAsync(a => a.RoleId == roleId, cancellationToken);
            return new RoleDetail(Summarize(role, holderCount), screens);
        }

        public async Task<Role> CreateRoleAsync(RoleDefinition definition, CancellationToken cancellationToken = default)
        {
            var code = Normalize(definition.Code);
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("A role needs a code.", nameof(definition));
            }

            var clash = await _db.Roles.IgnoreQueryFilters()
                .AnyAsync(r => r.SchoolId == SchoolId() && r.Code == code, cancellationToken);
            if (clash)
            {
                throw new DuplicateRoleCodeException(code);
            }

            var role = new Role
            {
                Code = code,
                Name = new LocalizedName(definition.NameAr, definition.NameEn),
                RequireTwoFactor = definition.RequireTwoFactor,
                EnforceSingleSession = definition.EnforceSingleSession,
                IsActive = true,
            };

            _db.Roles.Add(role);
            await _db.SaveChangesAsync(cancellationToken);
            return role;
        }

        public async Task UpdateRoleAsync(int roleId, RoleDefinition definition, CancellationToken cancellationToken = default)
        {
            var role = await LoadRoleAsync(roleId, tracking: true, cancellationToken);

            // Code is deliberately not updatable: the seeder, the ERP permission bridge and every
            // existing grant key on it, so renaming one here would silently orphan all three.
            role.Name = new LocalizedName(definition.NameAr, definition.NameEn);
            role.RequireTwoFactor = definition.RequireTwoFactor;
            role.EnforceSingleSession = definition.EnforceSingleSession;

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task DeactivateRoleAsync(int roleId, CancellationToken cancellationToken = default)
        {
            var role = await LoadRoleAsync(roleId, tracking: true, cancellationToken);
            if (!role.IsActive)
            {
                return;
            }

            await EnsureAdministrationSurvivesAsync(
                $"Deactivating role '{role.Code}'",
                excludeRoleId: roleId,
                cancellationToken: cancellationToken);

            role.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task ReactivateRoleAsync(int roleId, CancellationToken cancellationToken = default)
        {
            var role = await _db.Roles.IgnoreQueryFilters()
                .SingleOrDefaultAsync(r => r.Id == roleId && r.SchoolId == SchoolId(), cancellationToken)
                ?? throw new InvalidOperationException($"Role {roleId} was not found.");

            role.IsActive = true;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // ------------------------------------------------------------------ grants

        public async Task SetRolePermissionsAsync(
            int roleId, IReadOnlyCollection<PermissionKey> granted, CancellationToken cancellationToken = default)
        {
            var role = await LoadRoleAsync(roleId, tracking: true, cancellationToken);

            var wanted = new HashSet<(string Module, string Screen, ActionVerb Action)>(
                granted.Select(g => (g.ModuleCode, g.ScreenCode, g.Action)));

            foreach (var key in wanted)
            {
                if (!ScreenCatalog.Defines(key.Module, key.Screen, key.Action))
                {
                    throw new UncataloguedPermissionException(key.Module, key.Screen, key.Action);
                }
            }

            await EnsureAdministrationSurvivesAsync(
                $"Removing permission administration from role '{role.Code}'",
                excludeRoleId: roleId,
                roleWouldStillAdminister: wanted.Contains((Administration.ModuleCode, Administration.ScreenCode, Administration.Action)),
                cancellationToken: cancellationToken);

            var catalogue = await ResolveCatalogueAsync(wanted, cancellationToken);

            var current = role.Permissions.Where(p => p.Permission != null).ToList();
            foreach (var existing in current)
            {
                var key = (existing.Permission!.ModuleCode, existing.Permission.ScreenCode, existing.Permission.Action);
                if (!wanted.Contains(key))
                {
                    _db.RolePermissions.Remove(existing);
                }
            }

            var held = new HashSet<int>(current.Select(p => p.PermissionId));
            foreach (var key in wanted)
            {
                var permissionId = catalogue[key];
                if (!held.Contains(permissionId))
                {
                    _db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        // ------------------------------------------------------------------ assignments

        /// <summary>
        /// Who holds which role. Each row carries the person behind the account, and the search
        /// matches them: an administrator looking for a colleague types the name on their door or
        /// the number on their file, not <c>emp-1042</c>.
        /// <para>
        /// The search runs in memory because it spans four tables — <c>sec.UserAccount</c> and the
        /// three people registers it points into — and no single SQL predicate reaches across them.
        /// The role assignments are then loaded for the accounts that matched, so typing narrows the
        /// expensive half of the query rather than only the cheap one.
        /// </para>
        /// <para>
        /// <paramref name="includeInactive"/> lifts the soft-active filter, and with it every other
        /// filter on the query — so the school scope it would otherwise inherit is restated here
        /// (ADR-2, BR-GLB-010), exactly as <c>UserAccountAdmin.Accounts()</c> does.
        /// </para>
        /// </summary>
        public async Task<IReadOnlyList<UserRoleSummary>> ListUserRolesAsync(
            string? search = null, bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            var accounts = includeInactive
                ? _db.UserAccounts.IgnoreQueryFilters().Where(u => u.SchoolId == _db.CurrentSchoolId)
                : _db.UserAccounts;

            var users = await accounts.AsNoTracking()
                .OrderBy(u => u.UserName)
                .ToListAsync(cancellationToken);

            var people = await AccountPeople.LoadAsync(_db, users, cancellationToken);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                users = users
                    .Where(u =>
                    {
                        var person = people.Of(u);
                        return AccountPeople.Contains(u.UserName, term)
                               || AccountPeople.Contains(person.NameAr, term)
                               || AccountPeople.Contains(person.NameEn, term)
                               || AccountPeople.Contains(person.Reference, term);
                    })
                    .ToList();
            }

            var userIds = users.Select(u => u.Id).ToList();

            var assignments = await _db.RoleAssignments.AsNoTracking()
                .Where(a => userIds.Contains(a.UserAccountId))
                .Include(a => a.Role).ThenInclude(r => r!.Permissions).ThenInclude(p => p.Permission)
                .ToListAsync(cancellationToken);

            return users
                .Select(u =>
                {
                    var person = people.Of(u);
                    return new UserRoleSummary(
                        u.Id, u.UserName, u.AccountType, u.IsActive,
                        person.NameAr, person.NameEn, person.Reference,
                        assignments
                            .Where(a => a.UserAccountId == u.Id && a.Role != null)
                            .Select(a => new UserRoleGrant(
                                a.Role!.Id, a.Role.Code, a.Role.Name.NameAr, a.Role.Name.NameEn, Administers(a.Role)))
                            .OrderBy(r => r.Code, StringComparer.OrdinalIgnoreCase)
                            .ToList());
                })
                .ToList();
        }

        public async Task<RoleAssignment> AssignRoleAsync(
            int userAccountId, int roleId, CancellationToken cancellationToken = default)
        {
            var user = await _db.UserAccounts.SingleOrDefaultAsync(u => u.Id == userAccountId, cancellationToken)
                ?? throw new InvalidOperationException($"User account {userAccountId} was not found.");
            _ = await LoadRoleAsync(roleId, tracking: false, cancellationToken);

            // Past the soft-active filter: a revoked assignment is still a row, and the unique
            // (user, role) pair means re-granting has to revive it rather than insert a second one.
            var existing = await _db.RoleAssignments.IgnoreQueryFilters()
                .SingleOrDefaultAsync(
                    a => a.SchoolId == SchoolId() && a.UserAccountId == userAccountId && a.RoleId == roleId,
                    cancellationToken);

            if (existing != null)
            {
                existing.IsActive = true;
                await _db.SaveChangesAsync(cancellationToken);
                return existing;
            }

            var assignment = new RoleAssignment { UserAccountId = user.Id, RoleId = roleId, IsActive = true };
            _db.RoleAssignments.Add(assignment);
            await _db.SaveChangesAsync(cancellationToken);
            return assignment;
        }

        public async Task RevokeRoleAsync(int userAccountId, int roleId, CancellationToken cancellationToken = default)
        {
            var assignment = await _db.RoleAssignments
                .SingleOrDefaultAsync(a => a.UserAccountId == userAccountId && a.RoleId == roleId, cancellationToken);
            if (assignment == null)
            {
                return;
            }

            await EnsureAdministrationSurvivesAsync(
                "Revoking this role",
                excludeAssignment: (userAccountId, roleId),
                cancellationToken: cancellationToken);

            assignment.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // ------------------------------------------------------------------ the invariant

        /// <summary>
        /// Refuses a change that would <b>take away</b> the last active account holding
        /// <see cref="Administration"/>. Checked against the state the change <i>would</i> produce
        /// rather than after saving it, because there is no undo: by the time the row is written, the
        /// screen that could put it back is unreachable.
        /// <para>
        /// The rule is "do not remove the last one", not "there must always be one". Where nobody
        /// administers today — a database before the seeder has run, or a school whose administration
        /// arrives through some other route — every edit would otherwise be refused, including the
        /// one that creates the first administrator. Refusing to let a system be bootstrapped is the
        /// same lockout wearing the opposite sign.
        /// </para>
        /// </summary>
        private async Task EnsureAdministrationSurvivesAsync(
            string what,
            int? excludeRoleId = null,
            bool roleWouldStillAdminister = false,
            (int UserAccountId, int RoleId)? excludeAssignment = null,
            CancellationToken cancellationToken = default)
        {
            var assignments = await _db.RoleAssignments.AsNoTracking()
                .Include(a => a.Role).ThenInclude(r => r!.Permissions).ThenInclude(p => p.Permission)
                .ToListAsync(cancellationToken);

            var active = new HashSet<int>(
                await _db.UserAccounts.AsNoTracking().Select(u => u.Id).ToListAsync(cancellationToken));

            bool Holds(RoleAssignment a, bool afterTheChange)
            {
                if (a.Role == null || !active.Contains(a.UserAccountId))
                {
                    return false;
                }

                if (!afterTheChange)
                {
                    return Administers(a.Role);
                }

                if (excludeAssignment is { } skip
                    && a.UserAccountId == skip.UserAccountId && a.RoleId == skip.RoleId)
                {
                    return false;
                }

                // The role being edited answers from the proposed grant set, not the stored one.
                return a.RoleId == excludeRoleId ? roleWouldStillAdminister : Administers(a.Role);
            }

            var anyBefore = assignments.Any(a => Holds(a, afterTheChange: false));
            if (!anyBefore)
            {
                return;
            }

            if (!assignments.Any(a => Holds(a, afterTheChange: true)))
            {
                throw new LastPermissionAdministratorException(what);
            }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Codes are upper-cased and trimmed. The seeded templates are upper-case, the ERP bridge
        /// looks SYSADMIN up by exact code, and a role typed as "sysadmin" that silently became a
        /// second, empty role beside it is a support call nobody would diagnose quickly.
        /// </summary>
        private static string Normalize(string code) => (code ?? string.Empty).Trim().ToUpperInvariant();

        private static bool Administers(Role role) =>
            role.Permissions.Any(p =>
                p.Permission != null
                && p.Permission.ModuleCode == Administration.ModuleCode
                && p.Permission.ScreenCode == Administration.ScreenCode
                && p.Permission.Action == Administration.Action);

        private static RoleSummary Summarize(Role role, int holderCount) => new(
            role.Id, role.Code, role.Name.NameAr, role.Name.NameEn, role.IsActive,
            role.RequireTwoFactor, role.EnforceSingleSession,
            role.Permissions.Count, holderCount, Administers(role));

        private async Task<Role> LoadRoleAsync(int roleId, bool tracking, CancellationToken cancellationToken)
        {
            var query = _db.Roles.IgnoreQueryFilters().Where(r => r.SchoolId == SchoolId());
            if (!tracking)
            {
                query = query.AsNoTracking();
            }

            return await query
                       .Include(r => r.Permissions).ThenInclude(p => p.Permission)
                       .SingleOrDefaultAsync(r => r.Id == roleId, cancellationToken)
                   ?? throw new InvalidOperationException($"Role {roleId} was not found.");
        }

        /// <summary>
        /// The <c>sec.Permission</c> row for each wanted triple, creating any the seeder has not
        /// catalogued yet. Permission rows are product data — creating one is recording that the
        /// catalogue defines it, which was checked above, not widening anybody's access.
        /// </summary>
        private async Task<Dictionary<(string, string, ActionVerb), int>> ResolveCatalogueAsync(
            IReadOnlyCollection<(string Module, string Screen, ActionVerb Action)> wanted, CancellationToken cancellationToken)
        {
            var modules = wanted.Select(w => w.Module).Distinct().ToList();
            var rows = await _db.Permissions
                .Where(p => modules.Contains(p.ModuleCode))
                .ToListAsync(cancellationToken);

            var byKey = rows.ToDictionary(p => (p.ModuleCode, p.ScreenCode, p.Action), p => p);

            var added = false;
            foreach (var key in wanted.Where(w => !byKey.ContainsKey((w.Module, w.Screen, w.Action))))
            {
                var permission = new Permission { ModuleCode = key.Module, ScreenCode = key.Screen, Action = key.Action };
                _db.Permissions.Add(permission);
                byKey[(key.Module, key.Screen, key.Action)] = permission;
                added = true;
            }

            if (added)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }

            return byKey.ToDictionary(kv => kv.Key, kv => kv.Value.Id);
        }

        private int SchoolId() => _db.CurrentSchoolId;
    }
}
