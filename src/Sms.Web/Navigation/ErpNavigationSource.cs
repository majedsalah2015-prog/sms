using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using ERP2028.Web.Shared.Navigation;
using Microsoft.Extensions.Localization;
using ErpNavItem = ERP2028.Web.Shared.Navigation.NavItem;
using ErpNavSection = ERP2028.Web.Shared.Navigation.NavSection;
using SharedResource = ERP2028.Web.Shared.Resources.SharedResource;

namespace Sms.Web.Navigation
{
    /// <summary>
    /// Turns the embedded ERP's own sidebar into entries of this system's sidebar, so every screen
    /// the ERP publishes appears under the accounting section without this system listing any of them.
    /// <para>
    /// The alternative — the hand-written list P3 shipped — was nine entries against two modules and
    /// was already a second source of truth. Against seven modules and roughly a hundred and fifty
    /// screens it would be a maintenance liability that silently goes stale: a screen added upstream
    /// would simply never appear here, and nothing would fail to say so. Reading the ERP's own
    /// <see cref="INavigationProvider"/>s instead means a submodule bump brings its new screens with
    /// it, in the ERP's own order and under its own grouping.
    /// </para>
    /// <para>
    /// Permission filtering is the ERP's, done by <see cref="INavigationMenu"/> against the
    /// <c>erp.permission</c> claims this system mints at sign-in from ordinary <c>sec.RolePermission</c>
    /// grants. That is a real change from P3, where the ERP entries were shown to everyone and each
    /// screen denied on arrival: nine links a user could not open was untidy, a hundred and fifty is
    /// a menu that lies about what the user's job is.
    /// </para>
    /// </summary>
    public sealed class ErpNavigationSource
    {
        /// <summary>
        /// The ERP's own shared resource, the same one its shell localizes its sidebar through — so an
        /// entry reads here exactly as it reads on the screen it opens, and a translation added
        /// upstream arrives with the submodule bump rather than being re-typed into this system.
        /// </summary>
        private readonly IStringLocalizer<SharedResource> _localizer;

        private readonly INavigationMenu _menu;

        public ErpNavigationSource(INavigationMenu menu, IStringLocalizer<SharedResource> localizer)
        {
            _menu = menu;
            _localizer = localizer;
        }

        /// <summary>
        /// One <see cref="NavItem"/> group per ERP section, each holding that section's links —
        /// the children of this system's accounting group. A section the user may see nothing of has
        /// already been dropped by <see cref="INavigationMenu"/>; a section that survives with no
        /// items (the ERP's "coming soon" placeholders) is dropped here, because this system's
        /// sidebar has no disabled state and a group that opens onto nothing is worse than absent.
        /// </summary>
        public IReadOnlyList<NavItem> BuildGroupsFor(ClaimsPrincipal user)
        {
            var groups = new List<NavItem>();

            foreach (var section in _menu.BuildFor(user))
            {
                // A leaf section — one the ERP renders as a single link rather than a group. None of
                // the hosted modules publishes one today; handled anyway, so that a module which
                // starts to appears here rather than silently disappearing from the menu.
                if (!section.HasChildren)
                {
                    if (section.ComingSoon)
                    {
                        continue;
                    }

                    groups.Add(Leaf(GroupKey(section), section.Title, section.Icon, section.Area, section.Controller, section.Action));
                    continue;
                }

                var live = section.Items.Where(i => !i.ComingSoon).ToList();
                if (live.Count == 0)
                {
                    continue;
                }

                var group = new NavItem(GroupKey(section), section.Title, Arabic(section.Title), Icon(section.Icon));
                foreach (var item in live)
                {
                    group.Items.Add(Leaf(ItemKey(section, item), item.Title, item.Icon, item.Area, item.Controller, item.Action, item.Url));
                }

                groups.Add(group);
            }

            return groups;
        }

        /// <summary>
        /// One link. An entry with an explicit <paramref name="url"/> keeps it — that is how the ERP
        /// expresses a target a controller/action pair cannot, such as its two manual voucher entries,
        /// which are the same screen reached with <c>?side=Debit</c> and <c>?side=Credit</c>. The
        /// others carry an area, empty string included, so the link states which one it means.
        /// </summary>
        private NavItem Leaf(
            string key, string title, string? icon, string? area, string? controller, string? action, string? url = null) =>
            new(key, title, Arabic(title), Icon(icon), controller, action, area: url is null ? area ?? string.Empty : null, url: url);

        /// <summary>
        /// Prefixed, because the two systems chose the same word for the same thing: the ERP's
        /// Accounting section is keyed <c>accounting</c> and so is the section of this system's
        /// sidebar it is about to be nested inside. The sidebar builds a collapse element id from the
        /// key, so an unprefixed one would put two <c>#nav-accounting</c> ids on the page and the
        /// section header would toggle whichever the browser found first.
        /// </summary>
        private static string GroupKey(ErpNavSection section) => "erp-" + section.Key;

        /// <summary>
        /// Section-scoped, because the ERP's item titles are not unique across sections — "Reports"
        /// and "Account Mapping" each appear in several — and a duplicate key would collide the same
        /// way <see cref="GroupKey"/> describes.
        /// </summary>
        private static string ItemKey(ErpNavSection section, ErpNavItem item) =>
            $"{GroupKey(section)}-{item.Title.ToLowerInvariant().Replace(' ', '-')}";

        private static string Icon(string? icon) => string.IsNullOrWhiteSpace(icon) ? "bi-dot" : icon;

        /// <summary>
        /// The Arabic title for an ERP entry, asked for explicitly rather than through the request's
        /// culture: this system carries both languages on every <see cref="NavItem"/> and picks between
        /// them when it renders, so what it builds must not depend on which language built it.
        /// <para>
        /// The ERP's resource keys are the English text itself, which is why only the Arabic side is
        /// looked up and an untranslated key falls back to the English rather than to blank.
        /// </para>
        /// </summary>
        private string Arabic(string title)
        {
            var previous = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = ArabicCulture;
            try
            {
                var translated = _localizer[title];
                return translated.ResourceNotFound ? title : translated.Value;
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
            }
        }

        /// <summary>The one translated culture the ERP authors resources for; see SharedResource.</summary>
        private static readonly CultureInfo ArabicCulture = new("ar-SA");
    }
}
