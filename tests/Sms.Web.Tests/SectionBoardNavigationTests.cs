using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Routing;
using Sms.TestSupport;
using Sms.Web.Navigation;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The assignment board (doc/Modules/06 §8.3) is a screen of the Sections module reached from a
    /// People-side entry, which is the first time the sidebar holds two entries naming one
    /// controller. That is worth its own tests twice over: the entry has to obey both of the menu's
    /// filters even though it is not a module, and the highlight has to pick one of the two rather
    /// than lighting both and standing two groups open at once.
    /// </summary>
    public class SectionBoardNavigationTests
    {
        private const string BoardKey = "SEC-BOARD";

        private static IReadOnlyList<NavItem> Sidebar(
            bool canSeeSectionBoard = true, bool sectionsVisible = true) =>
            ModuleCatalog.BuildSidebar(
                m => sectionsVisible || m.Code != "SEC",
                _ => true,
                canExportToLedger: false,
                erpGroups: new List<NavItem>(),
                canSeeSectionBoard: canSeeSectionBoard);

        private static NavItem People(IReadOnlyList<NavItem> sidebar) =>
            Assert.Single(sidebar, n => n.Key == "people");

        private static RouteValueDictionary On(string action) =>
            new() { ["area"] = string.Empty, ["controller"] = "Sections", ["action"] = action };

        /// <summary>
        /// The board had no link anywhere in the menu: the only way to it was to type
        /// /sections/board. It now sits under People, directly beneath the students whose
        /// distribution it is.
        /// </summary>
        [Fact]
        public void The_board_has_an_entry_under_people_beside_the_students()
        {
            var people = People(Sidebar());

            var index = people.Items.FindIndex(i => i.Key == BoardKey);
            Assert.True(index > 0, "the assignment board is missing from the People group");
            Assert.Equal("STU", people.Items[index - 1].Key);

            var board = people.Items[index];
            Assert.Equal("Sections", board.Controller);
            Assert.Equal("Board", board.Action);
            Assert.Equal("لوحة توزيع الطلاب", board.TitleAr);
            Assert.Equal("Assignment board", board.TitleEn);
        }

        /// <summary>
        /// BR-SEC-010: being able to open the Sections list is not the right to redistribute a
        /// grade, so the entry goes for a user who does not hold the board's own screen permission —
        /// rather than showing a link that answers 404.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SEC-010")]
        public void The_board_entry_goes_for_a_user_without_that_screen()
        {
            var people = People(Sidebar(canSeeSectionBoard: false));

            Assert.DoesNotContain(people.Items, i => i.Key == BoardKey);
            Assert.Contains(people.Items, i => i.Key == "STU");
        }

        /// <summary>
        /// BR-SET-006: the board is Sections' screen wherever it is filed, so switching the module
        /// off has to take it too. Filing it under People must not become a way for a screen of a
        /// disabled module to survive in the menu.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SET-006")]
        public void The_board_entry_goes_with_the_sections_module_when_it_is_switched_off()
        {
            var sidebar = Sidebar(sectionsVisible: false);

            Assert.DoesNotContain(People(sidebar).Items, i => i.Key == BoardKey);
            var structure = Assert.Single(sidebar, n => n.Key == "structure");
            Assert.DoesNotContain(structure.Items, i => i.Key == "SEC");
        }

        /// <summary>
        /// The whole reason the highlight is resolved over the tree rather than asked of each entry:
        /// on the board, the more specific of the two Sections entries wins.
        /// </summary>
        [Fact]
        public void The_board_screen_highlights_the_board_and_not_the_sections_module()
        {
            var active = NavLinks.Resolve(Sidebar(), On("Board"));

            Assert.NotNull(active);
            Assert.Equal(BoardKey, active!.Key);
        }

        /// <summary>
        /// Proposing a distribution renders the board back rather than redirecting, so the entry
        /// stays lit through it — the sidebar must not jump to another group mid-decision.
        /// </summary>
        [Fact]
        public void A_proposal_keeps_the_board_highlighted()
        {
            var active = NavLinks.Resolve(Sidebar(), On("Propose"));

            Assert.NotNull(active);
            Assert.Equal(BoardKey, active!.Key);
        }

        /// <summary>
        /// And the other way round: the module keeps its own list, and keeps every other screen of
        /// its controller that no entry names more precisely.
        /// </summary>
        [Theory]
        [InlineData("Index")]
        [InlineData("Details")]
        [InlineData("CloseWizard")]
        public void The_rest_of_the_module_still_highlights_the_module(string action)
        {
            var active = NavLinks.Resolve(Sidebar(), On(action));

            Assert.NotNull(active);
            Assert.Equal("SEC", active!.Key);
        }

        /// <summary>
        /// One entry lit means one group open. Both of these entries live in groups, and before the
        /// highlight was resolved over the tree the board screen would have stood both of them open.
        /// </summary>
        [Fact]
        public void Only_one_group_stands_open_on_the_board()
        {
            var sidebar = Sidebar();
            var active = NavLinks.Resolve(sidebar, On("Board"));

            var open = sidebar.Where(n => NavLinks.Contains(n, active)).Select(n => n.Key).ToList();

            Assert.Equal(new[] { "people" }, open);
        }
    }
}
