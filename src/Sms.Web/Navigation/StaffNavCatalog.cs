using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Application.Security;
using Sms.Domain.Security;

namespace Sms.Web.Navigation
{
    /// <summary>
    /// The staff sub-navigation (<c>_StaffNav.cshtml</c>) — the tab bar the employee and teacher
    /// screens share — and which of its tabs the signed-in user may actually open.
    /// <para>
    /// It is a table here rather than a literal in the view for the reason
    /// <see cref="WorkspaceCatalog"/> is one: the bar has to name the same screen permission the
    /// action behind it is guarded with, and a list living in Razor cannot be held to that by a
    /// test. <c>StaffNavTests</c> now holds it to the controllers' own
    /// <c>[RequirePermission]</c> attributes.
    /// </para>
    /// <para>
    /// BR-SEC-010: unauthorized surface disappears rather than refuses. The sidebar
    /// (<see cref="ModuleCatalog"/>) and the launcher (<see cref="WorkspaceCatalog"/>) already
    /// obeyed that; this bar did not, and showed all seven tabs to everyone. الثوابت is where it
    /// showed: that screen shipped after the schools were provisioned, and
    /// <c>PermissionSeedContributor</c> tops up no role but SYSADMIN, so an HR officer holding
    /// every other employee screen was shown the tab and sent to "page not found" by it.
    /// </para>
    /// </summary>
    public static class StaffNavCatalog
    {
        /// <summary>
        /// One tab. <paramref name="Restricted"/> draws the padlock the contract manager carries —
        /// salary and contract data is HR + Principal only (BR-EMP-003).
        /// </summary>
        public sealed record StaffTab(
            string Controller,
            string Action,
            string ModuleCode,
            string ScreenCode,
            string TitleEn,
            string TitleAr,
            string Icon,
            bool Restricted = false);

        public static IReadOnlyList<StaffTab> All { get; } = new[]
        {
            new StaffTab("Employees", "Index", ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Directory, "Employee directory", "دليل الموظفين", "bi-person-badge"),
            new StaffTab("Employees", "Org", ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.OrgChart, "Org chart", "الهيكل التنظيمي", "bi-diagram-3"),
            new StaffTab("Employees", "Contracts", ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Contracts, "Contract manager", "إدارة العقود", "bi-file-earmark-text", Restricted: true),
            new StaffTab("Employees", "Reference", ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Reference, "Reference lists", "الثوابت", "bi-card-list"),
            new StaffTab("Teachers", "Index", ScreenCatalog.Modules.Teachers, ScreenCatalog.Teachers.Teachers_, "Teacher directory", "دليل المعلمين", "bi-person-workspace"),
            new StaffTab("Teachers", "Matrix", ScreenCatalog.Modules.Teachers, ScreenCatalog.Teachers.Assignments, "Assignment matrix", "مصفوفة الإسناد", "bi-grid-3x3"),
            new StaffTab("Teachers", "Load", ScreenCatalog.Modules.Teachers, ScreenCatalog.Teachers.Load, "Load board", "لوحة النصاب", "bi-speedometer2"),
        };

        /// <summary>
        /// The tabs this user can open, in catalogue order. Serial rather than concurrent on
        /// purpose: <c>PermissionService</c> caches the user's assignments for the request, so the
        /// seven questions cost one query, and one <c>DbContext</c> cannot answer them in parallel
        /// anyway.
        /// </summary>
        public static async Task<IReadOnlyList<StaffTab>> VisibleAsync(
            IPermissionService permissions, CancellationToken cancellationToken = default)
        {
            var visible = new List<StaffTab>();
            foreach (var tab in All)
            {
                if (await permissions.HasPermissionAsync(tab.ModuleCode, tab.ScreenCode, ActionVerb.View, cancellationToken))
                {
                    visible.Add(tab);
                }
            }

            return visible;
        }
    }
}
