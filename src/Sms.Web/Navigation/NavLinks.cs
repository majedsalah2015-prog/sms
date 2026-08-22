using System;
using System.Linq;
using Microsoft.AspNetCore.Routing;

namespace Sms.Web.Navigation
{
    /// <summary>
    /// What the sidebar needs to turn a <see cref="NavItem"/> into a link and decide whether it is
    /// the screen being shown. Here rather than as local functions in the view because the sidebar
    /// renders itself recursively — a group inside a group — and a Razor local function is not in
    /// scope inside the partial it renders.
    /// </summary>
    public static class NavLinks
    {
        /// <summary>
        /// The route values to generate this entry's URL from.
        /// <para>
        /// <c>area</c> is always present, empty string included. <c>Url.Action</c> inherits the
        /// current request's area whenever the caller stays silent about it, so while an embedded ERP
        /// screen is open every school link that did not say <c>area = ""</c> would generate
        /// /Accounting/Students and answer 404.
        /// </para>
        /// </summary>
        public static RouteValueDictionary RouteValuesFor(NavItem item) =>
            new(item.RouteValues) { ["area"] = item.Area ?? string.Empty };

        /// <summary>
        /// Whether <paramref name="item"/> is the screen currently being shown.
        /// </summary>
        public static bool IsActive(NavItem item, RouteValueDictionary routeValues)
        {
            // An entry addressed by URL is never highlighted. The two the ERP has are one screen
            // reached with different query strings, and route data carries no query string — so the
            // only comparison available would light up both of them on either.
            if (item.Url != null)
            {
                return false;
            }

            var area = Value(routeValues, "area");
            var controller = Value(routeValues, "controller");
            var action = Value(routeValues, "action");
            var code = Value(routeValues, "code");

            if (item.Controller == null || !string.Equals(item.Controller, controller, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // The area has to match even when both are empty. The embedded ERP publishes four
            // AccountMapping screens and a POS Reports beside this system's own Reports, so matching
            // on the controller name alone would light up several entries at once.
            if (!string.Equals(item.Area ?? string.Empty, area, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // One of this system's own modules with a controller to itself (e.g. Setup): any of its
            // actions counts as being in it. Not applied to the ERP's entries, which are individual
            // screens of a shared controller rather than a module apiece.
            if (item.Area == null && item.RouteValues == null
                && !string.Equals(item.Controller, "Home", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.Equals(item.Action, action, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var itemCode = item.RouteValues?.GetType().GetProperty("code")?.GetValue(item.RouteValues) as string;
            return itemCode == null || string.Equals(itemCode, code, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Whether this entry, or anything beneath it at any depth, is the screen being shown — so
        /// the accounting section and the module group inside it both open on the screen the user is
        /// actually on.
        /// </summary>
        public static bool IsBranchActive(NavItem item, RouteValueDictionary routeValues) =>
            item.HasChildren
                ? item.Items.Any(child => IsBranchActive(child, routeValues))
                : IsActive(item, routeValues);

        private static string Value(RouteValueDictionary routeValues, string key) =>
            routeValues.TryGetValue(key, out var value) ? value as string ?? string.Empty : string.Empty;
    }
}
