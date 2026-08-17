using System;
using System.Linq;
using Sms.Application.Timetable;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Timetable
{
    public class SessionGeneratorTests
    {
        [Fact]
        [BusinessRule("BR-TTB-006")]
        public void Generates_one_session_per_matching_weekday_in_range()
        {
            var rangeStart = new DateTime(2027, 9, 1);
            var rangeEnd = rangeStart.AddDays(13); // two full weeks
            var placements = new[] { new SessionGenerator.PlacementSlot(placementId: 1, dayOfWeek: rangeStart.DayOfWeek) };

            var sessions = SessionGenerator.Generate(rangeStart, rangeEnd, placements, isWorkingDay: _ => true).ToList();

            Assert.Equal(2, sessions.Count);
            Assert.All(sessions, s => Assert.Equal(rangeStart.DayOfWeek, s.Date.DayOfWeek));
        }

        [Fact]
        [BusinessRule("BR-TTB-006")]
        public void Non_working_days_are_skipped()
        {
            var rangeStart = new DateTime(2027, 9, 1);
            var rangeEnd = rangeStart.AddDays(13);
            var placements = new[] { new SessionGenerator.PlacementSlot(placementId: 1, dayOfWeek: rangeStart.DayOfWeek) };

            var sessions = SessionGenerator.Generate(rangeStart, rangeEnd, placements, isWorkingDay: d => d != rangeStart).ToList();

            Assert.Single(sessions);
            Assert.Equal(rangeStart.AddDays(7), sessions[0].Date);
        }

        [Fact]
        [BusinessRule("BR-TTB-006")]
        public void Placements_on_other_weekdays_do_not_generate_on_a_non_matching_day()
        {
            var rangeStart = new DateTime(2027, 9, 1);
            var rangeEnd = rangeStart;
            var otherWeekday = (DayOfWeek)(((int)rangeStart.DayOfWeek + 1) % 7);
            var placements = new[] { new SessionGenerator.PlacementSlot(placementId: 1, dayOfWeek: otherWeekday) };

            var sessions = SessionGenerator.Generate(rangeStart, rangeEnd, placements, isWorkingDay: _ => true).ToList();

            Assert.Empty(sessions);
        }
    }
}
