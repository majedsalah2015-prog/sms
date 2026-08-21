using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sms.Application.Security;
using Sms.Domain.Security;

namespace Sms.Web.Navigation
{
    /// <summary>
    /// Which modules the signed-in user can open at least one screen of.
    /// <para>
    /// BR-SEC-010 asks that unauthorized surface disappear rather than error, and
    /// the screen filter answers that with a 404 once you arrive. This answers it
    /// one step earlier, so a cashier is not shown a Timetable link that exists
    /// only to refuse them. Feature toggles already remove modules from the
    /// sidebar for a different reason (BR-SET-006); this is the same idea applied
    /// to the person rather than the deployment.
    /// </para>
    /// <para>
    /// Resolved once per request. <c>PermissionService</c> caches the user's
    /// assignments for its scoped lifetime, so asking about every screen in the
    /// catalogue costs one query, not one per screen.
    /// </para>
    /// </summary>
    public sealed class ModuleVisibility
    {
        private readonly IPermissionService _permissions;
        private HashSet<string>? _visible;

        public ModuleVisibility(IPermissionService permissions)
        {
            _permissions = permissions;
        }

        public async Task<bool> CanSeeAsync(string moduleCode, CancellationToken cancellationToken = default)
            => (await VisibleAsync(cancellationToken)).Contains(moduleCode);

        public Task<bool> CanSeeScreenAsync(string moduleCode, string screenCode, ActionVerb action = ActionVerb.View, CancellationToken cancellationToken = default)
            => _permissions.HasPermissionAsync(moduleCode, screenCode, action, cancellationToken);

        private async Task<HashSet<string>> VisibleAsync(CancellationToken cancellationToken)
        {
            if (_visible != null)
            {
                return _visible;
            }

            var visible = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var moduleCode in ScreenCatalog.Screens.Select(s => s.ModuleCode).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var screen in ScreenCatalog.ForModule(moduleCode))
                {
                    // A module with no viewable screen is not necessarily invisible — some are all
                    // action and no page — so a module whose screens define no View at all stays
                    // shown, and its actions refuse individually.
                    if (!screen.Verbs.Contains(ActionVerb.View))
                    {
                        continue;
                    }

                    if (await _permissions.HasPermissionAsync(moduleCode, screen.ScreenCode, ActionVerb.View, cancellationToken))
                    {
                        visible.Add(moduleCode);
                        break;
                    }
                }
            }

            _visible = visible;
            return _visible;
        }
    }
}
