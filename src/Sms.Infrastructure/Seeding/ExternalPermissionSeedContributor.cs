using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Security;
using Sms.Application.Seeding;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Seeding
{
    /// <summary>
    /// Catalogues the permissions of every hosted subsystem into
    /// <c>sec.Permission</c> and grants them to the roles the catalog names
    /// (<see cref="IExternalPermissionCatalog"/>).
    /// <para>
    /// A foreign permission name becomes the <c>ScreenCode</c> verbatim under the
    /// catalog's reserved <c>ModuleCode</c>, with <see cref="ActionVerb.View"/> as
    /// the verb — the name already carries its own verb
    /// (<c>Accounting.JournalEntries.Post</c>), and re-encoding it in the triple
    /// would give two places to disagree about what a permission means.
    /// </para>
    /// <para>
    /// Idempotent twice over: a permission already catalogued is left alone, and a
    /// grant is only created where none exists — so an administrator who revokes a
    /// grant does not find it restored on the next start. That is the difference
    /// between seeding a default and enforcing one.
    /// </para>
    /// <para>
    /// This runs after the role templates (Order 20) because the grants resolve
    /// roles by code; a deployment hosting no subsystem resolves no catalog and
    /// this contributor does nothing.
    /// </para>
    /// </summary>
    public class ExternalPermissionSeedContributor : ISeedContributor
    {
        private readonly AppDbContext _db;
        private readonly IEnumerable<IExternalPermissionCatalog> _catalogs;

        public ExternalPermissionSeedContributor(AppDbContext db, IEnumerable<IExternalPermissionCatalog> catalogs)
        {
            _db = db;
            _catalogs = catalogs;
        }

        public string Name => "Hosted-subsystem permissions (doc 06 §4 + Integration/01 §5.1)";

        // After role templates (20) and before the admin account (25), so the
        // SYSADMIN role already exists and carries these grants when it is assigned.
        public int Order => 22;

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            foreach (var catalog in _catalogs)
            {
                await SeedCatalogAsync(catalog, cancellationToken);
            }
        }

        private async Task SeedCatalogAsync(IExternalPermissionCatalog catalog, CancellationToken cancellationToken)
        {
            var existing = await _db.Permissions
                .Where(p => p.ModuleCode == catalog.ModuleCode && p.Action == ActionVerb.View)
                .ToDictionaryAsync(p => p.ScreenCode, cancellationToken);

            foreach (var name in catalog.PermissionNames.Distinct())
            {
                if (existing.ContainsKey(name))
                {
                    continue;
                }

                var permission = new Permission { ModuleCode = catalog.ModuleCode, ScreenCode = name, Action = ActionVerb.View };
                _db.Permissions.Add(permission);
                existing[name] = permission;
            }

            await _db.SaveChangesAsync(cancellationToken);

            foreach (var roleCode in catalog.DefaultGrantRoleCodes)
            {
                var role = await _db.Roles.SingleOrDefaultAsync(r => r.Code == roleCode, cancellationToken);
                if (role == null)
                {
                    continue;
                }

                var alreadyGranted = await _db.RolePermissions
                    .Where(rp => rp.RoleId == role.Id)
                    .Select(rp => rp.PermissionId)
                    .ToListAsync(cancellationToken);
                var granted = new HashSet<int>(alreadyGranted);

                foreach (var permission in existing.Values.Where(p => !granted.Contains(p.Id)))
                {
                    _db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
                }

                await _db.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
