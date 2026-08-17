using Sms.Application.Grading;
using Sms.Domain.Grading;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Grading
{
    public class MarksheetStatusTransitionsTests
    {
        [Theory]
        [InlineData(MarksheetStatus.Draft, MarksheetStatus.Submitted)]
        [InlineData(MarksheetStatus.Submitted, MarksheetStatus.HoDReviewed)]
        [InlineData(MarksheetStatus.HoDReviewed, MarksheetStatus.Approved)]
        [InlineData(MarksheetStatus.Approved, MarksheetStatus.Published)]
        [InlineData(MarksheetStatus.Published, MarksheetStatus.Draft)]
        [BusinessRule("BR-GRA-005")]
        public void Legal_moves_are_allowed(MarksheetStatus from, MarksheetStatus to)
        {
            Assert.True(MarksheetStatusTransitions.CanTransition(from, to));
        }

        [Theory]
        [InlineData(MarksheetStatus.Draft, MarksheetStatus.Approved)]
        [InlineData(MarksheetStatus.Draft, MarksheetStatus.Published)]
        [InlineData(MarksheetStatus.Submitted, MarksheetStatus.Approved)]
        [InlineData(MarksheetStatus.Submitted, MarksheetStatus.Draft)]
        [BusinessRule("BR-GRA-005")]
        public void Illegal_moves_are_rejected(MarksheetStatus from, MarksheetStatus to)
        {
            Assert.False(MarksheetStatusTransitions.CanTransition(from, to));
        }
    }
}
