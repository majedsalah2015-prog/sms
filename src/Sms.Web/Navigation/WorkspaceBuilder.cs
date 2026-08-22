using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Sms.Application.Setup;

namespace Sms.Web.Navigation
{
    /// <summary>
    /// Turns <see cref="WorkspaceCatalog"/> into the departments <b>this</b> user sees, on <b>this</b>
    /// deployment.
    /// <para>
    /// Two filters, for two different reasons, the same pair the sidebar already applies: a feature
    /// toggle that is off removes a module from every deployment (BR-SET-006), and a permission this
    /// person does not hold removes the screen from them (BR-SEC-010). A department left with nothing
    /// disappears rather than opening onto an empty page — being shown a Finance tile that leads
    /// nowhere is worse than not being shown one.
    /// </para>
    /// </summary>
    public sealed class WorkspaceBuilder
    {
        private readonly ModuleVisibility _visibility;
        private readonly ISystemSetupAdmin _setup;
        private readonly ErpNavigationSource _erp;

        public WorkspaceBuilder(ModuleVisibility visibility, ISystemSetupAdmin setup, ErpNavigationSource erp)
        {
            _visibility = visibility;
            _setup = setup;
            _erp = erp;
        }

        /// <summary>
        /// A department as one user sees it: the screens left after filtering, plus — for the finance
        /// department — the embedded ERP's groups, which arrive already filtered by the ERP's own
        /// permission check.
        /// </summary>
        public sealed record VisibleWorkspace(
            WorkspaceCatalog.WorkspaceInfo Info,
            IReadOnlyList<WorkspaceCatalog.WorkspaceLink> Links,
            IReadOnlyList<NavItem> ErpGroups)
        {
            /// <summary>How many screens are behind this tile — what the launcher counts.</summary>
            public int ScreenCount => Links.Count + ErpGroups.Sum(g => g.Items.Count);

            /// <summary>
            /// True when the department is one screen and nothing else, so its tile opens that screen
            /// instead of a page holding a single card. Today that is the cover rota.
            /// </summary>
            public bool IsSingleScreen => Links.Count == 1 && ErpGroups.Count == 0;
        }

        /// <summary>Every department this user can open at least one screen of, in catalogue order.</summary>
        public async Task<IReadOnlyList<VisibleWorkspace>> BuildAllAsync(
            ClaimsPrincipal user, CancellationToken cancellationToken = default)
        {
            var featureStates = await _setup.GetFeatureStatesAsync(cancellationToken);
            var erpGroups = _erp.BuildGroupsFor(user);

            var result = new List<VisibleWorkspace>();
            foreach (var workspace in WorkspaceCatalog.Workspaces)
            {
                var built = await BuildAsync(workspace, featureStates, erpGroups, cancellationToken);
                if (built.ScreenCount > 0)
                {
                    result.Add(built);
                }
            }

            return result;
        }

        /// <summary>
        /// One department, or <c>null</c> when this user may open nothing in it. Null rather than an
        /// empty department so the caller answers 404 — the same answer every other screen gives to a
        /// user who may not have it, rather than a page that says "you have no access here" and
        /// thereby confirms what is behind it.
        /// </summary>
        public async Task<VisibleWorkspace?> BuildAsync(
            string key, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        {
            var workspace = WorkspaceCatalog.Find(key);
            if (workspace == null)
            {
                return null;
            }

            var featureStates = await _setup.GetFeatureStatesAsync(cancellationToken);
            var built = await BuildAsync(workspace, featureStates, _erp.BuildGroupsFor(user), cancellationToken);
            return built.ScreenCount > 0 ? built : null;
        }

        private async Task<VisibleWorkspace> BuildAsync(
            WorkspaceCatalog.WorkspaceInfo workspace,
            IReadOnlyDictionary<string, bool> featureStates,
            IReadOnlyList<NavItem> erpGroups,
            CancellationToken cancellationToken)
        {
            var links = new List<WorkspaceCatalog.WorkspaceLink>();
            foreach (var link in workspace.Links)
            {
                if (FeatureCatalog.ForModule(link.ModuleCode) is { } feature
                    && featureStates.TryGetValue(feature.Code, out var on) && !on)
                {
                    continue;
                }

                if (await _visibility.CanSeeScreenAsync(link.ModuleCode, link.ScreenCode, cancellationToken: cancellationToken))
                {
                    links.Add(link);
                }
            }

            return new VisibleWorkspace(
                workspace,
                links,
                workspace.Accounting ? erpGroups : new List<NavItem>());
        }
    }
}
