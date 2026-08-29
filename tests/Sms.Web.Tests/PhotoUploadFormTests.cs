using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// Two ways an upload form fails without anyone noticing, both asserted here because both have
    /// already happened in this product.
    /// <para>
    /// The first is <c>enctype</c>. Drop <c>multipart/form-data</c> from a form holding a file
    /// input and the browser still submits, the screen still says the person was registered, the
    /// flash message is still green — and the photograph never left the machine.
    /// </para>
    /// <para>
    /// The second is the antiforgery token, and it is worse because it looks like nothing at all.
    /// Writing <c>action="…"</c> on a form switches the tag helper's antiforgery default off, so no
    /// hidden token is emitted; every post to a <c>[ValidateAntiForgeryToken]</c> action then comes
    /// back 400 and the screen simply reloads unchanged. That is exactly how the photo panel
    /// silently refused every photograph it was ever given.
    /// </para>
    /// <para>
    /// The scan reads the <c>.cshtml</c> sources: Razor turns these into literal markup, so there
    /// is nothing in the compiled view to reflect over.
    /// </para>
    /// </summary>
    public class PhotoUploadFormTests
    {
        private const string FileInput = "type=\"file\"";

        /// <summary>The shared upload widget carries the file input for the screens that use it.</summary>
        private const string UploadPartial = "_FileUploadField";

        private const string Multipart = "enctype=\"multipart/form-data\"";

        /// <summary>A form written with a bare action attribute, which is the shape that loses its token.</summary>
        private static readonly Regex HandWrittenPostForm = new(
            "<form[^>]*\\bmethod=\"post\"[^>]*>|<form[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

        private static IEnumerable<string> AllViews()
        {
            foreach (var file in Directory.EnumerateFiles(ViewsRoot, "*.cshtml", SearchOption.AllDirectories))
            {
                yield return Path.GetRelativePath(ViewsRoot, file).Replace(Path.DirectorySeparatorChar, '/');
            }
        }

        /// <summary>
        /// Views that put a file into a form of their own — whether by writing the input directly
        /// or by embedding the shared upload widget. Partials included, since <c>_PhotoPanel</c>
        /// posts its own form and would fail the same way.
        /// </summary>
        private static IEnumerable<string> ViewsWithAFileInput()
        {
            foreach (var view in AllViews())
            {
                var body = File.ReadAllText(Path.Combine(ViewsRoot, view));
                var takesAFile = body.Contains(FileInput, StringComparison.Ordinal)
                                 || body.Contains(UploadPartial, StringComparison.Ordinal);
                if (takesAFile && body.Contains("<form", StringComparison.Ordinal))
                {
                    yield return view;
                }
            }
        }

        public static IEnumerable<object[]> AllUploadViews =>
            ViewsWithAFileInput().Select(v => new object[] { v }).ToList();

        public static IEnumerable<object[]> AllViewFiles =>
            AllViews().Select(v => new object[] { v }).ToList();

        [Fact]
        public void The_screens_that_take_a_photograph_are_where_this_test_thinks_they_are()
        {
            // Without this the suite would pass by finding nothing to check, which is the one
            // failure mode a coverage gate must not have.
            var views = ViewsWithAFileInput().ToList();
            Assert.Contains("Employees/Register.cshtml", views);
            Assert.Contains("Students/Register.cshtml", views);
            Assert.Contains("Shared/_PhotoPanel.cshtml", views);
        }

        [Theory]
        [MemberData(nameof(AllUploadViews))]
        public void A_form_holding_a_file_input_posts_as_multipart(string view)
        {
            var body = File.ReadAllText(Path.Combine(ViewsRoot, view));

            Assert.True(
                body.Contains(Multipart, StringComparison.Ordinal),
                $"{view} has a file input but no form declaring {Multipart}. The browser will post the "
                + "form without the file and the screen will report success — add the attribute to the "
                + "form that contains the input.");
        }

        /// <summary>
        /// Every post form in the product, not only the ones with files: the tag helper's rule is
        /// about the <c>action</c> attribute, and a form that loses its token loses every post it
        /// ever makes.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllViewFiles))]
        public void A_post_form_written_with_its_own_action_still_asks_for_the_antiforgery_token(string view)
        {
            var body = File.ReadAllText(Path.Combine(ViewsRoot, view));

            foreach (Match tag in HandWrittenPostForm.Matches(body))
            {
                var open = tag.Value;
                if (!open.Contains("method=\"post\"", StringComparison.OrdinalIgnoreCase)) { continue; }

                // asp-action and friends leave the tag helper in charge, and it emits the token.
                // A literal action attribute takes that job away without saying so.
                if (!Regex.IsMatch(open, "(?<!asp-)\\baction=\"", RegexOptions.IgnoreCase)) { continue; }

                Assert.True(
                    open.Contains("asp-antiforgery=\"true\"", StringComparison.OrdinalIgnoreCase),
                    $"{view} posts to a hand-written action without asp-antiforgery=\"true\":\n  {open.Trim()}\n"
                    + "Razor switches antiforgery off when it sees a literal action attribute, so this form "
                    + "posts no token and a [ValidateAntiForgeryToken] action answers 400 with no visible "
                    + "error. Either use asp-action/asp-controller, or add asp-antiforgery=\"true\".");
            }
        }
    }
}
