using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.TestSupport;
using Sms.Web.Controllers;
using Sms.Web.Navigation;
using Sms.Web.Security;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The System Setup tab bar showed all ten tabs to everyone, and the screens behind them answer
    /// 404 to whoever does not hold them (BR-GLB-070). مناطق السكن is where that surfaced: the
    /// screen shipped after the schools were provisioned, so every database seeded before it had no
    /// <c>SET/Residence</c> permission at all and the tab led nowhere for everyone — and after the
    /// seeder catalogues it, only SYSADMIN is topped up, so a setup operator still lands on "page
    /// not found".
    /// <para>
    /// Two things have to hold for the bar to be honest, and neither did: it must drop the tabs
    /// this user cannot open, and each tab must name the same permission the action behind it is
    /// guarded with — a bar that hides on the wrong permission is a bar that hides the wrong tab.
    /// </para>
    /// </summary>
    public class SetupNavTests
    {
        /// <summary>Grants exactly the module/screen pairs it is given.</summary>
        private sealed class StubPermissions : IPermissionService
        {
            private readonly HashSet<(string, string)> _granted;

            public StubPermissions(params (string Module, string Screen)[] granted) =>
                _granted = new HashSet<(string, string)>(granted);

            public Task<bool> HasPermissionAsync(string moduleCode, string screenCode, ActionVerb action, CancellationToken cancellationToken = default)
                => Task.FromResult(_granted.Contains((moduleCode, screenCode)));

            public Task<EffectiveScope?> GetEffectiveScopeAsync(string moduleCode, string screenCode, ActionVerb action, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<IReadOnlyList<string>> GetGrantedScreenCodesAsync(int userAccountId, string moduleCode, ActionVerb action, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        /// <summary>
        /// BR-SEC-010. The one that was actually wrong in the product: a user who may run the
        /// wizard and the lookup lists but was never granted the residence hierarchy is offered
        /// مناطق السكن and gets "page not found" for taking it up.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SEC-010")]
        public async Task The_residence_tab_goes_for_a_user_without_that_screen()
        {
            var visible = await SetupNavCatalog.VisibleAsync(new StubPermissions(
                (ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Wizard),
                (ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Lookups)));

            Assert.DoesNotContain(visible, t => t.ScreenCode == ScreenCatalog.Setup.Residence);
            Assert.Equal(
                new[] { ScreenCatalog.Setup.Wizard, ScreenCatalog.Setup.Lookups },
                visible.Select(t => t.ScreenCode));
        }

        /// <summary>A user holding nothing is shown no bar at all, not an empty one.</summary>
        [Fact]
        [BusinessRule("BR-SEC-010")]
        public async Task A_user_holding_none_of_the_screens_gets_no_tabs()
            => Assert.Empty(await SetupNavCatalog.VisibleAsync(new StubPermissions()));

        /// <summary>And the bar is still whole for whoever holds all of it.</summary>
        [Fact]
        public async Task Every_tab_shows_for_a_user_holding_them_all()
        {
            var all = SetupNavCatalog.All.Select(t => (t.ModuleCode, t.ScreenCode)).ToArray();

            Assert.Equal(SetupNavCatalog.All, await SetupNavCatalog.VisibleAsync(new StubPermissions(all)));
        }

        /// <summary>
        /// The tab hides on the same permission the action refuses on. Read off the action's own
        /// <c>[RequirePermission]</c> rather than restated here, so the two cannot drift into a tab
        /// that is shown to someone the screen then turns away — which is the bug this file is
        /// about, one level further in.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SEC-010")]
        public void Each_tab_names_the_permission_its_own_action_is_guarded_with()
        {
            foreach (var tab in SetupNavCatalog.All)
            {
                var method = typeof(SetupController)
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Single(m => m.Name == tab.Action && m.GetCustomAttributes<HttpGetAttribute>().Any());

                var guard = Assert.Single(method.GetCustomAttributes<RequirePermissionAttribute>());
                var arguments = guard.Arguments!;

                Assert.Equal(tab.ModuleCode, arguments[0]);
                Assert.Equal(tab.ScreenCode, arguments[1]);
                Assert.Equal(ActionVerb.View, arguments[2]);
            }
        }

        /// <summary>
        /// A tab whose screen is not in the catalogue, or defines no View, can never be shown: the
        /// permission it asks about is one no seeder ever creates and no role can ever hold.
        /// </summary>
        [Fact]
        public void Every_tab_points_at_a_catalogued_screen_that_can_be_viewed()
        {
            foreach (var tab in SetupNavCatalog.All)
            {
                var screen = Assert.Single(
                    ScreenCatalog.Screens.Where(s => s.ModuleCode == tab.ModuleCode && s.ScreenCode == tab.ScreenCode));

                Assert.Contains(ActionVerb.View, screen.Verbs);
            }
        }
    }
}
