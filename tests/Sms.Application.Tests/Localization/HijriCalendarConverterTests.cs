using System;
using Sms.Application.Localization;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Localization
{
    public class HijriCalendarConverterTests
    {
        // Values confirmed against the actual BCL HijriCalendar output (not
        // hand-computed) — see the class-level gap note on HijriCalendarConverter:
        // this is the tabular Islamic calendar, not UmmAlQuraCalendar, so these
        // are NOT guaranteed to match the official Saudi Umm al-Qura date.
        [Theory]
        [InlineData(2026, 8, 15, 1448, 3, 2)]
        [InlineData(2000, 1, 1, 1420, 9, 25)]
        [InlineData(1990, 1, 1, 1410, 6, 4)]
        public void Converts_known_gregorian_dates_to_their_hijri_equivalent(
            int gy, int gm, int gd, int expectedYear, int expectedMonth, int expectedDay)
        {
            var hijri = HijriCalendarConverter.ToHijri(new DateTime(gy, gm, gd));

            Assert.Equal(expectedYear, hijri.Year);
            Assert.Equal(expectedMonth, hijri.Month);
            Assert.Equal(expectedDay, hijri.Day);
        }

        [Theory]
        [InlineData(2026, 8, 15)]
        [InlineData(2000, 1, 1)]
        [InlineData(1990, 1, 1)]
        public void Round_trips_gregorian_through_hijri_and_back(int y, int m, int d)
        {
            var original = new DateTime(y, m, d);

            var roundTripped = HijriCalendarConverter.ToGregorian(HijriCalendarConverter.ToHijri(original));

            Assert.Equal(original, roundTripped);
        }

        [Fact]
        public void Consecutive_gregorian_days_never_move_the_hijri_date_backwards()
        {
            var previous = HijriCalendarConverter.ToHijri(new DateTime(2026, 1, 1));

            for (var day = 2; day <= 366; day++)
            {
                var current = HijriCalendarConverter.ToHijri(new DateTime(2026, 1, 1).AddDays(day - 1));
                var currentTotal = current.Year * 12 + current.Month;
                var previousTotal = previous.Year * 12 + previous.Month;

                Assert.True(
                    currentTotal > previousTotal || (currentTotal == previousTotal && current.Day >= previous.Day),
                    $"Hijri date moved backwards from {previous.Year}-{previous.Month}-{previous.Day} to {current.Year}-{current.Month}-{current.Day}");
                previous = current;
            }
        }
    }
}
