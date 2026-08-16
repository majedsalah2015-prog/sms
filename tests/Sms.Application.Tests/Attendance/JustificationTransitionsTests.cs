using Sms.Application.Attendance;
using Sms.Domain.Attendance;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Attendance
{
    public class JustificationTransitionsTests
    {
        [Theory]
        [InlineData(JustificationReviewState.Submitted, JustificationReviewState.Accepted)]
        [InlineData(JustificationReviewState.Submitted, JustificationReviewState.Rejected)]
        [BusinessRule("BR-ATD-005")]
        public void Legal_moves_are_allowed(JustificationReviewState from, JustificationReviewState to)
        {
            Assert.True(JustificationTransitions.CanTransition(from, to));
        }

        [Theory]
        [InlineData(JustificationReviewState.Accepted, JustificationReviewState.Rejected)]
        [InlineData(JustificationReviewState.Rejected, JustificationReviewState.Accepted)]
        [InlineData(JustificationReviewState.Accepted, JustificationReviewState.Submitted)]
        [BusinessRule("BR-ATD-005")]
        public void Illegal_moves_are_rejected(JustificationReviewState from, JustificationReviewState to)
        {
            Assert.False(JustificationTransitions.CanTransition(from, to));
        }
    }
}
