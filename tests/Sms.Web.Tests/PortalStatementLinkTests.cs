using System;
using System.IO;
using System.Runtime.CompilerServices;
using Sms.TestSupport;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// BR-SEC-010 on the portal's one cross-screen link. The portal bar hides the tabs this account
    /// cannot open; the fees tab on the student profile links on to the same family statement, and
    /// that link is not part of the bar.
    /// <para>
    /// A student holds their own fee breakdown and deliberately not the family's money
    /// (doc 06 §4.3, asserted by <c>PermissionSeedContributorTests</c>), so for them the link
    /// answers a bare not-found — the disclosure BR-SEC-010 exists to prevent, and a page that to
    /// the parent reading over their shoulder simply looks broken. Nothing else fails when the
    /// guard is dropped: the view compiles, every other test passes, and the link renders.
    /// </para>
    /// </summary>
    public class PortalStatementLinkTests
    {
        [Fact]
        [BusinessRule("BR-SEC-010")]
        public void The_fees_tab_offers_the_family_statement_only_to_an_account_that_may_open_it()
        {
            var body = File.ReadAllText(Path.Combine(PortalViews, "Student.cshtml"));
            var link = body.IndexOf("asp-action=\"Statement\"", StringComparison.Ordinal);

            Assert.True(link >= 0, "Student.cshtml no longer links to the family statement at all.");

            // The guard sits on the same line, immediately ahead of the anchor.
            var lineStart = body.LastIndexOf('\n', link) + 1;
            var guarded = body.Substring(lineStart, link - lineStart);

            Assert.True(
                guarded.Contains("Model.CanOpenStatement", StringComparison.Ordinal),
                "The fees tab links to /portal/statement unconditionally. A student account cannot open that "
                + "screen, so the link hands them a blank 404 instead of not being offered (BR-SEC-010).");
        }

        private static string PortalViews
        {
            get
            {
                var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile())!, "..", ".."));
                return Path.Combine(repoRoot, "src", "Sms.Web", "Views", "Portal");
            }
        }

        private static string ThisFile([CallerFilePath] string path = "") => path;
    }
}
