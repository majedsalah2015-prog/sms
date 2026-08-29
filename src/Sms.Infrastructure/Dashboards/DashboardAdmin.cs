using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Dashboards;
using Sms.Application.Security;
using Sms.Domain.Dashboards;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Dashboards
{
    /// <summary>Standalone admin operations — save themselves, no larger transaction to ride.</summary>
    public class DashboardAdmin : IDashboardAdmin
    {
        private readonly AppDbContext _db;

        public DashboardAdmin(AppDbContext db)
        {
            _db = db;
        }

        public async Task<WidgetDefinition> DefineWidgetAsync(
            string code, string owningModuleCode, string titleAr, string titleEn, int requiredPermissionId,
            WidgetRefreshClass refreshClass, string drillTargetCode, bool isPortalEligible, CancellationToken cancellationToken = default)
        {
            var widget = new WidgetDefinition
            {
                Code = code, OwningModuleCode = owningModuleCode, TitleAr = titleAr, TitleEn = titleEn,
                RequiredPermissionId = requiredPermissionId, RefreshClass = refreshClass, DrillTargetCode = drillTargetCode,
                IsPortalEligible = isPortalEligible,
            };
            _db.WidgetDefinitions.Add(widget);
            await _db.SaveChangesAsync(cancellationToken);
            return widget;
        }

        public async Task UpdateWidgetAsync(
            int widgetDefinitionId, string owningModuleCode, string titleAr, string titleEn, int requiredPermissionId,
            WidgetRefreshClass refreshClass, string drillTargetCode, bool isPortalEligible, CancellationToken cancellationToken = default)
        {
            // IgnoreQueryFilters: a retired widget is still editable — fixing its permission is
            // exactly what someone does before putting it back on a dashboard.
            var widget = await _db.WidgetDefinitions.IgnoreQueryFilters()
                .SingleOrDefaultAsync(w => w.Id == widgetDefinitionId, cancellationToken)
                ?? throw new WidgetDefinitionNotFoundException(widgetDefinitionId);

            widget.OwningModuleCode = owningModuleCode;
            widget.TitleAr = titleAr;
            widget.TitleEn = titleEn;
            widget.RequiredPermissionId = requiredPermissionId;
            widget.RefreshClass = refreshClass;
            widget.DrillTargetCode = drillTargetCode;
            widget.IsPortalEligible = isPortalEligible;

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task SetWidgetActiveAsync(int widgetDefinitionId, bool isActive, CancellationToken cancellationToken = default)
        {
            var widget = await _db.WidgetDefinitions.IgnoreQueryFilters()
                .SingleOrDefaultAsync(w => w.Id == widgetDefinitionId, cancellationToken)
                ?? throw new WidgetDefinitionNotFoundException(widgetDefinitionId);

            widget.IsActive = isActive;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<LayoutTemplate> DefineLayoutTemplateAsync(int roleId, CancellationToken cancellationToken = default)
        {
            var template = new LayoutTemplate { RoleId = roleId };
            _db.LayoutTemplates.Add(template);
            await _db.SaveChangesAsync(cancellationToken);
            return template;
        }

        public async Task<LayoutTemplateWidget> AddWidgetToTemplateAsync(
            int layoutTemplateId, int widgetDefinitionId, int sortOrder, CancellationToken cancellationToken = default)
        {
            var entry = new LayoutTemplateWidget { LayoutTemplateId = layoutTemplateId, WidgetDefinitionId = widgetDefinitionId, SortOrder = sortOrder };
            _db.LayoutTemplateWidgets.Add(entry);
            await _db.SaveChangesAsync(cancellationToken);
            return entry;
        }

        /// <summary>Mirrors Sms.Infrastructure.Security.PermissionService's snapshot-building, but for an arbitrary target user rather than the ambient ICurrentUser — personalization/subscription-style checks need to verify someone other than "me".</summary>
        private async Task<bool> UserHasPermissionAsync(int userAccountId, int permissionId, CancellationToken cancellationToken)
        {
            var permission = await _db.Permissions.SingleAsync(p => p.Id == permissionId, cancellationToken);

            var raw = await _db.RoleAssignments
                .Where(a => a.UserAccountId == userAccountId)
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

            var snapshots = raw
                .Select(a => new AssignmentSnapshot(
                    a.Permissions.Select(p => new PermissionTriple(p.ModuleCode, p.ScreenCode, p.Action)).ToList(),
                    a.Grants.Select(g => new ScopeGrantValue(g.Dimension, g.ScopeValueId)).ToList()))
                .ToList();

            return PermissionEvaluator.HasPermission(snapshots, permission.ModuleCode, permission.ScreenCode, permission.Action);
        }

        public async Task<UserLayout> PersonalizeAsync(
            int userAccountId, int widgetDefinitionId, int sortOrder, bool isVisible, CancellationToken cancellationToken = default)
        {
            var widget = await _db.WidgetDefinitions.SingleAsync(w => w.Id == widgetDefinitionId, cancellationToken);
            if (!await UserHasPermissionAsync(userAccountId, widget.RequiredPermissionId, cancellationToken))
            {
                throw new WidgetNotPermittedException(userAccountId, widgetDefinitionId);
            }

            var layout = await _db.UserLayouts.SingleOrDefaultAsync(
                l => l.UserAccountId == userAccountId && l.WidgetDefinitionId == widgetDefinitionId, cancellationToken);
            if (layout == null)
            {
                layout = new UserLayout { UserAccountId = userAccountId, WidgetDefinitionId = widgetDefinitionId };
                _db.UserLayouts.Add(layout);
            }

            layout.SortOrder = sortOrder;
            layout.IsVisible = isVisible;
            await _db.SaveChangesAsync(cancellationToken);
            return layout;
        }

        public async Task ResetToDefaultAsync(int userAccountId, CancellationToken cancellationToken = default)
        {
            var rows = await _db.UserLayouts.Where(l => l.UserAccountId == userAccountId).ToListAsync(cancellationToken);
            _db.UserLayouts.RemoveRange(rows);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
