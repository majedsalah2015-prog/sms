using System;
using System.Collections.Generic;
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
        /// <summary>This entry is not the screen being shown.</summary>
        public const int NoMatch = 0;

        /// <summary>
        /// This entry is the module that owns the controller being shown, but does not name the
        /// action — the weak match that puts /sections/5/edit on the Sections entry.
        /// </summary>
        public const int OwningModule = 1;

        /// <summary>This entry names the exact screen being shown.</summary>
        public const int ExactScreen = 2;

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
        /// How well <paramref name="item"/> matches the screen being shown: <see cref="NoMatch"/>,
        /// <see cref="OwningModule"/>, or <see cref="ExactScreen"/>.
        /// <para>
        /// Two entries can legitimately name one controller — the Sections module and the assignment
        /// board that is one of its screens — and a yes/no answer would light up both of them, and
        /// open both of their groups, on either screen. Grading the match lets
        /// <see cref="Resolve"/> pick the one that says the most, so the board wins on /sections/board
        /// and the module wins everywhere else in it.
        /// </para>
        /// </summary>
        public static int MatchStrength(NavItem item, RouteValueDictionary routeValues)
        {
            // An entry addressed by URL is never highlighted. The two the ERP has are one screen
            // reached with different query strings, and route data carries no query string — so the
            // only comparison available would light up both of them on either.
            if (item.Url != null)
            {
                return NoMatch;
            }

            var area = Value(routeValues, "area");
            var controller = Value(routeValues, "controller");
            var action = Value(routeValues, "action");
            var code = Value(routeValues, "code");

            if (item.Controller == null || !string.Equals(item.Controller, controller, StringComparison.OrdinalIgnoreCase))
            {
                return NoMatch;
            }

            // The area has to match even when both are empty. The embedded ERP publishes four
            // AccountMapping screens and a POS Reports beside this system's own Reports, so matching
            // on the controller name alone would light up several entries at once.
            if (!string.Equals(item.Area ?? string.Empty, area, StringComparison.OrdinalIgnoreCase))
            {
                return NoMatch;
            }

            // The unbuilt modules are all ModulesController.Index and are told apart only by code.
            var itemCode = item.RouteValues?.GetType().GetProperty("code")?.GetValue(item.RouteValues) as string;
            if (itemCode != null && !string.Equals(itemCode, code, StringComparison.OrdinalIgnoreCase))
            {
                return NoMatch;
            }

            if (string.Equals(item.Action, action, StringComparison.OrdinalIgnoreCase)
                || Names(item.SiblingActions, action))
            {
                return ExactScreen;
            }

            // One of this system's own modules with a controller to itself (e.g. Setup): any of its
            // actions counts as being in it. Not applied to the ERP's entries, which are individual
            // screens of a shared controller rather than a module apiece.
            if (item.Area == null && item.RouteValues == null
                && !string.Equals(item.Controller, "Home", StringComparison.OrdinalIgnoreCase))
            {
                return OwningModule;
            }

            return NoMatch;
        }

        /// <summary>
        /// Whether <paramref name="item"/> matches the screen being shown at all. The sidebar asks
        /// <see cref="Resolve"/> instead, because it needs the single best match rather than every
        /// entry that matches; this stays as the plain predicate over one entry.
        /// </summary>
        public static bool IsActive(NavItem item, RouteValueDictionary routeValues) =>
            MatchStrength(item, routeValues) != NoMatch;

        /// <summary>
        /// The one entry in the whole tree that is the screen being shown, or <c>null</c> when the
        /// screen is not in the menu at all. The strongest match wins; ties go to the entry that
        /// appears first, so a module keeps its own controller's odd screens.
        /// </summary>
        public static NavItem? Resolve(IEnumerable<NavItem> items, RouteValueDictionary routeValues)
        {
            NavItem? best = null;
            var strongest = NoMatch;

            void Walk(IEnumerable<NavItem> nodes)
            {
                foreach (var node in nodes)
                {
                    if (node.HasChildren)
                    {
                        Walk(node.Items);
                        continue;
                    }

                    var strength = MatchStrength(node, routeValues);
                    if (strength > strongest)
                    {
                        best = node;
                        strongest = strength;
                    }
                }
            }

            Walk(items);
            return best;
        }

        /// <summary>
        /// Whether <paramref name="active"/> — the entry <see cref="Resolve"/> settled on — is this
        /// entry or sits anywhere beneath it, so the accounting section and the module group inside
        /// it both open on the screen the user is actually on.
        /// </summary>
        public static bool Contains(NavItem item, NavItem? active) =>
            active != null
            && (item.HasChildren
                ? item.Items.Any(child => Contains(child, active))
                : ReferenceEquals(item, active));

        private static bool Names(IReadOnlyCollection<string>? actions, string action) =>
            actions != null && actions.Any(a => string.Equals(a, action, StringComparison.OrdinalIgnoreCase));

        private static string Value(RouteValueDictionary routeValues, string key) =>
            routeValues.TryGetValue(key, out var value) ? value as string ?? string.Empty : string.Empty;
    }
}
