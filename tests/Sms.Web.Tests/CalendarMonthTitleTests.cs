using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Sms.Web.Models;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The calendar board is a Gregorian grid, and its month titles have to say so.
    /// <para>
    /// <c>ar-SA</c>'s default calendar is Umm al-Qura, so <c>ToString("MMMM yyyy")</c> on that
    /// culture names the <em>Hijri</em> month of the date handed to it. The board painted
    /// September 2025's thirty days under the title ربيع الأول ١٤٤٧ — a title from one calendar
    /// over numbers from another, which is not a Hijri view of September so much as an unreadable
    /// one. ADR-4 and docs/UI/02 settle it: Gregorian is what is shown and stored, Hijri is a
    /// sub-display the school switches on.
    /// </para>
    /// <para>
    /// Startup pins the request culture's calendar to Gregorian precisely so no screen has to
    /// think about this, and the view had walked around that fix by building its own culture from
    /// <c>CultureInfo.GetCultureInfo</c> — which returns the cached, unpinned instance. These
    /// tests hold both halves: that the view keeps formatting on the pinned culture, and that the
    /// framework behaviour which made the bug possible is what this claims it is.
    /// </para>
    /// </summary>
    public class CalendarMonthTitleTests
    {
        private static string CalendarView
        {
            get
            {
                var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile())!, "..", ".."));
                return Path.Combine(repoRoot, "src", "Sms.Web", "Views", "Calendar", "Index.cshtml");
            }
        }

        private static string ThisFile([CallerFilePath] string path = "") => path;

        /// <summary>
        /// The view's own comment explains the trap by naming it, so the scan reads code only —
        /// otherwise the explanation of the bug reads as the bug.
        /// </summary>
        private static string CodeOnly(string source) => string.Join(
            "\n",
            source.Split('\n').Where(line =>
            {
                var trimmed = line.TrimStart();
                return !trimmed.StartsWith("//", StringComparison.Ordinal)
                    && !trimmed.StartsWith("@*", StringComparison.Ordinal);
            }));

        [Fact]
        public void The_board_formats_its_month_titles_on_the_gregorian_pinned_culture()
        {
            Assert.True(File.Exists(CalendarView), $"Calendar view not found at '{CalendarView}'.");
            var body = CodeOnly(File.ReadAllText(CalendarView));

            Assert.False(
                body.Contains("GetCultureInfo(\"ar-SA\")", StringComparison.Ordinal),
                "The calendar view builds its formatting culture with GetCultureInfo(\"ar-SA\"), whose calendar is "
                + "Umm al-Qura and whose cached instance the Startup Gregorian pin cannot reach. Format month titles "
                + "on CultureInfo.CurrentCulture instead, or the Arabic board titles September as ربيع الأول.");

            Assert.True(
                body.Contains("CultureInfo.CurrentCulture", StringComparison.Ordinal),
                "The calendar view no longer formats on CultureInfo.CurrentCulture — the one culture whose calendar "
                + "the request pipeline has pinned to Gregorian.");
        }

        /// <summary>
        /// The trap itself, asserted rather than described: a Gregorian date formatted on the raw
        /// Arabic culture carries a Hijri year, and the same date on a Gregorian-pinned clone
        /// carries the Gregorian one. Deliberately asserts the year rather than the month name —
        /// which of أيلول/سبتمبر the ICU data ships is not this test's business.
        /// </summary>
        [Fact]
        public void Pinning_the_calendar_is_what_makes_an_arabic_month_title_gregorian()
        {
            var september = new DateTime(2025, 9, 1);
            var raw = CultureInfo.GetCultureInfo("ar-SA");

            Assert.IsNotType<GregorianCalendar>(raw.DateTimeFormat.Calendar);
            Assert.Contains("1447", september.ToString("MMMM yyyy", raw), StringComparison.Ordinal);

            var pinned = (CultureInfo)raw.Clone();
            pinned.DateTimeFormat.Calendar = new GregorianCalendar();
            var title = september.ToString("MMMM yyyy", pinned);

            Assert.Contains("2025", title, StringComparison.Ordinal);
            Assert.DoesNotContain("1447", title, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(10)]
        [InlineData(11)]
        [InlineData(12)]
        public void Every_hijri_month_is_named_in_both_languages(int month)
        {
            var ar = CalendarLabels.HijriMonth(month, true);
            var en = CalendarLabels.HijriMonth(month, false);

            Assert.False(string.IsNullOrWhiteSpace(ar), $"Hijri month {month} has no Arabic name.");
            Assert.False(string.IsNullOrWhiteSpace(en), $"Hijri month {month} has no English name.");
            Assert.NotEqual(ar, en);
            Assert.NotEqual(month.ToString(CultureInfo.InvariantCulture), ar);
        }

        /// <summary>
        /// The overlay label is decoration on a working screen; a month number the table does not
        /// cover must not take the board down with it.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(13)]
        public void An_out_of_range_month_renders_its_number_rather_than_throwing(int month)
        {
            Assert.Equal(month.ToString(CultureInfo.InvariantCulture), CalendarLabels.HijriMonth(month, true));
        }
    }
}
