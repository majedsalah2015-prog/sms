using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// Every screen in this product carries a help button, and this is what keeps that true.
    /// <para>
    /// The help panel is not decoration here. The product is bilingual, sold to schools that
    /// configure it themselves, and most of its screens enforce a numbered business rule that the
    /// screen alone cannot explain — why a field is disabled, why a status cannot be reversed, why
    /// a save was refused. That explanation lived in <c>docs/</c>, which the person actually
    /// filling in the form does not have open. <c>_HelpModal</c> puts it on the screen.
    /// </para>
    /// <para>
    /// Coverage of that kind decays silently: the twentieth screen gets built by copying the
    /// nineteenth, and if the nineteenth was one of the ones that never got a panel, the gap
    /// spreads. Nothing fails, nothing 500s, the screen simply has no explanation and nobody
    /// notices until a school asks. So the rule is enforced the same way screen permissions are —
    /// by a red build rather than a review.
    /// </para>
    /// <para>
    /// The scan reads the <c>.cshtml</c> sources rather than the compiled views, because what is
    /// being asserted is that the author wrote the partial into the page. Razor compiles the
    /// partial name to a string argument, so there is nothing in the emitted type to reflect over.
    /// </para>
    /// </summary>
    public class HelpCoverageTests
    {
        /// <summary>The marker every screen must render — see <c>Views/Shared/_HelpModal.cshtml</c>.</summary>
        private const string HelpPartial = "name=\"_HelpModal\"";

        /// <summary>
        /// Screens that deliberately have no help button, each with the reason it does not.
        /// <para>
        /// Keep this list short and keep it argued. "It was awkward to add" is not a reason; the
        /// three below are all pages whose entire content is already the explanation, so a modal
        /// would be a second copy of the sentence the user is looking at.
        /// </para>
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Exempt = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Shared/Error.cshtml"] = "An error page is itself the explanation; a help panel about how to read a failure is noise on top of a failure.",
            ["Account/AccessDenied.cshtml"] = "Exists to refuse, and says so in one sentence. There is no procedure to document — BR-SEC-010 hides what the user may not open, so reaching this page is already the exception.",
            ["Home/Privacy.cshtml"] = "A prose page. It is help, and a help button on it would open a panel explaining prose.",
            ["Help/Index.cshtml"] = "The user guide itself — the page every other screen's help button is the specific counterpart to. A panel here would open an explanation of an explanation.",
        };

        private static string ViewsRoot
        {
            get
            {
                // The test's own compile-time path, so the scan does not depend on the working
                // directory the runner happens to start in.
                var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile())!, "..", ".."));
                return Path.Combine(repoRoot, "src", "Sms.Web", "Views");
            }
        }

        private static string ThisFile([CallerFilePath] string path = "") => path;

        /// <summary>
        /// Every view a user can land on, repo-relative and forward-slashed. Files beginning with
        /// an underscore are partials and layouts — they are fragments of a screen, not a screen,
        /// and <c>_HelpModal</c> is one of them.
        /// </summary>
        private static IEnumerable<string> ScreenViews()
        {
            var root = ViewsRoot;
            foreach (var file in Directory.EnumerateFiles(root, "*.cshtml", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file).StartsWith("_", StringComparison.Ordinal))
                {
                    continue;
                }

                yield return Path.GetRelativePath(root, file).Replace('\\', '/');
            }
        }

        public static IEnumerable<object[]> AllScreenViews =>
            ScreenViews().Where(v => !Exempt.ContainsKey(v)).Select(v => new object[] { v }).ToList();

        [Fact]
        public void The_views_folder_is_where_this_test_thinks_it_is()
        {
            // Without this the suite would pass by finding nothing to check, which is the one
            // failure mode a coverage gate must not have.
            Assert.True(Directory.Exists(ViewsRoot), $"Views folder not found at '{ViewsRoot}'.");
            Assert.True(ScreenViews().Count() > 100, "Found suspiciously few views — the scan is probably pointed at the wrong folder.");
        }

        [Theory]
        [MemberData(nameof(AllScreenViews))]
        public void Every_screen_offers_help(string view)
        {
            var body = File.ReadAllText(Path.Combine(ViewsRoot, view));

            Assert.True(
                body.Contains(HelpPartial, StringComparison.Ordinal),
                $"{view} renders no help button. Author a HelpPanelViewModel in the view's @{{ }} block and render "
                + "<partial name=\"_HelpModal\" model=\"help\" /> in its page head — or, if the screen genuinely "
                + $"needs none, add it to {nameof(HelpCoverageTests)}.{nameof(Exempt)} with the reason why.");
        }

        [Fact]
        public void No_screen_is_exempted_that_no_longer_exists()
        {
            // A stale exemption is how a screen quietly loses its help button: the file is renamed,
            // the entry keeps matching nothing, and the new name is never checked.
            var present = ScreenViews().ToHashSet(StringComparer.OrdinalIgnoreCase);
            var stale = Exempt.Keys.Where(k => !present.Contains(k)).ToList();

            Assert.True(stale.Count == 0, $"Exempted views that no longer exist: {string.Join(", ", stale)}");
        }

        [Fact]
        public void No_screen_is_exempted_without_a_reason()
        {
            var unexplained = Exempt.Where(e => string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Key).ToList();

            Assert.True(unexplained.Count == 0, $"Exempted with no reason given: {string.Join(", ", unexplained)}");
        }
    }
}
