using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// doc/Modules/13 §8.1 — the teacher directory's designation panel picks an employee out of a
    /// list that is the whole active non-teaching staff, three hundred people in a real school.
    /// A plain <c>&lt;select&gt;</c> that long is scrolled, not chosen from, so the panel carries a
    /// filter box beside it.
    /// <para>
    /// The filter runs in the browser, over the options the page was given, which makes three
    /// things load-bearing and invisible to every other test: the box and the picker must agree on
    /// the picker's id, each option must carry the searchable text (both languages' names, so an
    /// Arabic reader can still type a Latin one), and a filtered-out option must be disabled rather
    /// than merely hidden — a hidden option is still submittable in some browsers, and designating
    /// the employee the filter just ruled out is the one outcome worse than no filter at all.
    /// </para>
    /// <para>
    /// Razor turns the view into literal markup, so the assertions read the <c>.cshtml</c> source —
    /// the same approach <see cref="PhotoUploadFormTests"/> takes for the same reason.
    /// </para>
    /// </summary>
    public class TeacherDesignatePickerTests
    {
        private static string ThisFile([CallerFilePath] string path = "") => path;

        private static string View
        {
            get
            {
                var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile())!, "..", ".."));
                return File.ReadAllText(Path.Combine(repoRoot, "src", "Sms.Web", "Views", "Teachers", "Index.cshtml"));
            }
        }

        [Fact]
        public void The_filter_box_names_the_picker_it_narrows()
        {
            var body = View;

            Assert.Contains("id=\"designate-filter\"", body, StringComparison.Ordinal);
            Assert.Contains("id=\"designate-employee\"", body, StringComparison.Ordinal);
            Assert.Contains("aria-controls=\"designate-employee\"", body, StringComparison.Ordinal);
            Assert.Contains("getElementById('designate-filter')", body, StringComparison.Ordinal);
            Assert.Contains("getElementById('designate-employee')", body, StringComparison.Ordinal);
        }

        [Fact]
        public void The_picker_still_posts_the_employee_id()
        {
            // The filter must not have turned a referential field into free text (BR-GLB-112): the
            // form still posts the id the Designate action binds, not a typed name.
            Assert.Matches(new Regex("<select[^>]*id=\"designate-employee\"[^>]*name=\"employeeId\""), View);
        }

        [Fact]
        public void Every_employee_option_carries_both_languages_of_the_name_to_search()
        {
            var body = View;

            // The option shows one language; data-s holds the number and both, so "Khalid" finds
            // خالد in an Arabic session and خالد finds him in an English one.
            Assert.Contains("data-s=\"@($\"{e.EmployeeNo} {e.FirstNameAr} {e.FatherNameAr} {e.FamilyNameAr} {e.FirstNameEn} {e.FatherNameEn} {e.FamilyNameEn}\")\"", body, StringComparison.Ordinal);
            Assert.Contains("getAttribute('data-s')", body, StringComparison.Ordinal);
        }

        [Fact]
        public void A_filtered_out_option_is_disabled_and_never_left_selected()
        {
            var body = View;

            Assert.Contains(".el.hidden = !hit;", body, StringComparison.Ordinal);
            Assert.Contains(".el.disabled = !hit;", body, StringComparison.Ordinal);
            Assert.Contains("if (!current || current.disabled) { picker.selectedIndex = 0; }", body, StringComparison.Ordinal);
        }

        [Fact]
        public void Arabic_spelling_variants_and_arabic_digits_are_folded_before_comparing()
        {
            var body = View;

            // أحمد typed as احمد, فاطمة as فاطمه, ليلى as ليلي, and ٢٤ as 24 — all one person or
            // one number. Without the fold the filter answers "nobody" about a name in the list.
            Assert.Contains("function fold(s)", body, StringComparison.Ordinal);
            Assert.Contains("0x0630", body, StringComparison.Ordinal);   // Arabic-Indic digits → ASCII
            Assert.Contains("[آأإٱ]", body, StringComparison.Ordinal);
            Assert.Contains("ة", body, StringComparison.Ordinal);
            Assert.Contains("[ىئ]", body, StringComparison.Ordinal);
        }

        [Fact]
        public void The_count_and_the_empty_result_are_written_in_both_languages()
        {
            var body = View;

            // The readout is built in script, where the view's own T() helper cannot reach — so the
            // Arabic has to be sitting there beside the English (the house rule, and the reason
            // BilingualValidationTests exists for the server side of the same problem).
            Assert.Contains("No employee matches this filter.", body, StringComparison.Ordinal);
            Assert.Contains("لا موظف مطابق لهذه التصفية.", body, StringComparison.Ordinal);
            Assert.Contains("document.documentElement.dir === 'rtl'", body, StringComparison.Ordinal);
        }

        [Fact]
        public void A_truncated_list_says_so_rather_than_letting_the_filter_imply_nobody()
        {
            var body = View;

            // The cap is the controller's; the filter can only search what it was handed. If the
            // school has more designatable employees than the page carries, the panel admits it.
            Assert.Contains("Model.DesignatableTruncated", body, StringComparison.Ordinal);
            Assert.Contains("التصفية تبحث في هذه فقط.", body, StringComparison.Ordinal);
        }

        [Fact]
        public void Enter_in_the_filter_box_does_not_submit_the_designation()
        {
            // Typing a name and pressing Enter would otherwise post the form with whatever weekly
            // maximum happened to be sitting in the box.
            Assert.Contains("if (e.key === 'Enter') { e.preventDefault(); }", View, StringComparison.Ordinal);
        }
    }
}
