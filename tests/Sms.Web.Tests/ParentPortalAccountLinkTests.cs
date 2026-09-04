using System;
using System.IO;
using System.Runtime.CompilerServices;
using Sms.TestSupport;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// doc/Modules/11 §8.2's portal-account tab, and BR-SEC-010 on the two links it offers into
    /// Module 36. The tab has always named System Administration as the place a parent's login is
    /// made and managed; it now takes the clerk there, which only helps while the offer is made to
    /// the clerks who may actually open those screens.
    /// <para>
    /// Neither link belongs to the sidebar, so nothing else hides it: dropping the guard leaves a
    /// view that compiles, a page that renders, and a parents clerk with no accounts grant clicking
    /// through to a bare not-found — the posture BR-SEC-010 exists to avoid. The guards are asserted
    /// against the view's source because that is where they can be deleted by accident.
    /// </para>
    /// </summary>
    public class ParentPortalAccountLinkTests
    {
        [Fact]
        [BusinessRule("BR-SEC-010")]
        public void Creating_the_account_is_offered_only_to_a_user_who_may_provision_one()
            => AssertGuarded(
                "asp-action=\"NewUser\"",
                "Model.CanProvisionAccount",
                "The portal tab links to /security/users/new unconditionally. SYS/Users/Create is a "
                + "separate grant from the parent file, so an unauthorized clerk is handed a 404 "
                + "instead of not being offered the link (BR-SEC-010).");

        [Fact]
        [BusinessRule("BR-SEC-010")]
        public void The_existing_account_is_offered_only_to_a_user_who_may_open_the_directory()
            => AssertGuarded(
                "asp-action=\"Users\"",
                "Model.CanOpenAccounts",
                "The portal tab links to /security/users unconditionally. SYS/UserRoles/View is a "
                + "separate grant from the parent file, so an unauthorized clerk is handed a 404 "
                + "instead of not being offered the link (BR-SEC-010).");

        private static void AssertGuarded(string marker, string flag, string because)
        {
            var body = File.ReadAllText(Path.Combine(ParentViews, "File.cshtml"));
            var anchor = body.IndexOf(marker, StringComparison.Ordinal);

            Assert.True(anchor >= 0, $"File.cshtml no longer carries a link with {marker} at all.");

            // The guard is the nearest @if opening ahead of the anchor: the link is written inside it.
            var guardStart = body.LastIndexOf("@if (", anchor, StringComparison.Ordinal);

            Assert.True(guardStart >= 0, because);
            Assert.True(body.Substring(guardStart, anchor - guardStart).Contains(flag, StringComparison.Ordinal), because);
        }

        private static string ParentViews
        {
            get
            {
                var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile())!, "..", ".."));
                return Path.Combine(repoRoot, "src", "Sms.Web", "Views", "Parents");
            }
        }

        private static string ThisFile([CallerFilePath] string path = "") => path;
    }
}
