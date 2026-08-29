using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sms.Application.Dashboards;
using Sms.Domain.Schools;

namespace Sms.Web.Models
{
    /// <summary>
    /// The statistics screen (doc/Modules/31 §8.1 at school scope).
    /// </summary>
    public class StatisticsViewModel
    {
        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();

        public AcademicYear? Year { get; set; }

        public SchoolStatistics? Stats { get; set; }

        /// <summary>When the figures were computed. Nothing here is cached, so this is "now" — shown anyway, because a printed page needs to say when it was true (BR-DSH-002).</summary>
        public DateTime AsOfUtc { get; set; }

        /// <summary>
        /// False when no ledger is attached. Distinct from "expenses were zero" —
        /// the screen says which, and never shows a zero it did not measure.
        /// </summary>
        public bool HasLedger => Stats?.Expenses != null;
    }

    /// <summary>
    /// A horizontal bar chart, drawn with sized elements rather than SVG.
    /// <para>
    /// Bars are the one chart kind that needs no geometry: a width percentage is
    /// already the whole drawing, the browser mirrors it under RTL without being
    /// asked, and the labels stay real text that reflows and is selectable at any
    /// zoom. SVG is kept for the two shapes that genuinely need coordinates.
    /// </para>
    /// </summary>
    public class BarChartViewModel
    {
        public string Title { get; set; } = string.Empty;

        public IReadOnlyList<StatisticSlice> Slices { get; set; } = Array.Empty<StatisticSlice>();

        /// <summary>Money renders to two decimals inside an LTR isolate; counts render whole.</summary>
        public bool IsMoney { get; set; }

        /// <summary>Shown in place of the chart when every slice is zero.</summary>
        public string EmptyText { get; set; } = string.Empty;

        /// <summary>Longest slice, the length every bar is drawn against.</summary>
        public decimal Ceiling => Slices.Count == 0 ? 0m : Slices.Max(s => s.Value);
    }

    /// <summary>A donut and its legend. The legend carries the numbers, so the colours never have to.</summary>
    public class DonutChartViewModel
    {
        public string Title { get; set; } = string.Empty;

        public IReadOnlyList<StatisticSlice> Slices { get; set; } = Array.Empty<StatisticSlice>();

        public bool IsMoney { get; set; }

        public string EmptyText { get; set; } = string.Empty;

        public decimal Total => Slices.Sum(s => s.Value);
    }

    /// <summary>
    /// Two series over the same months — billed against collected, revenue against
    /// expenses. One chart and one ceiling, because reading a pair off two charts
    /// with two scales is what makes a bad month look like a good one.
    /// </summary>
    public class TrendChartViewModel
    {
        public string Title { get; set; } = string.Empty;

        public IReadOnlyList<MonthlyPair> Points { get; set; } = Array.Empty<MonthlyPair>();

        public string FirstLabel { get; set; } = string.Empty;

        public string SecondLabel { get; set; } = string.Empty;

        public string EmptyText { get; set; } = string.Empty;

        public bool HasData => Points.Any(p => p.First != 0m || p.Second != 0m);
    }

    /// <summary>
    /// Text the statistics screen and its chart partials share.
    /// <para>
    /// Month names are written out rather than taken from
    /// <see cref="CultureInfo"/>: the invariant culture gives English, and
    /// <c>ar-SA</c> gives Hijri month names off a Gregorian month number — so
    /// September would print as صفر. CLAUDE.md's rule is Gregorian input with
    /// Hijri only as an optional sub-display, and a trend axis is not the place to
    /// switch calendars behind a reader's back.
    /// </para>
    /// </summary>
    public static class StatisticsLabels
    {
        private static readonly string[] MonthsEn =
        {
            "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec",
        };

        private static readonly string[] MonthsAr =
        {
            "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
            "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر",
        };

        /// <summary>Short month name. Out-of-range months render as their number rather than throwing.</summary>
        public static string Month(int month, bool isRtl)
            => month >= 1 && month <= 12
                ? (isRtl ? MonthsAr[month - 1] : MonthsEn[month - 1])
                : month.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// A month with its year, for the axis label of a chart that crosses a new
        /// year — an academic year always does, and "Jan" alone in the middle of one
        /// does not say which January.
        /// </summary>
        public static string MonthYear(int year, int month, bool isRtl)
            => Month(month, isRtl) + " " + year.ToString(CultureInfo.InvariantCulture);

        /// <summary>The slice's name in the reader's language.</summary>
        public static string Name(StatisticSlice slice, bool isRtl)
            => isRtl ? slice.NameAr : slice.NameEn;

        /// <summary>
        /// Money to two decimals, counts whole, both in Latin digits — the caller
        /// wraps the result in a <c>bdi dir="ltr"</c>, which is how every other
        /// figure in this product is rendered.
        /// </summary>
        public static string Value(decimal value, bool isMoney)
            => value.ToString(isMoney ? "N2" : "N0", CultureInfo.InvariantCulture);
    }
}
