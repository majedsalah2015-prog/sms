using System.Collections.Generic;

namespace Sms.Web.Navigation
{
    /// <summary>
    /// One sidebar entry (doc/DesignSystem/05 §"Navigation registry"). Leaf
    /// entries route to a controller/action; group entries hold children.
    /// Titles are carried bilingually here (BR-GLB-001 shape) instead of via
    /// resource files — the shell has no localization pipeline yet, and the
    /// module catalog is static product data, not per-school text.
    /// </summary>
    public sealed class NavItem
    {
        public NavItem(string key, string titleEn, string titleAr, string icon, string? controller = null, string? action = null, object? routeValues = null)
        {
            Key = key;
            TitleEn = titleEn;
            TitleAr = titleAr;
            Icon = icon;
            Controller = controller;
            Action = action;
            RouteValues = routeValues;
        }

        public string Key { get; }

        public string TitleEn { get; }

        public string TitleAr { get; }

        /// <summary>Bootstrap Icons class suffix, e.g. "bi-people".</summary>
        public string Icon { get; }

        public string? Controller { get; }

        public string? Action { get; }

        public object? RouteValues { get; }

        public List<NavItem> Items { get; } = new();

        public bool HasChildren => Items.Count > 0;

        public string Title(bool arabic) => arabic ? TitleAr : TitleEn;
    }
}
