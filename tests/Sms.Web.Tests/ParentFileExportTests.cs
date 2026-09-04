using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Sms.Application.Security;
using Sms.Domain.Parents;
using Sms.Domain.Security;
using Sms.Domain.Students;
using Sms.TestSupport;
using Sms.Web.Controllers;
using Sms.Web.Models;
using Sms.Web.Security;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The parent file's two ways out of the screen (doc/Modules/11 §8.2; §10 "Family register —
    /// parents with children count, balances").
    /// <para>
    /// What is pinned here is what can be got silently wrong. That the sheet renders shows up the
    /// moment anybody opens it; what does not show up is the right each surface demands — a family
    /// file handed out under View is every child, every mobile number and what the family owes
    /// leaving the building — and the file's own shaping, which is only wrong once it is on
    /// somebody else's computer and reads as the school's data being wrong.
    /// </para>
    /// </summary>
    public class ParentFileExportTests
    {
        private static MethodInfo Action(string name) =>
            Assert.Single(typeof(ParentsController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.Name == name));

        /// <summary>The attribute keeps its three values in <c>Arguments</c>, so they are read positionally.</summary>
        private static (string Module, string Screen, ActionVerb Verb) Permission(string action) =>
            Assert.Single(Action(action).GetCustomAttributes<RequirePermissionAttribute>()
                .Select(a => ((string)a.Arguments![0], (string)a.Arguments[1], (ActionVerb)a.Arguments[2])));

        // ---------------------------------------------------------------- the rights

        /// <summary>
        /// Reading the file, printing it and exporting it are three grants, not one. A school that
        /// lets a receptionist look a family up has not thereby agreed to hand them the sheet
        /// naming every child in it (doc/Modules/11 §6).
        /// </summary>
        [Fact]
        [BusinessRule("BR-GLB-070")]
        public void Printing_the_file_and_exporting_it_are_rights_of_their_own()
        {
            Assert.Equal(
                (ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.File, ActionVerb.View),
                Permission(nameof(ParentsController.File)));

            Assert.Equal(
                (ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.File, ActionVerb.Print),
                Permission(nameof(ParentsController.Print)));

            Assert.Equal(
                (ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.File, ActionVerb.Export),
                Permission(nameof(ParentsController.ExportCsv)));
        }

        /// <summary>
        /// And the catalogue has to define them, or the seeder never writes the rows and both
        /// surfaces answer NotFound to everybody — the system administrator included. That failure
        /// is silent and reads as a broken button rather than as a missing grant.
        /// </summary>
        [Fact]
        [BusinessRule("BR-GLB-070")]
        public void The_catalogue_defines_both_verbs_so_the_seeder_writes_them()
        {
            Assert.True(ScreenCatalog.Defines(
                ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.File, ActionVerb.Print));
            Assert.True(ScreenCatalog.Defines(
                ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.File, ActionVerb.Export));
        }

        /// <summary>
        /// Both read and write nothing, so neither carries an antiforgery token — and neither may
        /// be a POST either, because both are reached by a link and a browser re-issues those on
        /// refresh.
        /// </summary>
        [Fact]
        public void The_sheet_and_the_export_are_reached_by_a_link()
        {
            Assert.NotEmpty(Action(nameof(ParentsController.Print)).GetCustomAttributes<HttpGetAttribute>());
            Assert.NotEmpty(Action(nameof(ParentsController.ExportCsv)).GetCustomAttributes<HttpGetAttribute>());
        }

        /// <summary>
        /// BR-SEC-010 on the page head. Neither button belongs to the sidebar, so nothing else
        /// hides it: dropping the guard leaves a view that compiles, a page that renders, and a
        /// clerk without the grant clicking through to a bare not-found.
        /// </summary>
        [Theory]
        [InlineData("asp-action=\"Print\"", "Model.CanPrint")]
        [InlineData("asp-action=\"ExportCsv\"", "Model.CanExport")]
        [BusinessRule("BR-SEC-010")]
        public void The_buttons_are_offered_only_to_a_user_who_holds_the_grant(string marker, string flag)
        {
            var body = System.IO.File.ReadAllText(Path.Combine(ParentViews, "File.cshtml"));
            var anchor = body.IndexOf(marker, StringComparison.Ordinal);

            Assert.True(anchor >= 0, $"File.cshtml no longer carries a button with {marker} at all.");

            // The guard is the nearest @if opening ahead of the anchor: the button is written inside it.
            var guardStart = body.LastIndexOf("@if (", anchor, StringComparison.Ordinal);
            var because = $"The page head offers {marker} unconditionally, so a clerk without the "
                + "grant is handed a 404 instead of not being offered the button (BR-SEC-010).";

            Assert.True(guardStart >= 0, because);
            Assert.True(body.Substring(guardStart, anchor - guardStart).Contains(flag, StringComparison.Ordinal), because);
        }

        // ---------------------------------------------------------------- the file

        /// <summary>
        /// Every row carries the same number of cells as there are headings. A row one cell short
        /// shifts every column after it — a mobile number becomes a relationship — and the file
        /// still opens, which is why nobody notices.
        /// </summary>
        [Fact]
        public void Every_row_is_as_wide_as_the_headings()
        {
            foreach (var arabic in new[] { false, true })
            {
                var records = ParentFileExport.Records(Family(), arabic);
                var width = ParentFileExport.Headings(arabic).Count;

                Assert.All(records, record => Assert.Equal(width, record.Count()));
            }
        }

        /// <summary>
        /// One row per child, not one per family, with the parent repeated down the left — that
        /// repetition is what lets a registrar concatenate several of these into one register.
        /// </summary>
        [Fact]
        public void The_family_is_one_row_per_child_with_the_parent_on_each()
        {
            var records = Cells(ParentFileExport.Records(Family(), arabic: false));

            // Headings, the current child, then the ended link.
            Assert.Equal(3, records.Count);
            Assert.Equal(new[] { "PAR-000752", "Yusuf Al-Amin", "0555000111", "yusuf@example.com" }, records[1].Take(4));
            Assert.Equal(records[1].Take(4), records[2].Take(4));
            Assert.Equal("S-001", records[1][4]);
            Assert.Equal("S-002", records[2][4]);
        }

        /// <summary>
        /// The family statement consolidates the current links (BR-FEE-008). A balance printed
        /// against a link that ended in March would be read as still owing, so those cells stay
        /// empty — and the end date says why.
        /// </summary>
        [Fact]
        [BusinessRule("BR-FEE-008")]
        public void An_ended_link_is_exported_without_a_balance()
        {
            var records = Cells(ParentFileExport.Records(Family(), arabic: false));

            Assert.Equal("", records[1][13]);
            Assert.Equal("1250.00", records[1][14]);
            Assert.Equal("300.00", records[1][18]);

            Assert.Equal("2026-03-31", records[2][13]);
            Assert.All(records[2].Skip(14), cell => Assert.Equal("", cell));
        }

        /// <summary>
        /// A parent with nobody linked is still a parent worth exporting. A file holding only its
        /// headings reads as the export having failed rather than as the fact that it is.
        /// </summary>
        [Fact]
        public void A_parent_with_no_children_still_exports_a_row()
        {
            var alone = Family();
            alone.Children = Array.Empty<ParentFileViewModel.ChildRow>();
            alone.PastChildren = Array.Empty<ParentFileViewModel.ChildRow>();
            alone.FamilyStatement = Array.Empty<FamilyStatementLine>();

            var records = Cells(ParentFileExport.Records(alone, arabic: false));

            Assert.Equal(2, records.Count);
            Assert.Equal("PAR-000752", records[1][0]);
            Assert.All(records[1].Skip(4), cell => Assert.Equal("", cell));
        }

        /// <summary>
        /// Money leaves the system for another one — a ministry return, a bank file, a mail merge.
        /// The screen's <c>N2</c> is the reader's format and carries a thousands separator; under
        /// an Arabic locale it can carry Arabic-Indic digits too, and either one arrives in the
        /// receiving system as text rather than as an amount.
        /// </summary>
        [Fact]
        public void Money_leaves_in_the_invariant_format_whatever_the_clerk_is_reading_in()
        {
            var original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("ar-SA");

                Assert.Equal("1250.00", ParentFileExport.Money(1250m));
                Assert.Equal("0.00", ParentFileExport.Money(0m));
                Assert.Equal("-75.50", ParentFileExport.Money(-75.5m));
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        /// <summary>A date the record does not hold is an empty cell, never today's date.</summary>
        [Fact]
        public void A_missing_date_is_an_empty_cell()
        {
            Assert.Equal(string.Empty, ParentFileExport.Date(null));
            Assert.Equal("2026-03-31", ParentFileExport.Date(new DateTime(2026, 3, 31, 9, 0, 0, DateTimeKind.Utc)));
        }

        /// <summary>The screen's ✓ and — mean nothing in a spreadsheet, and neither does English in an Arabic file.</summary>
        [Fact]
        public void The_link_flags_are_words_in_the_readers_language()
        {
            Assert.Equal("Yes", ParentFileExport.Flag(true, arabic: false));
            Assert.Equal("No", ParentFileExport.Flag(false, arabic: false));
            Assert.Equal("نعم", ParentFileExport.Flag(true, arabic: true));
            Assert.Equal("لا", ParentFileExport.Flag(false, arabic: true));
        }

        /// <summary>The headings are the reader's, not a fixed English schema, and there is one per column.</summary>
        [Fact]
        public void The_headings_are_bilingual_and_one_per_column()
        {
            var english = ParentFileExport.Headings(arabic: false);
            var arabic = ParentFileExport.Headings(arabic: true);

            Assert.Equal(19, english.Count);
            Assert.Equal(19, arabic.Count);
            Assert.All(english.Zip(arabic), pair => Assert.NotEqual(pair.First, pair.Second));
            Assert.All(arabic, heading => Assert.DoesNotContain(heading, english));
        }

        /// <summary>
        /// Two families' exports have to be able to sit in one folder, and two exports of one
        /// family taken a week apart as well.
        /// </summary>
        [Fact]
        public void The_download_is_named_for_the_family_and_the_day()
        {
            Assert.Equal(
                "parent-PAR-000752-2026-09-01.csv",
                ParentFileExport.FileName("PAR-000752", new DateTime(2026, 9, 1, 20, 12, 0, DateTimeKind.Utc)));
        }

        /// <summary>
        /// A numbering series can be configured with an Arabic prefix (doc/Modules/01 §"Numbering"),
        /// and a browser handed a non-Latin <c>Content-Disposition</c> may save the download under a
        /// name nobody can find again. The number is folded to ASCII, and a number with nothing
        /// ASCII in it falls back rather than producing <c>parent--2026-09-01.csv</c>.
        /// </summary>
        [Fact]
        public void A_non_latin_file_number_still_produces_a_findable_download()
        {
            var taken = new DateTime(2026, 9, 1, 20, 12, 0, DateTimeKind.Utc);

            Assert.Equal("parent-752-2026-09-01.csv", ParentFileExport.FileName("ولي-752", taken));
            Assert.Equal("parent-file-2026-09-01.csv", ParentFileExport.FileName("ولي أمر", taken));
            Assert.Equal("parent-file-2026-09-01.csv", ParentFileExport.FileName(null, taken));
        }

        // ---------------------------------------------------------------- the bytes

        /// <summary>
        /// A family name can carry a comma, and an unquoted one shifts every column after it —
        /// which turns a mobile number into a relationship and is never noticed, because the file
        /// opens.
        /// </summary>
        [Fact]
        public void A_name_containing_a_comma_stays_one_cell()
        {
            Assert.Equal(
                "\"PAR-000752\",\"Al-Amin, Yusuf\",\"0555000111\"",
                CsvFile.Line(new[] { "PAR-000752", "Al-Amin, Yusuf", "0555000111" }));
        }

        /// <summary>A quote inside a value is doubled, per RFC 4180 — not stripped, not backslash-escaped.</summary>
        [Fact]
        public void A_quote_inside_a_value_is_doubled()
        {
            Assert.Equal("\"He said \"\"no\"\"\"", CsvFile.Cell("He said \"no\""));
            Assert.Equal("\"\"", CsvFile.Cell(null));
        }

        /// <summary>
        /// The byte-order mark is the whole reason Arabic names survive the trip into Excel.
        /// Without it the family arrives as mojibake and reads as if the system mangled it.
        /// </summary>
        [Fact]
        public void The_file_opens_in_excel_with_arabic_intact()
        {
            var bytes = CsvFile.Bytes(new[] { new[] { "يوسف الأمين" } });

            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
            Assert.Equal("\"يوسف الأمين\"\r\n", Encoding.UTF8.GetString(bytes.Skip(3).ToArray()));
        }

        // ---------------------------------------------------------------- a family to export

        /// <summary>
        /// One parent, one current child with a balance, one link that ended — the three shapes the
        /// export has to keep apart.
        /// </summary>
        private static ParentFileViewModel Family()
        {
            var current = Child(1, "S-001", "Layla", "ليلى");
            var past = Child(2, "S-002", "Omar", "عمر");

            return new ParentFileViewModel
            {
                Parent = new Parent
                {
                    Id = 752,
                    ParentFileNo = "PAR-000752",
                    NameEn = "Yusuf Al-Amin",
                    NameAr = "يوسف الأمين",
                    PrimaryMobile = "0555000111",
                    Email = "yusuf@example.com",
                },
                Children = new[]
                {
                    new ParentFileViewModel.ChildRow(
                        new StudentGuardianLink
                        {
                            StudentId = current.Id,
                            IsPrimaryContact = true,
                            IsFinanciallyResponsible = true,
                            IsPickupAuthorized = true,
                            IsPortalVisible = true,
                            EffectiveFromUtc = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                        },
                        current, "Father", "Grade 5"),
                },
                PastChildren = new[]
                {
                    new ParentFileViewModel.ChildRow(
                        new StudentGuardianLink
                        {
                            StudentId = past.Id,
                            EffectiveFromUtc = new DateTime(2023, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                            EffectiveToUtc = new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc),
                        },
                        past, "Father", "Grade 9"),
                },
                FamilyStatement = new[]
                {
                    new FamilyStatementLine(current, 1250m, 100m, 50m, 800m, 300m, 3),
                },
            };
        }

        private static Student Child(int id, string no, string en, string ar) => new Student
        {
            Id = id,
            StudentNo = no,
            FirstNameEn = en, FatherNameEn = "Yusuf", FamilyNameEn = "Al-Amin",
            FirstNameAr = ar, FatherNameAr = "يوسف", FamilyNameAr = "الأمين",
        };

        private static List<string?[]> Cells(IEnumerable<IEnumerable<string?>> records)
            => records.Select(r => r.ToArray()).ToList();

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
