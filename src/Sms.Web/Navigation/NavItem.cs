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
        public NavItem(string key, string titleEn, string titleAr, string icon, string? controller = null, string? action = null, object? routeValues = null, string? area = null, string? url = null)
        {
            Key = key;
            TitleEn = titleEn;
            TitleAr = titleAr;
            Icon = icon;
            Controller = controller;
            Action = action;
            RouteValues = routeValues;
            Area = area;
            Url = url;
        }

        public string Key { get; }

        public string TitleEn { get; }

        public string TitleAr { get; }

        /// <summary>Bootstrap Icons class suffix, e.g. "bi-people".</summary>
        public string Icon { get; }

        public string? Controller { get; }

        public string? Action { get; }

        /// <summary>
        /// The MVC area the target lives in, or <c>null</c> for this system's own screens, which use
        /// none. Carried as its own property rather than inside <see cref="RouteValues"/> because the
        /// sidebar has to state it on <b>every</b> link, including the ones that have no area:
        /// <c>Url.Action</c> inherits the current request's area when the caller stays silent, so
        /// while an embedded ERP screen is open, a school link that did not say <c>area = ""</c> would
        /// generate /Accounting/Students and 404. Two links to the same controller in different areas
        /// (ERP has four AccountMapping screens, and a POS Reports beside this system's own) also make
        /// the area the only thing that tells the highlighted entry from its namesakes.
        /// </summary>
        public string? Area { get; }

        public object? RouteValues { get; }

        /// <summary>
        /// An application-relative URL used instead of controller/action, or <c>null</c> for the
        /// ordinary case. The embedded ERP needs this for entries that carry a query string — its
        /// "manual debit voucher" and "manual credit voucher" are one screen reached with
        /// <c>?side=Debit</c> and <c>?side=Credit</c> — which a controller/action pair cannot express.
        /// Such an entry is never highlighted as the current screen: the path alone would light up
        /// both of them on either.
        /// </summary>
        public string? Url { get; }

        public List<NavItem> Items { get; } = new();

        public bool HasChildren => Items.Count > 0;

        public string Title(bool arabic) => arabic ? TitleAr : TitleEn;
    }
}
