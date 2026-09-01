using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// A refused save on the parent screens gives the page back with everything that was typed still
    /// on it, and this is what keeps that true.
    /// <para>
    /// These are the two most refused forms in the people modules. The identity tab holds a mobile
    /// unique across the register and an ID number beside it (BR-PAR-002), and will not let either
    /// name change without a written reason (BR-PAR-009); the residence picker refuses a quarter
    /// that has no locality under it, or one belonging to a different locality. For a while every
    /// one of those refusals answered with a redirect, which reloaded the stored row and threw the
    /// correction away — a registrar retyping a whole family's details because the reason box was
    /// empty, or reopening a three-level picker from the top for choosing the levels out of order.
    /// </para>
    /// <para>
    /// The mechanism that fixes it is small and easy to undo by copying an older screen: the
    /// controller re-renders instead of redirecting, and each view binds its fields to what was
    /// submitted rather than to what is stored. Both halves are asserted here because a screen with
    /// only one of them still looks right on a normal load — the loss shows only on the refusal, on
    /// the day a school meets it.
    /// </para>
    /// </summary>
    public class ParentRefusalRedrawTests
    {
        /// <summary>
        /// A value read off the stored entity inside the identity form. <c>@p.Id</c> is allowed
        /// through: it addresses the row the form posts to, and is not something anybody types.
        /// </summary>
        private static readonly Regex StoredRow = new(@"@\(?p\.(?!Id\b)", RegexOptions.Compiled);

        private static string Repo([CallerFilePath] string thisFile = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

        private static string Source(params string[] parts) =>
            File.ReadAllText(Path.Combine(Repo(), Path.Combine(parts)));

        /// <summary>The identity tab's edit form, from its opening tag to its closing one.</summary>
        private static string IdentityForm()
        {
            var source = Source("src", "Sms.Web", "Views", "Parents", "File.cshtml");
            var start = source.IndexOf("asp-action=\"Edit\"", StringComparison.Ordinal);
            Assert.True(start > 0, "Views/Parents/File.cshtml no longer contains the identity edit form.");
            var open = source.LastIndexOf("<form", start, StringComparison.Ordinal);
            var close = source.IndexOf("</form>", start, StringComparison.Ordinal);
            return source[open..close];
        }

        /// <summary>
        /// One method of the controller, braces counted from its signature. Balanced-brace counting
        /// is enough here because neither method holds a brace inside a string literal.
        /// </summary>
        private static string ControllerMethod(string signature)
        {
            var source = Source("src", "Sms.Web", "Controllers", "ParentsController.cs");
            var start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.True(start > 0, $"ParentsController no longer declares '{signature}'.");

            var open = source.IndexOf('{', start);
            var depth = 0;
            for (var i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0) return source[open..(i + 1)];
            }

            throw new InvalidOperationException($"Unbalanced braces after '{signature}'.");
        }

        [Fact]
        public void The_identity_form_draws_its_values_from_the_submission_not_the_stored_row()
        {
            var match = StoredRow.Match(IdentityForm());
            Assert.False(
                match.Success,
                $"The identity form binds an input to the stored parent ('{match.Value}'). It must bind to 'f' — " +
                "the submitted values when a save was refused, the stored row otherwise — or a refusal will " +
                "erase everything the user typed.");
        }

        [Fact]
        public void The_identity_form_is_redrawn_from_the_rejected_submission()
        {
            Assert.Contains("Model.Submitted", Source("src", "Sms.Web", "Views", "Parents", "File.cshtml"), StringComparison.Ordinal);
            Assert.Contains("@f.Reason", IdentityForm(), StringComparison.Ordinal);
        }

        [Fact]
        public void The_residence_picker_keeps_the_reason_that_was_typed_with_the_refused_selection()
        {
            Assert.Contains(
                "value=\"@Model.SubmittedReason\"",
                Source("src", "Sms.Web", "Views", "Parents", "Residence.cshtml"),
                StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("public async Task<IActionResult> Edit(int id, ParentFormViewModel form)")]
        [InlineData("public async Task<IActionResult> SaveResidence(")]
        public void A_refused_save_renders_the_screen_again_rather_than_redirecting_away_from_it(string signature)
        {
            Assert.Contains(
                "return View(nameof(",
                ControllerMethod(signature),
                StringComparison.Ordinal);
        }
    }
}
