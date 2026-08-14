using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Security
{
    /// <summary>
    /// EF-backed IPermissionService. Loads the current user's active
    /// assignments once per scoped lifetime (request); the session-level
    /// cache with invalidation on role change (T-8) lands with E-011 jobs +
    /// caching infrastructure.
    /// </summary>
    public class PermissionService : IPermissionService
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _currentUser;
        private IReadOnlyCollection<AssignmentSnapshot>? _snapshots;

        public PermissionService(AppDbContext db, ICurrentUser currentUser)
        {
            _db = db;
            _currentUser = currentUser;
        }

        public async Task<bool> HasPermissionAsync(string moduleCode, string screenCode, ActionVerb action, CancellationToken cancellationToken = default)
        {
            var snapshots = await LoadAsync(cancellationToken);
            return PermissionEvaluator.HasPermission(snapshots, moduleCode, screenCode, action);
        }

        public async Task<EffectiveScope?> GetEffectiveScopeAsync(string moduleCode, string screenCode, ActionVerb action, CancellationToken cancellationToken = default)
        {
            var snapshots = await LoadAsync(cancellationToken);
            return PermissionEvaluator.GetEffectiveScope(snapshots, moduleCode, screenCode, action);
        }

        private async Task<IReadOnlyCollection<AssignmentSnapshot>> LoadAsync(CancellationToken cancellationToken)
        {
            if (_snapshots != null)
            {
                return _snapshots;
            }

            // Tenant + soft-active query filters apply automatically: inactive
            // assignments, roles, and other-school rows never surface here.
            var assignments = await _db.RoleAssignments
                .Where(a => a.UserAccountId == _currentUser.UserId)
                .Select(a => new
                {
                    Permissions = a.Role!.Permissions.Select(rp => new
                    {
                        rp.Permission!.ModuleCode,
                        rp.Permission!.ScreenCode,
                        rp.Permission!.Action,
                    }).ToList(),
                    Grants = a.ScopeGrants.Select(g => new { g.Dimension, g.ScopeValueId }).ToList(),
                })
                .ToListAsync(cancellationToken);

            _snapshots = assignments
                .Select(a => new AssignmentSnapshot(
                    a.Permissions.Select(p => new PermissionTriple(p.ModuleCode, p.ScreenCode, p.Action)).ToList(),
                    a.Grants.Select(g => new ScopeGrantValue(g.Dimension, g.ScopeValueId)).ToList()))
                .ToList();

            return _snapshots;
        }
    }
}
