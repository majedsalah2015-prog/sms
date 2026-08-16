using Sms.Application.Attendance;
using Sms.Domain.Attendance;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Attendance
{
    public class LeavePassTransitionsTests
    {
        [Theory]
        [InlineData(LeavePassStatus.Requested, LeavePassStatus.Approved)]
        [InlineData(LeavePassStatus.Requested, LeavePassStatus.Rejected)]
        [InlineData(LeavePassStatus.Approved, LeavePassStatus.Released)]
        [InlineData(LeavePassStatus.Approved, LeavePassStatus.Rejected)]
        [InlineData(LeavePassStatus.Released, LeavePassStatus.Returned)]
        [BusinessRule("BR-ATD-006")]
        public void Legal_moves_are_allowed(LeavePassStatus from, LeavePassStatus to)
        {
            Assert.True(LeavePassTransitions.CanTransition(from, to));
        }

        [Theory]
        [InlineData(LeavePassStatus.Requested, LeavePassStatus.Released)]
        [InlineData(LeavePassStatus.Requested, LeavePassStatus.Returned)]
        [InlineData(LeavePassStatus.Released, LeavePassStatus.Approved)]
        [InlineData(LeavePassStatus.Rejected, LeavePassStatus.Approved)]
        [BusinessRule("BR-ATD-006")]
        public void Illegal_moves_are_rejected(LeavePassStatus from, LeavePassStatus to)
        {
            Assert.False(LeavePassTransitions.CanTransition(from, to));
        }
    }
}
