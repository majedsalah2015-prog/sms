using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Routing;
using Sms.TestSupport;
using Sms.Web.Navigation;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// Module 36's two built screens (doc/Modules/36 §8.1-8.2, doc 06 §8) as the menu shows them.
    /// <para>
    /// Before this section they were one leaf called "System administration" under Platform, and the
    /// user-role screen had no entry at all — the screen that decides whether a new employee reaches
    /// anything was found by typing /security/users. The section is therefore the fix, and these
    /// tests hold the two things that would quietly undo it: an entry appearing for someone who does
    /// not hold its screen, and the highlight landing on the wrong one of two entries that name the
    /// same controller.
    /// </para>
    /// </summary>
    public class SecurityNavigationTests
    {
        private const string SectionKey = "security";
        private const string UsersKey = "SYS-USERS";
        private const string RolesKey = "SYS-ROLES";

        private static IReadOnlyList<NavItem> Sidebar(bool canSeeRoles = true, bool canSeeUserRoles = true) =>
            ModuleCatalog.BuildSidebar(
                _ => true,
                _ => true,
                canExportToLedger: false,
                erpGroups: new List<NavItem>(),
                canSeeSectionBoard: true,
                canSeeRoles: canSeeRoles,
                canSeeUserRoles: canSeeUserRoles);

        private static RouteValueDictionary On(string action) =>
            new() { ["area"] = string.Empty, ["controller"] = "Security", ["action"] = action };

        [Fact]
        public void The_section_is_top_level_and_names_what_is_in_it()
        {
            var section = Assert.Single(Sidebar(), n => n.Key == SectionKey);

            Assert.Equal("المستخدمون والصلاحيات", section.TitleAr);
            Assert.Equal("Users & permissions", section.TitleEn);
            // The everyday screen first: roles are designed once and handed out every time somebody
            // joins, moves or leaves.
            Assert.Equal(new[] { UsersKey, RolesKey }, section.Items.Select(i => i.Key).ToArray());

            var users = section.Items[0];
            Assert.Equal("Security", users.Controller);
            Assert.Equal("Users", users.Action);
            Assert.Equal("المستخدمون وأدوارهم", users.TitleAr);

            var roles = section.Items[1];
            Assert.Equal("Security", roles.Controller);
            Assert.Equal("Index", roles.Action);
            Assert.Equal("الأدوار والصلاحيات", roles.TitleAr);
        }

        /// <summary>
        /// The other half of the move: module 36 is not also still a leaf at the foot of Platform.
        /// Two entries onto the same screen would light up whichever the resolver reached first and
        /// stand two groups open around it.
        /// </summary>
        [Fact]
        public void Module_36_is_no_longer_a_leaf_under_platform()
        {
            var sidebar = Sidebar();
            var platform = Assert.Single(sidebar, n => n.Key == "platform");

            Assert.DoesNotContain(platform.Items, i => i.Key == "SYS");
            Assert.Contains(platform.Items, i => i.Key == "RPT");
            Assert.Single(sidebar.SelectMany(Flatten), i => i.Controller == "Security" && i.Action == "Index");
        }

        /// <summary>
        /// BR-SEC-010: designing roles reaches every permission in the product, which is exactly the
        /// authority worth withholding from someone who may still hand existing roles out. The entry
        /// goes rather than showing a link that answers 404.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SEC-010")]
        public void The_role_designer_goes_for_a_user_who_may_not_design_roles()
        {
            var section = Assert.Single(Sidebar(canSeeRoles: false), n => n.Key == SectionKey);

            Assert.Equal(new[] { UsersKey }, section.Items.Select(i => i.Key).ToArray());
        }

        /// <summary>BR-SEC-010, the other way round: designing roles is not the right to hand them out.</summary>
        [Fact]
        [BusinessRule("BR-SEC-010")]
        public void The_user_roles_entry_goes_for_a_user_who_may_not_assign_them()
        {
            var section = Assert.Single(Sidebar(canSeeUserRoles: false), n => n.Key == SectionKey);

            Assert.Equal(new[] { RolesKey }, section.Items.Select(i => i.Key).ToArray());
        }

        /// <summary>
        /// No section at all for a user holding neither screen — rather than a heading over nothing,
        /// which would tell them the screens exist and that they may not have them.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SEC-010")]
        public void The_section_disappears_for_a_user_holding_neither_screen()
        {
            var sidebar = Sidebar(canSeeRoles: false, canSeeUserRoles: false);

            Assert.DoesNotContain(sidebar, n => n.Key == SectionKey);
            Assert.DoesNotContain(sidebar.SelectMany(Flatten), i => i.Controller == "Security");
        }

        /// <summary>
        /// Two entries name one controller, so the highlight has to be graded rather than answered
        /// yes/no — the same problem the assignment board has beside the Sections module.
        /// </summary>
        [Theory]
        [InlineData("Index", RolesKey)]
        [InlineData("Users", UsersKey)]
        public void Each_screen_highlights_its_own_entry(string action, string expected)
        {
            var active = NavLinks.Resolve(Sidebar(), On(action));

            Assert.NotNull(active);
            Assert.Equal(expected, active!.Key);
        }

        /// <summary>
        /// Opening one role is still the roles screen as far as the menu is concerned. Without the
        /// sibling action the designer would drop the highlight onto the user-roles entry, which is
        /// the first of the two the resolver reaches.
        /// </summary>
        [Fact]
        public void Opening_one_role_keeps_the_roles_entry_highlighted()
        {
            var active = NavLinks.Resolve(Sidebar(), On("Role"));

            Assert.NotNull(active);
            Assert.Equal(RolesKey, active!.Key);
        }

        /// <summary>One entry lit means one section open, on either screen.</summary>
        [Theory]
        [InlineData("Index")]
        [InlineData("Users")]
        public void Only_the_security_section_stands_open_on_its_screens(string action)
        {
            var sidebar = Sidebar();
            var active = NavLinks.Resolve(sidebar, On(action));

            var open = sidebar.Where(n => NavLinks.Contains(n, active)).Select(n => n.Key).ToList();

            Assert.Equal(new[] { SectionKey }, open);
        }

        private static IEnumerable<NavItem> Flatten(NavItem item) =>
            item.HasChildren ? item.Items.SelectMany(Flatten) : new[] { item };
    }
}
