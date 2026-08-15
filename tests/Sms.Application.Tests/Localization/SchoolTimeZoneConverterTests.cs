using System;
using Sms.Application.Localization;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Localization
{
    public class SchoolTimeZoneConverterTests
    {
        // Fixed +3, no DST — same offset as Saudi Arabia (Arab Standard Time),
        // built with CreateCustomTimeZone so the test never depends on the
        // host OS's installed tz database (Windows IDs vs IANA IDs).
        private static readonly TimeZoneInfo RiyadhLike =
            TimeZoneInfo.CreateCustomTimeZone("Test/FixedPlus3", TimeSpan.FromHours(3), "Test +3", "Test +3");

        [Fact]
        public void School_local_time_converts_to_the_correct_utc_instant()
        {
            var local = new DateTime(2026, 8, 15, 10, 0, 0);

            var utc = SchoolTimeZoneConverter.ToUtc(local, RiyadhLike);

            Assert.Equal(new DateTime(2026, 8, 15, 7, 0, 0), utc);
        }

        [Fact]
        public void Utc_converts_back_to_the_correct_school_local_time()
        {
            var utc = new DateTime(2026, 8, 15, 7, 0, 0);

            var local = SchoolTimeZoneConverter.ToSchoolLocal(utc, RiyadhLike);

            Assert.Equal(new DateTime(2026, 8, 15, 10, 0, 0), local);
        }

        [Fact]
        public void A_school_local_date_round_trips_through_utc_and_back()
        {
            var local = new DateTime(2026, 8, 15, 14, 30, 0);

            var roundTripped = SchoolTimeZoneConverter.ToSchoolLocal(SchoolTimeZoneConverter.ToUtc(local, RiyadhLike), RiyadhLike);

            Assert.Equal(local, roundTripped);
        }

        [Fact]
        public void The_attendance_day_boundary_spans_exactly_24_hours_of_school_local_time()
        {
            var (startUtc, endUtc) = SchoolTimeZoneConverter.GetSchoolDayBoundariesUtc(new DateTime(2026, 8, 15), RiyadhLike);

            Assert.Equal(new DateTime(2026, 8, 14, 21, 0, 0), startUtc); // 2026-08-15 00:00 local = 2026-08-14 21:00 UTC
            Assert.Equal(new DateTime(2026, 8, 15, 21, 0, 0), endUtc);   // 2026-08-16 00:00 local
            Assert.Equal(TimeSpan.FromDays(1), endUtc - startUtc);
        }

        [Fact]
        public void An_instant_just_before_midnight_local_falls_in_the_prior_days_boundary()
        {
            var (_, endUtc) = SchoolTimeZoneConverter.GetSchoolDayBoundariesUtc(new DateTime(2026, 8, 15), RiyadhLike);
            var justBeforeMidnight = SchoolTimeZoneConverter.ToUtc(new DateTime(2026, 8, 15, 23, 59, 59), RiyadhLike);

            Assert.True(justBeforeMidnight < endUtc);
        }
    }
}
