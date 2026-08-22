using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Web.Navigation;
using Sms.Web.Security;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The landing page's departments are a second index over screens this system already has, and
    /// an index can be wrong in exactly one way that nothing else catches: it can point somewhere
    /// that is not there. A renamed action, a screen code that never existed, a permission pair that
    /// does not match the one the action actually enforces — each produces a tile that looks right
    /// and answers 404, or worse, one that is hidden from the person who is allowed to use it.
    /// <para>
    /// These tests hold the catalogue against the code it claims to describe, by reflection, so the
    /// mistake is a red build rather than a support call.
    /// </para>
    /// </summary>
    public class WorkspaceCatalogTests
    {
        private static readonly Assembly Web = typeof(Sms.Web.Startup).Assembly;

        public static IEnumerable<object[]> AllLinks =>
            WorkspaceCatalog.Workspaces
                .SelectMany(w => w.Links.Select(l => new object[] { w.Key, l }))
                .ToList();

        [Fact]
        public void The_owner_asked_for_these_departments_and_they_are_all_here()
        {
            // The list, in the order it was asked for: finance, students, secretariat, teaching
            // staff, reports, timetable, cover rota — plus transport, asked for after the first
            // seven and placed beside the timetable because both are about where people have to be.
            Assert.Equal(
                new[] { "finance", "students", "secretariat", "teachers", "reports", "timetable", "transport", "cover" },
                WorkspaceCatalog.Workspaces.Select(w => w.Key).ToArray());
        }

        /// <summary>
        /// Transport's tile leads with the trip console rather than the fleet. The department is
        /// opened at 07:00 far more often than it is opened to register a bus, and the first card is
        /// what a hurried person clicks.
        /// </summary>
        [Fact]
        public void The_transport_department_leads_with_the_trip_console()
        {
            var transport = WorkspaceCatalog.Find("transport")!;

            Assert.Equal("Trips", transport.Links[0].Action);
            Assert.Equal(ScreenCatalog.Transport.Trips, transport.Links[0].ScreenCode);
            Assert.Contains(transport.Links, l => l.ScreenCode == ScreenCatalog.Transport.Safety);
            Assert.Contains(transport.Links, l => l.ScreenCode == ScreenCatalog.Transport.Fleet);
        }

        [Fact]
        public void Every_department_has_a_key_of_its_own()
        {
            var keys = WorkspaceCatalog.Workspaces.Select(w => w.Key).ToList();

            Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [Fact]
        public void Every_department_is_named_in_both_languages_and_carries_an_icon()
        {
            foreach (var w in WorkspaceCatalog.Workspaces)
            {
                Assert.False(string.IsNullOrWhiteSpace(w.TitleEn), $"{w.Key}: no English title");
                Assert.False(string.IsNullOrWhiteSpace(w.TitleAr), $"{w.Key}: no Arabic title");
                Assert.False(string.IsNullOrWhiteSpace(w.BlurbAr), $"{w.Key}: no Arabic blurb");
                Assert.StartsWith("bi-", w.Icon);
                Assert.NotEmpty(w.Links);
            }
        }

        [Theory]
        [MemberData(nameof(AllLinks))]
        public void Every_link_names_a_screen_the_catalogue_declares_viewable(
            string workspaceKey, WorkspaceCatalog.WorkspaceLink link)
        {
            var screen = ScreenCatalog.Screens.SingleOrDefault(s =>
                s.ModuleCode == link.ModuleCode && s.ScreenCode == link.ScreenCode);

            Assert.True(screen != null,
                $"{workspaceKey}: no screen {link.ModuleCode}/{link.ScreenCode} in ScreenCatalog.");
            Assert.True(screen!.Verbs.Contains(ActionVerb.View),
                $"{workspaceKey}: {link.ModuleCode}/{link.ScreenCode} has no View verb, so it can never be opened from a tile.");
        }

        /// <summary>
        /// The test that stops a dead tile: the action exists, answers GET, and needs nothing the
        /// launcher cannot supply. A link to an action with a required argument would render fine and
        /// then throw or bind a zero.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllLinks))]
        public void Every_link_reaches_a_GET_action_that_needs_no_arguments(
            string workspaceKey, WorkspaceCatalog.WorkspaceLink link)
        {
            var action = FindGetAction(link);

            Assert.True(action != null,
                $"{workspaceKey}: no parameterless GET action {link.Controller}Controller.{link.Action}.");
            Assert.All(action!.GetParameters(), p =>
                Assert.True(p.IsOptional, $"{workspaceKey}: {link.Controller}.{link.Action} requires '{p.Name}'."));
        }

        /// <summary>
        /// The tile's permission and the screen's guard must be the same pair. If they diverge, the
        /// launcher either hides a screen from someone entitled to it or offers one that will refuse
        /// them — and both look like a permissions bug rather than a catalogue typo.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllLinks))]
        public void Every_link_is_gated_on_the_permission_its_action_enforces(
            string workspaceKey, WorkspaceCatalog.WorkspaceLink link)
        {
            var action = FindGetAction(link)!;
            var attribute = action.GetCustomAttributes<RequirePermissionAttribute>().SingleOrDefault();

            Assert.True(attribute != null,
                $"{workspaceKey}: {link.Controller}.{link.Action} declares no [RequirePermission].");
            Assert.Equal(
                new object[] { link.ModuleCode, link.ScreenCode, ActionVerb.View },
                attribute!.Arguments);
        }

        /// <summary>
        /// Finance is the department the embedded ERP belongs to, and the only one — the ledger, the
        /// stores and the till are money, and putting them anywhere else would split the accounting
        /// story across two places again.
        /// </summary>
        [Fact]
        public void Only_finance_carries_the_embedded_accounting()
        {
            var accounting = WorkspaceCatalog.Workspaces.Where(w => w.Accounting).ToList();

            var only = Assert.Single(accounting);
            Assert.Equal("finance", only.Key);
        }

        /// <summary>
        /// The cover rota is one screen, which is what lets its tile open that screen directly rather
        /// than a page holding a single card.
        /// </summary>
        [Fact]
        public void The_cover_rota_is_a_single_screen()
        {
            var cover = WorkspaceCatalog.Find("cover")!;

            var link = Assert.Single(cover.Links);
            Assert.Equal("Timetable", link.Controller);
            Assert.Equal("Cover", link.Action);
            Assert.False(cover.Accounting);
        }

        [Fact]
        public void An_unknown_department_is_not_found()
        {
            Assert.Null(WorkspaceCatalog.Find("payroll"));
        }

        private static MethodInfo? FindGetAction(WorkspaceCatalog.WorkspaceLink link)
        {
            var controller = Web.GetTypes().SingleOrDefault(t =>
                t.Name == link.Controller + "Controller" && typeof(Controller).IsAssignableFrom(t));

            return controller?
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.Name == link.Action && !m.IsSpecialName)
                .FirstOrDefault(m => m.GetCustomAttribute<HttpPostAttribute>() == null);
        }
    }
}
