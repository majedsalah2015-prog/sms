using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Application.Security;
using Sms.Domain.Security;

namespace Sms.Web.Navigation
{
    /// <summary>
    /// The System Setup sub-navigation (<c>_SetupNav.cshtml</c>) — the tab bar every setup screen
    /// shares — and which of its tabs the signed-in user may actually open.
    /// <para>
    /// A table here rather than a literal in the view, for the reason <see cref="StaffNavCatalog"/>
    /// is one: each tab has to name the same screen permission the action behind it is guarded
    /// with, and a list living in Razor cannot be held to that by a test. <c>SetupNavTests</c>
    /// holds it to <c>SetupController</c>'s own <c>[RequirePermission]</c> attributes.
    /// </para>
    /// <para>
    /// BR-SEC-010: unauthorized surface disappears rather than refuses. The sidebar
    /// (<see cref="ModuleCatalog"/>), the launcher (<see cref="WorkspaceCatalog"/>) and the staff
    /// bar already obeyed that; this one did not, and offered all ten tabs to everyone while the
    /// screens behind them answer <c>NotFound</c> to whoever does not hold them (BR-GLB-070).
    /// مناطق السكن is where it showed: that screen shipped after the schools were provisioned, so a
    /// database seeded before it carries no <c>SET/Residence</c> permission at all and the tab led
    /// nowhere for everyone — and once the seeder catalogues it,
    /// <c>PermissionSeedContributor</c> tops up no staff role but SYSADMIN, so an operator holding
    /// every other setup screen is still shown the tab and sent to "page not found" by it.
    /// </para>
    /// </summary>
    public static class SetupNavCatalog
    {
        /// <summary>The controller every tab links to.</summary>
        public const string Controller = "Setup";

        /// <summary>One tab, and the screen permission it is shown on.</summary>
        public sealed record SetupTab(
            string Action,
            string ModuleCode,
            string ScreenCode,
            string TitleEn,
            string TitleAr,
            string Icon);

        public static IReadOnlyList<SetupTab> All { get; } = new[]
        {
            new SetupTab("Index", ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Wizard, "Setup wizard", "معالج الإعداد", "bi-list-check"),
            new SetupTab("Settings", ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Settings, "Settings hub", "مركز الإعدادات", "bi-sliders"),
            new SetupTab("Features", ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Features, "Feature toggles", "الميزات", "bi-toggles"),
            new SetupTab("Pack", ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.ContentPack, "Country pack", "حزمة الدولة", "bi-globe2"),
            new SetupTab("Numbering", ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Numbering, "Numbering", "الترقيم", "bi-123"),
            new SetupTab("Documents", ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Documents, "Document types", "أنواع المستندات", "bi-paperclip"),
            new SetupTab("Notifications", ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Notifications, "Notification defaults", "الإشعارات", "bi-bell"),
            new SetupTab("Lookups", ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Lookups, "Lookup lists", "القوائم المرجعية", "bi-card-list"),
            new SetupTab("Nationalities", ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Nationalities, "Nationalities", "الجنسيات", "bi-flag"),
            new SetupTab("Residence", ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Residence, "Residence areas", "مناطق السكن", "bi-geo-alt"),
        };

        /// <summary>
        /// The tabs this user can open, in catalogue order. Serial rather than concurrent on
        /// purpose: <c>PermissionService</c> caches the user's assignments for the request, so the
        /// ten questions cost one query, and one <c>DbContext</c> cannot answer them in parallel
        /// anyway.
        /// </summary>
        public static async Task<IReadOnlyList<SetupTab>> VisibleAsync(
            IPermissionService permissions, CancellationToken cancellationToken = default)
        {
            var visible = new List<SetupTab>();
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
