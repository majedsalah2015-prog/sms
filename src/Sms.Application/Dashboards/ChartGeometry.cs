using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Sms.Application.Dashboards
{
    /// <summary>
    /// The arithmetic behind the statistics screen's charts: axis ceilings, bar
    /// lengths, donut arcs and trend polylines. A pure static engine in the
    /// Application layer for the reason every engine here is one — the geometry is
    /// where a chart lies, and a lie in a chart is only findable by a test.
    /// <para>
    /// <b>Every number this class emits is formatted with
    /// <see cref="CultureInfo.InvariantCulture"/>, and that is the point of it
    /// existing rather than the view doing the sums.</b> SVG path data and
    /// coordinate attributes are machine-read: <c>d="M 12.5 40"</c> is a path and
    /// <c>d="M ١٢٫٥ ٤٠"</c> is nothing at all. A Razor view rendering under
    /// <c>ar-SA</c> interpolates a decimal exactly that way, so a chart built
    /// inline in a view works in English and silently draws an empty box in
    /// Arabic — half this product's users. Geometry crosses into the view already
    /// stringified, in one culture, from here.
    /// </para>
    /// <para>
    /// Angles are degrees clockwise from twelve o'clock, which is how a reader
    /// scans a pie and therefore the only ordering that needs no comment at the
    /// call site. Trigonometry runs in <see cref="double"/> because
    /// <see cref="Math"/> offers nothing else; the inputs and the emitted text stay
    /// decimal, so no money value is ever round-tripped through a binary float.
    /// </para>
    /// </summary>
    public static class ChartGeometry
    {
        /// <summary>
        /// A full circle drawn as one arc has identical start and end points, and
        /// SVG renders that as nothing. Where a single slice holds everything —
        /// one payment method, one fee category, both ordinary in a small school —
        /// the ring is emitted as two half arcs instead. See <see cref="SlicePath"/>.
        /// </summary>
        private const decimal FullCircle = 360m;

        /// <summary>
        /// The floor under every axis top, and it applies to small series as well
        /// as empty ones.
        /// <para>
        /// An all-zero series needs it because a ceiling of zero is divided by. A
        /// series whose largest value is a fraction needs it for a different
        /// reason: every figure on this screen is a headcount or an amount of
        /// money, so an axis topping out at 0.5 is measuring half a person or half
        /// a riyal. Scaling that to full height turns rounding dust into a
        /// mountain range — the honest picture of "almost nothing" is a flat chart
        /// against a ceiling of one.
        /// </para>
        /// </summary>
        private const decimal MinimumCeiling = 1m;

        /// <summary>
        /// The rounded axis maximum at or above <paramref name="largestValue"/>:
        /// 1, 2, 2.5 or 5 times a power of ten.
        /// <para>
        /// Charts are read against their gridlines, and a gridline at 3,847 is not
        /// read at all. Rounding up rather than to nearest keeps the tallest bar
        /// inside the plot instead of one pixel over its top.
        /// </para>
        /// <para>Zero and negatives yield <see cref="MinimumCeiling"/> — an empty
        /// chart still has to have an axis to be drawn against.</para>
        /// </summary>
        public static decimal NiceCeiling(decimal largestValue)
        {
            if (largestValue <= 0m)
            {
                return MinimumCeiling;
            }

            // Decompose into mantissa × 10^exponent by dividing down, rather than
            // through Log10: a decimal -> double -> decimal round trip loses the
            // exactness that made the caller pass a decimal in the first place.
            var exponent = 0;
            var mantissa = largestValue;
            while (mantissa >= 10m)
            {
                mantissa /= 10m;
                exponent++;
            }

            while (mantissa < 1m)
            {
                mantissa *= 10m;
                exponent--;
            }

            var step = mantissa switch
            {
                <= 1m => 1m,
                <= 2m => 2m,
                <= 2.5m => 2.5m,
                <= 5m => 5m,
                _ => 10m,
            };

            var ceiling = step * Pow10(exponent);
            return ceiling < MinimumCeiling ? MinimumCeiling : ceiling;
        }

        /// <summary>
        /// Evenly spaced axis values from zero to <paramref name="ceiling"/>
        /// inclusive — <paramref name="divisions"/> + 1 of them.
        /// </summary>
        public static IReadOnlyList<decimal> AxisTicks(decimal ceiling, int divisions = 4)
        {
            if (divisions < 1)
            {
                divisions = 1;
            }

            var ticks = new decimal[divisions + 1];
            for (var i = 0; i <= divisions; i++)
            {
                ticks[i] = ceiling * i / divisions;
            }

            return ticks;
        }

        /// <summary>
        /// <paramref name="part"/> as a percentage of <paramref name="whole"/>,
        /// rounded to one decimal. A zero or negative whole gives zero rather than
        /// throwing: a screen asking "what share of nothing" wants a flat bar, not
        /// a 500.
        /// </summary>
        public static decimal Percent(decimal part, decimal whole)
            => whole <= 0m ? 0m : Math.Round(part * 100m / whole, 1, MidpointRounding.AwayFromZero);

        /// <summary>
        /// A bar's length as a percentage of the plot, clamped to 0–100.
        /// <para>
        /// Clamped rather than trusted because the ceiling and the value can be
        /// computed from different queries a moment apart; a 104% bar overhangs
        /// its card and takes the layout with it.
        /// </para>
        /// </summary>
        public static decimal BarPercent(decimal value, decimal ceiling)
        {
            var percent = Percent(value, ceiling);
            return percent < 0m ? 0m : percent > 100m ? 100m : percent;
        }

        /// <summary>
        /// One slice of a donut: where it starts, how far it sweeps, what share it
        /// holds, and the SVG path that draws it.
        /// </summary>
        /// <param name="Index">Position in the input series, so a caller can pair the slice back to its label and colour.</param>
        /// <param name="Value">The slice's own value, unchanged.</param>
        /// <param name="Percent">Its share of the total, to one decimal.</param>
        /// <param name="StartAngle">Degrees clockwise from twelve o'clock.</param>
        /// <param name="SweepAngle">Degrees covered.</param>
        /// <param name="Path">Ready for an SVG <c>path</c> element's <c>d</c> attribute.</param>
        public sealed record DonutSlice(
            int Index, decimal Value, decimal Percent, decimal StartAngle, decimal SweepAngle, string Path);

        /// <summary>
        /// Splits <paramref name="values"/> into donut slices around
        /// (<paramref name="centreX"/>, <paramref name="centreY"/>), largest angle
        /// last so rounding lands where it shows least.
        /// <para>
        /// Zero and negative values are skipped rather than drawn as hairlines: a
        /// fee category nobody was charged for has no wedge, and a zero-width path
        /// is still a tooltip target and still a row in the legend nobody can
        /// point at.
        /// </para>
        /// <para>
        /// An all-zero series returns an empty list; the caller draws its own empty
        /// state, because "no data" is a sentence, not a shape.
        /// </para>
        /// </summary>
        public static IReadOnlyList<DonutSlice> Donut(
            IReadOnlyList<decimal> values, decimal centreX, decimal centreY, decimal outerRadius, decimal innerRadius)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<DonutSlice>();
            }

            var total = values.Where(v => v > 0m).Sum();
            if (total <= 0m)
            {
                return Array.Empty<DonutSlice>();
            }

            var slices = new List<DonutSlice>(values.Count);
            var cursor = 0m;

            for (var i = 0; i < values.Count; i++)
            {
                var value = values[i];
                if (value <= 0m)
                {
                    continue;
                }

                // The last drawn slice takes whatever angle is left rather than its
                // own computed share, so three thirds close the circle instead of
                // leaving a 0.0001 degree seam at twelve o'clock.
                var isLast = !values.Skip(i + 1).Any(v => v > 0m);
                var sweep = isLast ? FullCircle - cursor : value * FullCircle / total;

                slices.Add(new DonutSlice(
                    i, value, Percent(value, total), cursor, sweep,
                    SlicePath(centreX, centreY, outerRadius, innerRadius, cursor, sweep)));

                cursor += sweep;
            }

            return slices;
        }

        /// <summary>
        /// The path for one donut segment: out along the outer radius, back along
        /// the inner one.
        /// <para>
        /// A sweep of a full circle is drawn as two half arcs. One arc whose start
        /// and end coincide is, to every SVG renderer, a command to draw nothing —
        /// so the single-category case would render an empty box, which is exactly
        /// the case a small school hits first.
        /// </para>
        /// </summary>
        private static string SlicePath(
            decimal centreX, decimal centreY, decimal outerRadius, decimal innerRadius,
            decimal startAngle, decimal sweepAngle)
        {
            var path = new StringBuilder();

            if (sweepAngle >= FullCircle)
            {
                // Two half arcs each way: outer clockwise, inner anticlockwise, so
                // the even-odd fill leaves the hole in the middle.
                AppendRing(path, centreX, centreY, outerRadius, sweep: 1);
                AppendRing(path, centreX, centreY, innerRadius, sweep: 0);
                return path.ToString();
            }

            var endAngle = startAngle + sweepAngle;
            var largeArc = sweepAngle > 180m ? "1" : "0";

            var (outerStartX, outerStartY) = PointOnCircle(centreX, centreY, outerRadius, startAngle);
            var (outerEndX, outerEndY) = PointOnCircle(centreX, centreY, outerRadius, endAngle);
            var (innerEndX, innerEndY) = PointOnCircle(centreX, centreY, innerRadius, endAngle);
            var (innerStartX, innerStartY) = PointOnCircle(centreX, centreY, innerRadius, startAngle);

            path.Append("M ").Append(outerStartX).Append(' ').Append(outerStartY)
                .Append(" A ").Append(N(outerRadius)).Append(' ').Append(N(outerRadius))
                .Append(" 0 ").Append(largeArc).Append(" 1 ").Append(outerEndX).Append(' ').Append(outerEndY)
                .Append(" L ").Append(innerEndX).Append(' ').Append(innerEndY)
                .Append(" A ").Append(N(innerRadius)).Append(' ').Append(N(innerRadius))
                .Append(" 0 ").Append(largeArc).Append(" 0 ").Append(innerStartX).Append(' ').Append(innerStartY)
                .Append(" Z");

            return path.ToString();
        }

        private static void AppendRing(StringBuilder path, decimal centreX, decimal centreY, decimal radius, int sweep)
        {
            var (topX, topY) = PointOnCircle(centreX, centreY, radius, 0m);
            var (bottomX, bottomY) = PointOnCircle(centreX, centreY, radius, 180m);
            var r = N(radius);

            path.Append(path.Length == 0 ? "M " : " M ").Append(topX).Append(' ').Append(topY)
                .Append(" A ").Append(r).Append(' ').Append(r).Append(" 0 0 ").Append(sweep).Append(' ')
                .Append(bottomX).Append(' ').Append(bottomY)
                .Append(" A ").Append(r).Append(' ').Append(r).Append(" 0 0 ").Append(sweep).Append(' ')
                .Append(topX).Append(' ').Append(topY)
                .Append(" Z");
        }

        /// <summary>
        /// A point on a circle at <paramref name="angleDegrees"/> clockwise from
        /// twelve o'clock, already formatted for an SVG attribute. Y is subtracted
        /// because the SVG axis grows downward.
        /// </summary>
        private static (string X, string Y) PointOnCircle(decimal centreX, decimal centreY, decimal radius, decimal angleDegrees)
        {
            var radians = (double)angleDegrees * Math.PI / 180d;
            var x = (double)centreX + ((double)radius * Math.Sin(radians));
            var y = (double)centreY - ((double)radius * Math.Cos(radians));
            return (N(x), N(y));
        }

        /// <summary>
        /// <paramref name="values"/> laid across a
        /// <paramref name="width"/> × <paramref name="height"/> plot as an SVG
        /// <c>points</c> list, oldest at the left.
        /// <para>
        /// Left to right in both languages, deliberately, and it is the one thing on
        /// this screen that does not mirror. A trend axis is time; under RTL the
        /// surrounding page flips, and if the plot flipped with it the same series
        /// would slope up in Arabic and down in English. The month names below it
        /// are translated and stay in plot order — the labels follow the line, not
        /// the page direction.
        /// </para>
        /// <para>
        /// A single point is emitted as a flat two-point line, so a year with one
        /// month of data draws a segment rather than an invisible dot.
        /// </para>
        /// </summary>
        public static string Polyline(IReadOnlyList<decimal> values, decimal width, decimal height, decimal ceiling)
        {
            if (values == null || values.Count == 0)
            {
                return string.Empty;
            }

            if (ceiling <= 0m)
            {
                ceiling = MinimumCeiling;
            }

            if (values.Count == 1)
            {
                var only = PlotY(values[0], height, ceiling);
                return "0," + only + " " + N(width) + "," + only;
            }

            var points = new StringBuilder();
            for (var i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    points.Append(' ');
                }

                var x = width * i / (values.Count - 1);
                points.Append(N(x)).Append(',').Append(PlotY(values[i], height, ceiling));
            }

            return points.ToString();
        }

        /// <summary>
        /// The same series closed down to the baseline and back — the fill under a
        /// trend line. Empty when <see cref="Polyline"/> is.
        /// </summary>
        public static string AreaPath(IReadOnlyList<decimal> values, decimal width, decimal height, decimal ceiling)
        {
            if (values == null || values.Count == 0)
            {
                return string.Empty;
            }

            if (ceiling <= 0m)
            {
                ceiling = MinimumCeiling;
            }

            var baseline = N(height);
            var path = new StringBuilder("M 0 ").Append(baseline);

            for (var i = 0; i < values.Count; i++)
            {
                var x = values.Count == 1 ? 0m : width * i / (values.Count - 1);
                path.Append(" L ").Append(N(x)).Append(' ').Append(PlotY(values[i], height, ceiling));
            }

            // A single point still has to become an area: it is carried straight
            // across to the right edge, matching Polyline's flat two-point line.
            if (values.Count == 1)
            {
                path.Append(" L ").Append(N(width)).Append(' ').Append(PlotY(values[0], height, ceiling));
            }

            return path.Append(" L ").Append(N(width)).Append(' ').Append(baseline).Append(" Z").ToString();
        }

        /// <summary>
        /// Where a value sits vertically in a plot of <paramref name="height"/>:
        /// zero at the bottom, <paramref name="ceiling"/> at the top. Clamped, for
        /// the same reason <see cref="BarPercent"/> is.
        /// </summary>
        private static string PlotY(decimal value, decimal height, decimal ceiling)
        {
            var ratio = value / ceiling;
            ratio = ratio < 0m ? 0m : ratio > 1m ? 1m : ratio;
            return N(height - (height * ratio));
        }

        private static decimal Pow10(int exponent)
        {
            var result = 1m;
            for (var i = 0; i < Math.Abs(exponent); i++)
            {
                result = exponent > 0 ? result * 10m : result / 10m;
            }

            return result;
        }

        /// <summary>
        /// Invariant, at most three decimals, no trailing zeros and no group
        /// separators — the only number format SVG accepts. Every coordinate this
        /// class emits goes through here; see the class remarks for why that
        /// matters more than it looks.
        /// </summary>
        private static string N(decimal value)
            => Math.Round(value, 3, MidpointRounding.AwayFromZero).ToString("0.###", CultureInfo.InvariantCulture);

        private static string N(double value)
            => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
