using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Routing;
using Sms.TestSupport;
using Sms.Web.Navigation;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// Student finance (doc/Modules/19 §8.7, student side) is a screen of the Fees module reached
    /// from its own Finance-group entry — the second place in the sidebar where two entries name one
    /// controller, after the assignment board.
    /// <para>
    /// The same two things are worth pinning as they were there: the entry has to obey both of the
    /// menu's filters even though it is not a module of its own, and the highlight has to pick it
    /// over the Fees module on its own screens rather than lighting both. It carries a third the
    /// board does not — two drill-downs (the breakdown and the statement) that render under it, and
    /// a clerk who opens a statement must not watch the menu jump to the charge explorer.
    /// </para>
    /// </summary>
    public class StudentFinanceNavigationTests
    {
        private const string EntryKey = "FEE-STUDENTS";

        private static IReadOnlyList<NavItem> Sidebar(
            bool canSeeStudentFinance = true, bool feesVisible = true) =>
            ModuleCatalog.BuildSidebar(
                m => feesVisible || m.Code != "FEE",
                _ => true,
                canExportToLedger: false,
                erpGroups: new List<NavItem>(),
                canSeeSectionBoard: false,
                canSeeRoles: false,
                canSeeUserRoles: false,
                canSeeStudentFinance: canSeeStudentFinance);

        private static NavItem Finance(IReadOnlyList<NavItem> sidebar) =>
            Assert.Single(sidebar, n => n.Key == "finance");

        private static RouteValueDictionary On(string action) =>
            new() { ["area"] = string.Empty, ["controller"] = "Fees", ["action"] = action };

        /// <summary>
        /// The entry the request was for: a Finance-menu line named for the students, sitting
        /// directly under the fees whose position it reads.
        /// </summary>
        [Fact]
        public void Student_finance_has_an_entry_under_finance_beside_the_fees()
        {
            var finance = Finance(Sidebar());

            var index = finance.Items.FindIndex(i => i.Key == EntryKey);
            Assert.True(index > 0, "student finance is missing from the Finance group");
            Assert.Equal("FEE", finance.Items[index - 1].Key);

            var entry = finance.Items[index];
            Assert.Equal("Fees", entry.Controller);
            Assert.Equal("StudentFinance", entry.Action);
            Assert.Equal("مالية الطلاب", entry.TitleAr);
            Assert.Equal("Student finance", entry.TitleEn);
        }

        /// <summary>
        /// BR-SEC-010: reading one family's position is not the right to browse the whole roll's, so
        /// the entry goes for a user who does not hold this screen — rather than showing a link that
        /// answers 404 after they have already told a parent it was coming.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SEC-010")]
        public void The_entry_goes_for_a_user_without_that_screen()
        {
            var finance = Finance(Sidebar(canSeeStudentFinance: false));

            Assert.DoesNotContain(finance.Items, i => i.Key == EntryKey);
            Assert.Contains(finance.Items, i => i.Key == "FEE");
        }

        /// <summary>
        /// BR-SET-006: it is a Fees screen wherever it is filed, so switching the module off takes it
        /// too. Giving it an entry of its own must not become a way for a screen of a disabled module
        /// to survive in the menu.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SET-006")]
        public void The_entry_goes_with_the_fees_module_when_it_is_switched_off()
        {
            var sidebar = Sidebar(feesVisible: false);

            var finance = Finance(sidebar);
            Assert.DoesNotContain(finance.Items, i => i.Key == EntryKey);
            Assert.DoesNotContain(finance.Items, i => i.Key == "FEE");
        }

        /// <summary>On its own screen the more specific of the two Fees entries wins.</summary>
        [Fact]
        public void The_roll_highlights_student_finance_and_not_the_fees_module()
        {
            var active = NavLinks.Resolve(Sidebar(), On("StudentFinance"));

            Assert.NotNull(active);
            Assert.Equal(EntryKey, active!.Key);
        }

        /// <summary>
        /// The breakdown and the statement are drill-downs of this entry, not screens of their own.
        /// Without the sibling actions the highlight would land on the charge explorer the moment a
        /// clerk opened either, and the Finance group would reshuffle under them mid-task.
        /// </summary>
        [Theory]
        [InlineData("StudentFinanceDetail")]
        [InlineData("StudentStatement")]
        public void The_drill_downs_keep_student_finance_highlighted(string action)
        {
            var active = NavLinks.Resolve(Sidebar(), On(action));

            Assert.NotNull(active);
            Assert.Equal(EntryKey, active!.Key);
        }

        /// <summary>
        /// And the other way round: the module keeps every screen of its controller that this entry
        /// does not name more precisely — the charge explorer, the structure workbench, the payer
        /// statement.
        /// </summary>
        [Theory]
        [InlineData("Index")]
        [InlineData("Structure")]
        [InlineData("Categories")]
        [InlineData("Position")]
        public void The_rest_of_the_module_still_highlights_the_module(string action)
        {
            var active = NavLinks.Resolve(Sidebar(), On(action));

            Assert.NotNull(active);
            Assert.Equal("FEE", active!.Key);
        }

        /// <summary>One entry lit means one group open, on the drill-downs as much as on the roll.</summary>
        [Fact]
        public void Only_one_group_stands_open_on_the_statement()
        {
            var sidebar = Sidebar();
            var active = NavLinks.Resolve(sidebar, On("StudentStatement"));

            var open = sidebar.Where(n => NavLinks.Contains(n, active)).Select(n => n.Key).ToList();

            Assert.Equal(new[] { "finance" }, open);
        }
    }
}
