using System;
using System.IO;
using System.Runtime.CompilerServices;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.TestSupport;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The portal's own bar, for module 37's content half.
    /// <para>
    /// The reported defect was "بوابة الطالب لا يظهر سوى الواجبات" — the portal showed nothing of
    /// e-learning but the homework. It was accurate: the module's three family-facing surfaces are
    /// the lesson plan, the material filed against it, and the work set from it, and only the last
    /// had a page. doc/Modules/37 §5 gives the student "read content" in the portal, §1 puts
    /// content "surfaced through the portal", BR-LRN-003 makes publication "the event families
    /// see", and <c>Lesson.PublishedAtUtc</c> documents itself as the moment a lesson "becomes
    /// visible in the portal". §8's numbered screen list enumerates only "my work" and "my
    /// sitting", which is how the half with a number got built and the half without one did not.
    /// </para>
    /// <para>
    /// These pin the tab into the bar, and — the part that actually decided whether anybody would
    /// ever see it — pin it to the permission gate rather than rendering unconditionally
    /// (BR-SEC-010: a tab that answers not-found is the disclosure the rule exists to prevent).
    /// </para>
    /// </summary>
    public class PortalLessonsNavigationTests
    {
        [Fact]
        public void The_portal_bar_offers_my_lessons()
        {
            var bar = File.ReadAllText(Path.Combine(SharedViews, "_PortalLayout.cshtml"));

            Assert.True(
                bar.Contains("ScreenCatalog.Portal.Lessons", StringComparison.Ordinal),
                "The portal bar has no \"my lessons\" tab, so the published lesson plans a teacher "
                + "files against an offering reach no family (doc/Modules/37 §5).");
            Assert.Contains("دروسي", bar, StringComparison.Ordinal);
            Assert.Contains("My lessons", bar, StringComparison.Ordinal);
        }

        /// <summary>
        /// Every tab in the bar goes through one permission check. Asserted on the shape of the
        /// loop rather than tab by tab, because the failure this guards against is a tab added
        /// outside the collection the loop filters.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SEC-010")]
        public void Every_portal_tab_is_hidden_from_an_account_that_cannot_open_it()
        {
            var bar = File.ReadAllText(Path.Combine(SharedViews, "_PortalLayout.cshtml"));

            var filter = bar.IndexOf("visibleTabs.Add(tab)", StringComparison.Ordinal);
            Assert.True(filter >= 0, "The portal bar no longer filters its tabs by permission at all.");

            var check = bar.IndexOf("Permissions.HasPermissionAsync(ScreenCatalog.Modules.Portal, tab.Screen", StringComparison.Ordinal);
            Assert.True(check >= 0 && check < filter,
                "A portal tab is rendered without asking whether this account holds its screen (BR-SEC-010).");

            // The rendering loop reads visibleTabs, never the full table.
            var loop = bar.IndexOf("foreach (var t in visibleTabs)", StringComparison.Ordinal);
            Assert.True(loop > filter, "The portal bar renders its unfiltered tab table.");
        }

        /// <summary>
        /// The screen is catalogued read-only. Module 37 gives a family no verb over content —
        /// reading the lesson and downloading its material are both View — and a catalogued verb no
        /// action answers is a grant that opens nothing.
        /// </summary>
        [Fact]
        public void My_lessons_is_catalogued_read_only_in_the_portal_permission_space()
        {
            Assert.True(ScreenCatalog.Defines(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Lessons, ActionVerb.View));

            foreach (var verb in new[] { ActionVerb.Create, ActionVerb.Edit, ActionVerb.Deactivate, ActionVerb.Approve, ActionVerb.Submit })
            {
                Assert.False(
                    ScreenCatalog.Defines(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Lessons, verb),
                    $"POR/Lessons catalogues {verb}, which no action behind it answers.");
            }
        }

        private static string SharedViews
        {
            get
            {
                var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile())!, "..", ".."));
                return Path.Combine(repoRoot, "src", "Sms.Web", "Views", "Shared");
            }
        }

        private static string ThisFile([CallerFilePath] string path = "") => path;
    }
}
