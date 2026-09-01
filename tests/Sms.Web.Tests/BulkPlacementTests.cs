using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
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
    /// Bulk placement (doc/Modules/10 §8, BR-STU-010): the step between registering an intake and
    /// everything the year does with it.
    /// <para>
    /// It exists because the register import creates students and no enrollments, and every
    /// year-scoped screen in the product — the fee roll, the section board, attendance, the charge
    /// pickers — reads through <c>Enrollment</c>. A school that imported 481 children and enrolled 8
    /// of them saw a fee roll of 8 with nothing saying why, and read the system as broken.
    /// </para>
    /// <para>
    /// What is pinned here is the wiring rather than the rules: the rules are BR-GLB-024's and
    /// BR-SCN-002/003's, enforced in the ports this screen calls and tested there. What can only be
    /// got wrong here is which permission each of the three requests demands, and whether the menu
    /// entry obeys both of the sidebar's filters.
    /// </para>
    /// </summary>
    public class BulkPlacementTests
    {
        private const string EntryKey = "STU-PLACE";

        private static IReadOnlyList<NavItem> Sidebar(bool canSeeBulkPlacement = true, bool studentsVisible = true) =>
            ModuleCatalog.BuildSidebar(
                m => studentsVisible || m.Code != "STU",
                _ => true,
                canExportToLedger: false,
                erpGroups: new List<NavItem>(),
                canSeeSectionBoard: false,
                canSeeRoles: false,
                canSeeUserRoles: false,
                canSeeStudentFinance: false,
                canSeeBulkPlacement: canSeeBulkPlacement);

        private static NavItem People(IReadOnlyList<NavItem> sidebar) =>
            Assert.Single(sidebar, n => n.Key == "people");

        private static RouteValueDictionary On(string action) =>
            new() { ["area"] = string.Empty, ["controller"] = "Students", ["action"] = action };

        private static MethodInfo Action(string name) =>
            Assert.Single(typeof(StudentsController).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == name));

        /// <summary>The attribute keeps its three values in <c>Arguments</c>, so they are read positionally.</summary>
        private static (string Module, string Screen, ActionVerb Verb) Permission(string action) =>
            Assert.Single(Action(action).GetCustomAttributes<RequirePermissionAttribute>()
                .Select(a => ((string)a.Arguments![0], (string)a.Arguments[1], (ActionVerb)a.Arguments[2])));

        // ---------------------------------------------------------------- the menu

        /// <summary>A People-menu line directly under the students it places.</summary>
        [Fact]
        public void Bulk_placement_has_an_entry_under_people_beside_the_students()
        {
            var people = People(Sidebar());

            var index = people.Items.FindIndex(i => i.Key == EntryKey);
            Assert.True(index > 0, "bulk placement is missing from the People group");
            Assert.Equal("STU", people.Items[index - 1].Key);

            var entry = people.Items[index];
            Assert.Equal("Students", entry.Controller);
            Assert.Equal("BulkPlacement", entry.Action);
            Assert.Equal("الإسناد الجماعي", entry.TitleAr);
            Assert.Equal("Bulk placement", entry.TitleEn);
        }

        /// <summary>
        /// BR-SEC-010: reading the student directory is not the right to enrol a year group, so the
        /// entry disappears for a user who does not hold this screen rather than offering a link
        /// that answers 404.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SEC-010")]
        public void The_entry_goes_for_a_user_without_that_screen()
        {
            var people = People(Sidebar(canSeeBulkPlacement: false));

            Assert.DoesNotContain(people.Items, i => i.Key == EntryKey);
            Assert.Contains(people.Items, i => i.Key == "STU");
        }

        /// <summary>
        /// BR-SET-006: it is a Students screen wherever it is filed, so switching the module off
        /// takes it too. An entry of its own must not become a way for a screen of a disabled
        /// module to survive in the menu.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SET-006")]
        public void The_entry_goes_with_the_students_module_when_it_is_switched_off()
        {
            var sidebar = Sidebar(studentsVisible: false);

            var people = People(sidebar);
            Assert.DoesNotContain(people.Items, i => i.Key == EntryKey);
            Assert.DoesNotContain(people.Items, i => i.Key == "STU");
        }

        /// <summary>On its own screen the more specific of the two Students entries wins.</summary>
        [Fact]
        public void The_screen_highlights_bulk_placement_and_not_the_students_module()
        {
            var active = NavLinks.Resolve(Sidebar(), On("BulkPlacement"));

            Assert.NotNull(active);
            Assert.Equal(EntryKey, active!.Key);
        }

        /// <summary>
        /// The dry run renders the screen back rather than redirecting, so the entry has to own that
        /// action too — otherwise the highlight leaves the group at the moment the reader is deciding
        /// whether to commit.
        /// </summary>
        [Fact]
        public void The_dry_run_keeps_bulk_placement_highlighted()
        {
            var active = NavLinks.Resolve(Sidebar(), On("BulkPlacementPreview"));

            Assert.NotNull(active);
            Assert.Equal(EntryKey, active!.Key);
        }

        /// <summary>
        /// And the other way round: the directory, the file and one child's placement stay with the
        /// module, which is where a reader who opened them started.
        /// </summary>
        [Theory]
        [InlineData("Index")]
        [InlineData("File")]
        [InlineData("Placement")]
        public void The_rest_of_the_module_still_highlights_the_module(string action)
        {
            var active = NavLinks.Resolve(Sidebar(), On(action));

            Assert.NotNull(active);
            Assert.Equal("STU", active!.Key);
        }

        // ---------------------------------------------------------------- the permissions

        /// <summary>
        /// Reading the roll and previewing a run are the same right, and it is not the write. A
        /// registrar can be shown who is unplaced before anyone hands them the ability to place
        /// them — which is what makes the count usable as a report.
        /// </summary>
        [Theory]
        [InlineData(nameof(StudentsController.BulkPlacement))]
        [InlineData(nameof(StudentsController.BulkPlacementPreview))]
        [BusinessRule("BR-GLB-070")]
        public void Reading_and_previewing_ask_for_the_enrollment_screen_view(string action)
        {
            var required = Permission(action);

            Assert.Equal(ScreenCatalog.Modules.Students, required.Module);
            Assert.Equal(ScreenCatalog.Students.Enrollment, required.Screen);
            Assert.Equal(ActionVerb.View, required.Verb);
        }

        /// <summary>
        /// The commit is the write, and it asks for exactly the right the single placement form asks
        /// for. Doing three hundred children at once must not be a cheaper permission than doing one.
        /// </summary>
        [Fact]
        [BusinessRule("BR-GLB-070")]
        public void Committing_asks_for_the_same_right_as_enrolling_one_child()
        {
            var bulk = Permission(nameof(StudentsController.BulkPlacementCommit));
            var single = Permission(nameof(StudentsController.Enroll));

            Assert.Equal(ActionVerb.Create, bulk.Verb);
            Assert.Equal(single.Module, bulk.Module);
            Assert.Equal(single.Screen, bulk.Screen);
            Assert.Equal(single.Verb, bulk.Verb);
        }

        /// <summary>Both writes are forms, and both carry the token. A bulk enrollment by forged post is a whole year group.</summary>
        [Theory]
        [InlineData(nameof(StudentsController.BulkPlacementPreview))]
        [InlineData(nameof(StudentsController.BulkPlacementCommit))]
        public void The_posted_requests_validate_the_antiforgery_token(string action)
        {
            Assert.Single(Action(action).GetCustomAttributes<ValidateAntiForgeryTokenAttribute>());
        }

        /// <summary>
        /// The catalogue has to define the verb the screen asks for, or the filter never matches and
        /// the screen is unreachable for everyone including the system administrator — silently, and
        /// looking like a data problem.
        /// </summary>
        [Theory]
        [InlineData(ActionVerb.View)]
        [InlineData(ActionVerb.Create)]
        public void The_enrollment_screen_defines_both_verbs(ActionVerb verb)
        {
            Assert.True(ScreenCatalog.Defines(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Enrollment, verb));
        }

        // ---------------------------------------------------------------- the routes

        private static string Route(string action) =>
            Assert.Single(Action(action).GetCustomAttributes<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>()).Template!;

        /// <summary>
        /// The bulk screen sits at <c>students/placement</c> beside one child's
        /// <c>students/{id}/placement</c>. The int constraint on the single one is what keeps the two
        /// apart; losing it would make the word "placement" ambiguous and route by accident.
        /// </summary>
        [Fact]
        public void The_bulk_screen_and_one_childs_placement_do_not_collide()
        {
            Assert.Equal("placement", Route(nameof(StudentsController.BulkPlacement)));
            Assert.Equal("{id:int}/placement", Route(nameof(StudentsController.Placement)));
            Assert.Equal("placement/preview", Route(nameof(StudentsController.BulkPlacementPreview)));
            Assert.Equal("placement/commit", Route(nameof(StudentsController.BulkPlacementCommit)));
        }
    }
}
