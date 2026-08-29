using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Dashboards;

namespace Sms.Application.Dashboards
{
    /// <summary>
    /// doc/Modules/31 §8 Dashboard shell / Layout administrator screens
    /// backing (screens deferred, the operations are core). Widget
    /// *content* (the consolidated widget→data-source→drill-path
    /// specification) is Phase 9, out of scope — see
    /// <see cref="IDashboardQuery"/> for the handful of real widget
    /// computations this slice does wire, reusing each source module's
    /// own calculator (BR-DSH-002's "one computation source" mandate).
    /// </summary>
    public interface IDashboardAdmin
    {
        Task<WidgetDefinition> DefineWidgetAsync(
            string code, string owningModuleCode, string titleAr, string titleEn, int requiredPermissionId,
            WidgetRefreshClass refreshClass, string drillTargetCode, bool isPortalEligible, CancellationToken cancellationToken = default);

        /// <summary>
        /// doc/Modules/33 §8: corrects a registered widget in place. The registry could only be
        /// appended to, so a widget registered against the wrong permission — the one mistake
        /// that makes a panel invisible to the people it was for (BR-DSH-001) — could not be
        /// repaired, only shadowed by a second row under a new code.
        /// <para>The code is the identity: layout templates and personalizations point at the row, so it is not editable here.</para>
        /// </summary>
        Task UpdateWidgetAsync(
            int widgetDefinitionId, string owningModuleCode, string titleAr, string titleEn, int requiredPermissionId,
            WidgetRefreshClass refreshClass, string drillTargetCode, bool isPortalEligible, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-GLB-005: retires a widget or puts it back. A retired widget stops being offered to
        /// layout templates; the templates already carrying it keep the row, so this is a flag
        /// and not a delete.
        /// </summary>
        Task SetWidgetActiveAsync(int widgetDefinitionId, bool isActive, CancellationToken cancellationToken = default);

        Task<LayoutTemplate> DefineLayoutTemplateAsync(int roleId, CancellationToken cancellationToken = default);

        Task<LayoutTemplateWidget> AddWidgetToTemplateAsync(
            int layoutTemplateId, int widgetDefinitionId, int sortOrder, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.WidgetNotPermittedException"/> unless the user holds the widget's required permission (doc §9, server-enforced).</summary>
        Task<UserLayout> PersonalizeAsync(
            int userAccountId, int widgetDefinitionId, int sortOrder, bool isVisible, CancellationToken cancellationToken = default);

        /// <summary>Removes all of the user's personalization rows — they fall back to their role's LayoutTemplate.</summary>
        Task ResetToDefaultAsync(int userAccountId, CancellationToken cancellationToken = default);
    }
}
