using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// A create form that carries a photograph has to post as multipart, and this is what keeps
    /// that true.
    /// <para>
    /// The failure it guards is silent. Drop <c>enctype="multipart/form-data"</c> from a form
    /// holding a file input and the browser still submits, the screen still says the person was
    /// registered, the flash message is still green — and the photograph never left the machine.
    /// Nothing throws, no test fails, and the gap is found by whoever prints the ID cards weeks
    /// later. Two screens now depend on that one attribute, so it is asserted rather than
    /// remembered.
    /// </para>
    /// <para>
    /// The scan reads the <c>.cshtml</c> sources: Razor turns the attribute into literal markup,
    /// so there is nothing in the compiled view to reflect over.
    /// </para>
    /// </summary>
    public class PhotoUploadFormTests
    {
        private const string FileInput = "type=\"file\"";

        private const string Multipart = "enctype=\"multipart/form-data\"";

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
        /// Views that put a file input inside a form of their own — partials included, since
        /// <c>_PhotoPanel</c> posts its own form and would fail the same way.
        /// </summary>
        private static IEnumerable<string> ViewsWithAFileInput()
        {
            var root = ViewsRoot;
            foreach (var file in Directory.EnumerateFiles(root, "*.cshtml", SearchOption.AllDirectories))
            {
                var body = File.ReadAllText(file);
                if (body.Contains(FileInput, StringComparison.Ordinal) && body.Contains("<form", StringComparison.Ordinal))
                {
                    yield return Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
                }
            }
        }

        public static IEnumerable<object[]> AllUploadViews =>
            ViewsWithAFileInput().Select(v => new object[] { v }).ToList();

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
    }
}
