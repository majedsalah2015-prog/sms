using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    /// Module 37's entry in the sidebar. The module shipped its screens with a catalogue entry
    /// already in place, so nothing here was ever missing from the menu's own table — and the
    /// menu still showed nothing, because <see cref="ModuleVisibility"/> hides a module the user
    /// can open no screen of (BR-SEC-010) and the module held no permission anybody had been
    /// granted. These tests pin both halves of that: the entry is there, and it is there only for
    /// somebody who can actually open it.
    /// </summary>
    public class LearningNavigationTests
    {
        private const string LearningKey = "LRN";

        private static IReadOnlyList<NavItem> Sidebar(bool permitted = true, bool visible = true) =>
            ModuleCatalog.BuildSidebar(
                m => visible || m.Code != LearningKey,
                m => permitted || m.Code != LearningKey,
                canExportToLedger: false,
                erpGroups: new List<NavItem>());

        private static NavItem Academics(IReadOnlyList<NavItem> sidebar) =>
            Assert.Single(sidebar, n => n.Key == "academics");

        /// <summary>
        /// The reported defect, from the menu's side: "التعليم الالكتروني غير موجود له رابط" — the
        /// module had screens, a controller and a catalogue row, and no way in.
        /// </summary>
        [Fact]
        public void E_learning_has_an_entry_under_academics_that_opens_the_lesson_planner()
        {
            var academics = Academics(Sidebar());

            var learning = Assert.Single(academics.Items, i => i.Key == LearningKey);
            Assert.Equal("Learning", learning.Controller);
            Assert.Equal("Index", learning.Action);
            Assert.Equal("E-Learning", learning.TitleEn);
            Assert.Equal("التعليم الإلكتروني", learning.TitleAr);
        }

        /// <summary>
        /// Module 37 is numbered past the 36 of Analysis v1.0 but is an academic module, so it sits
        /// with them rather than trailing the platform section — the ordering the catalogue's own
        /// comment claims, asserted rather than trusted.
        /// </summary>
        [Fact]
        public void It_sits_with_the_academic_modules_rather_than_after_the_numbered_thirty_six()
        {
            var keys = Academics(Sidebar()).Items.Select(i => i.Key).ToList();

            Assert.Contains(LearningKey, keys);
            Assert.DoesNotContain(LearningKey, Sidebar().Single(n => n.Key == "platform").Items.Select(i => i.Key));
        }

        /// <summary>
        /// BR-SEC-010: a link that exists only to answer 404 is worse than no link. This is the
        /// behaviour that turned a missing grant into a missing feature — correct on its own terms,
        /// and worth pinning so the entry is never "fixed" by being made unconditional.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SEC-010")]
        public void It_goes_for_a_user_who_holds_no_learning_screen()
        {
            var academics = Academics(Sidebar(permitted: false));

            Assert.DoesNotContain(academics.Items, i => i.Key == LearningKey);
            // The group survives on its neighbours rather than collapsing with it.
            Assert.Contains(academics.Items, i => i.Key == "ATT");
        }

        /// <summary>BR-SET-006: and for a deployment that has switched the module off.</summary>
        [Fact]
        [BusinessRule("BR-SET-006")]
        public void It_goes_for_a_deployment_that_has_the_module_switched_off()
        {
            Assert.DoesNotContain(Academics(Sidebar(visible: false)).Items, i => i.Key == LearningKey);
        }

        /// <summary>
        /// The reported defect, second round: "لا توجد امتحانات وأوراق عمل ومواد
        /// إثرائية فقط واجبات". The worksheet half of it was reachable only by
        /// opening a lesson, so §8.2's library had no standing surface of its
        /// own. <c>Materials</c> is it, and this pins the route it answers on —
        /// a rename would otherwise leave the tab in <c>_LearningNav</c> pointing
        /// at nothing, which is the failure that put module 37 in the menu with
        /// no way in to begin with.
        /// </summary>
        [Fact]
        public void The_materials_library_answers_on_its_own_route()
        {
            var action = Assert.Single(
                typeof(LearningController).GetMethods(BindingFlags.Public | BindingFlags.Instance),
                m => m.Name == "Materials");

            var route = Assert.Single(action.GetCustomAttributes<HttpGetAttribute>());
            Assert.Equal("materials", route.Template);
        }

        /// <summary>
        /// It reads the per-lesson library's own screen rather than a new one.
        /// Same §8.2 rows read a second way, so a second catalogue entry would
        /// seed a permission granting nothing new (BR-SEC-010) and would let two
        /// surfaces over one table drift apart in who may read them.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SEC-010")]
        public void The_materials_library_is_gated_by_the_resource_library_screen()
        {
            var action = Assert.Single(
                typeof(LearningController).GetMethods(BindingFlags.Public | BindingFlags.Instance),
                m => m.Name == "Materials");

            var permission = Assert.Single(action.GetCustomAttributes<RequirePermissionAttribute>());

            Assert.Equal(
                new object[] { ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Resources, ActionVerb.View },
                permission.Arguments);
        }
    }
}
