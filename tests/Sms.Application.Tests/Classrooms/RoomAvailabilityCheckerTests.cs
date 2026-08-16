using System;
using System.Collections.Generic;
using Sms.Application.Classrooms;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Classrooms
{
    public class RoomAvailabilityCheckerTests
    {
        [Fact]
        [BusinessRule("BR-ROM-004")]
        public void A_room_with_no_exceptions_is_available()
        {
            Assert.True(RoomAvailabilityChecker.IsAvailable(
                new DateTime(2026, 9, 1), new DateTime(2026, 9, 2), Array.Empty<(DateTime, DateTime)>()));
        }

        [Fact]
        [BusinessRule("BR-ROM-004")]
        public void A_window_overlapping_a_maintenance_range_is_unavailable()
        {
            var exceptions = new[] { (new DateTime(2026, 9, 1), new DateTime(2026, 9, 10)) };

            Assert.False(RoomAvailabilityChecker.IsAvailable(new DateTime(2026, 9, 5), new DateTime(2026, 9, 6), exceptions));
        }

        [Fact]
        [BusinessRule("BR-ROM-004")]
        public void A_window_immediately_after_maintenance_ends_is_available()
        {
            var exceptions = new[] { (new DateTime(2026, 9, 1), new DateTime(2026, 9, 10)) };

            Assert.True(RoomAvailabilityChecker.IsAvailable(new DateTime(2026, 9, 10), new DateTime(2026, 9, 11), exceptions));
        }
    }
}
