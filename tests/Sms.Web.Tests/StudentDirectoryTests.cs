using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.TestSupport;
using Sms.Web.Controllers;
using Sms.Web.Models;
using Sms.Web.Security;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The student directory's filters and its two ways out of the screen
    /// (doc/Modules/10 §8 — "student search/list (global, filters, saved views,
    /// export-gated)"; §10 — "Students register by grade/section/status").
    /// <para>
    /// What is pinned here is what can be got silently wrong. The filtering
    /// itself is an EF query and shows up the moment the screen is opened; the
    /// two things that do not show up are the rights the printed register and
    /// the exported file demand — a screen handed out under View is a school's
    /// whole roll of children, guardians and mobile numbers leaving the building
    /// — and the file's own encoding, which is only wrong once it is on somebody
    /// else's computer.
    /// </para>
    /// </summary>
    public class StudentDirectoryTests
    {
        private static MethodInfo Action(string name) =>
            Assert.Single(typeof(StudentsController).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == name));

        /// <summary>The attribute keeps its three values in <c>Arguments</c>, so they are read positionally.</summary>
        private static (string Module, string Screen, ActionVerb Verb) Permission(string action) =>
            Assert.Single(Action(action).GetCustomAttributes<RequirePermissionAttribute>()
                .Select(a => ((string)a.Arguments![0], (string)a.Arguments[1], (ActionVerb)a.Arguments[2])));

        // ---------------------------------------------------------------- the rights

        /// <summary>
        /// Reading the directory, printing it and exporting it are three grants, not one. A school
        /// that lets a homeroom teacher look a child up has not thereby agreed to hand them the
        /// register (doc/Modules/10 §6 — "Full-file export: Registrar + Export permission").
        /// </summary>
        [Fact]
        [BusinessRule("BR-GLB-070")]
        public void Printing_and_exporting_the_register_are_rights_of_their_own()
        {
            Assert.Equal(
                (ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, ActionVerb.View),
                Permission(nameof(StudentsController.Index)));

            Assert.Equal(
                (ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, ActionVerb.Print),
                Permission(nameof(StudentsController.Print)));

            Assert.Equal(
                (ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, ActionVerb.Export),
                Permission(nameof(StudentsController.ExportCsv)));
        }

        /// <summary>
        /// And the catalogue has to define them, or the seeder never writes the rows and the two
        /// screens answer NotFound to everybody — the system administrator included. That failure
        /// is silent and reads as a broken link rather than as a missing grant.
        /// </summary>
        [Fact]
        [BusinessRule("BR-GLB-070")]
        public void The_catalogue_defines_both_verbs_so_the_seeder_writes_them()
        {
            Assert.True(ScreenCatalog.Defines(
                ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, ActionVerb.Print));
            Assert.True(ScreenCatalog.Defines(
                ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, ActionVerb.Export));
        }

        /// <summary>
        /// Both are GET requests that read and write nothing, so neither carries an antiforgery
        /// token — but neither may be a POST either, because the print sheet is reached by a link
        /// and browsers re-issue it on refresh.
        /// </summary>
        [Fact]
        public void The_register_and_the_export_are_reached_by_a_link()
        {
            Assert.NotEmpty(Action(nameof(StudentsController.Print)).GetCustomAttributes<HttpGetAttribute>());
            Assert.NotEmpty(Action(nameof(StudentsController.ExportCsv)).GetCustomAttributes<HttpGetAttribute>());
        }

        /// <summary>
        /// Every filter the screen offers reaches all three, by the same names. A print button that
        /// dropped the section would hand a registrar the whole school's roll under the heading of
        /// the section they were looking at.
        /// </summary>
        [Fact]
        public void All_three_surfaces_take_the_same_five_filters()
        {
            var expected = new[] { "q", "status", "grade", "section", "gender" };

            foreach (var name in new[]
            {
                nameof(StudentsController.Index),
                nameof(StudentsController.Print),
                nameof(StudentsController.ExportCsv),
            })
            {
                Assert.Equal(expected, Action(name).GetParameters().Select(p => p.Name).ToArray());
            }
        }

        // ---------------------------------------------------------------- the file

        /// <summary>
        /// A family name can carry a comma, and an unquoted one shifts every column after it —
        /// which turns a mobile number into a status and is never noticed, because the file opens.
        /// </summary>
        [Fact]
        public void A_name_containing_a_comma_stays_one_cell()
        {
            var line = StudentDirectoryExport.Line(new[] { "S-001", "Al-Amin, Yusuf", "Male" });

            Assert.Equal("\"S-001\",\"Al-Amin, Yusuf\",\"Male\"", line);
        }

        /// <summary>A quote inside a value is doubled, per RFC 4180 — not stripped, not escaped with a backslash.</summary>
        [Fact]
        public void A_quote_inside_a_value_is_doubled()
        {
            Assert.Equal("\"He said \"\"no\"\"\"", StudentDirectoryExport.Cell("He said \"no\""));
        }

        [Fact]
        public void A_missing_value_is_an_empty_cell_rather_than_the_word_null()
        {
            Assert.Equal("\"\"", StudentDirectoryExport.Cell(null));
        }

        /// <summary>
        /// The byte-order mark is the whole reason Arabic names survive the trip into Excel.
        /// Without it the register arrives as mojibake and reads as if the system mangled it.
        /// </summary>
        [Fact]
        public void The_file_opens_in_excel_with_arabic_intact()
        {
            var bytes = StudentDirectoryExport.Bytes(new[] { new[] { "محمد الأمين" } });

            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
            Assert.Equal("\"محمد الأمين\"\r\n", Encoding.UTF8.GetString(bytes.Skip(3).ToArray()));
        }

        /// <summary>The headings are the reader's, not a fixed English schema, and there is one per column.</summary>
        [Fact]
        public void The_headings_are_bilingual_and_one_per_column()
        {
            var english = StudentDirectoryExport.Headings(arabic: false);
            var arabic = StudentDirectoryExport.Headings(arabic: true);

            Assert.Equal(9, english.Count);
            Assert.Equal(9, arabic.Count);
            Assert.All(english.Zip(arabic), pair => Assert.NotEqual(pair.First, pair.Second));
            Assert.All(arabic, heading => Assert.DoesNotContain(heading, english));
        }

        [Fact]
        public void The_download_is_dated_so_two_exports_do_not_share_a_name()
        {
            Assert.Equal("students-2026-08-31.csv", StudentDirectoryExport.FileName(new DateTime(2026, 8, 31, 14, 5, 0, DateTimeKind.Utc)));
        }

        // ---------------------------------------------------------------- what the sheet says it holds

        /// <summary>
        /// A printed roll with nothing saying which children it holds gets read next term as the
        /// whole school. The description is what stops that, and it has to be in the reader's
        /// language like every other string the product prints.
        /// </summary>
        [Fact]
        public void The_printed_sheet_says_what_it_was_filtered_to_in_both_languages()
        {
            var english = StudentDirectoryExport.Describe(
                arabic: false, query: "ahmad", status: "Enrolled", grade: "Grade 5", section: "Grade 5 / A", gender: "Female");

            Assert.Equal(
                new[] { "Grade: Grade 5", "Section: Grade 5 / A", "Gender: Female", "Status: Enrolled", "Search: ahmad" },
                english);

            var arabic = StudentDirectoryExport.Describe(
                arabic: true, query: "أحمد", status: "مقيَّد", grade: "الصف الخامس", section: "الصف الخامس / أ", gender: "أنثى");

            Assert.Equal(
                new[] { "الصف: الصف الخامس", "الشعبة: الصف الخامس / أ", "الجنس: أنثى", "الحالة: مقيَّد", "بحث: أحمد" },
                arabic);
        }

        /// <summary>No filter is itself an answer — the sheet says "all students" rather than nothing.</summary>
        [Fact]
        public void An_unfiltered_register_describes_itself_as_empty_of_filters()
        {
            Assert.Empty(StudentDirectoryExport.Describe(arabic: false, null, null, null, null, null));
            Assert.Empty(StudentDirectoryExport.Describe(arabic: true, "  ", "", " ", null, ""));
        }
    }
}
