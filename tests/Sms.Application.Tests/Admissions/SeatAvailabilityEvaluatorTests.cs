using Sms.Application.Admissions;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Admissions
{
    public class SeatAvailabilityEvaluatorTests
    {
        [Theory]
        [InlineData(30, 0, 30)]
        [InlineData(30, 29, 1)]
        [InlineData(30, 30, 0)]
        [InlineData(30, 31, -1)]
        [BusinessRule("BR-ADM-004")]
        public void RemainingSeats_is_planned_minus_active(int planned, int active, int expected)
        {
            Assert.Equal(expected, SeatAvailabilityEvaluator.RemainingSeats(planned, active));
        }

        [Theory]
        [InlineData(30, 29, true)]
        [InlineData(30, 30, false)]
        [InlineData(30, 31, false)]
        [BusinessRule("BR-ADM-004")]
        public void HasSeat_is_false_once_full(int planned, int active, bool expected)
        {
            Assert.Equal(expected, SeatAvailabilityEvaluator.HasSeat(planned, active));
        }
    }
}
