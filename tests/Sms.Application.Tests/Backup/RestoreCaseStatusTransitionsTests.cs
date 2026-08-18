using Sms.Application.Backup;
using Sms.Domain.Backup;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Backup
{
    public class RestoreCaseStatusTransitionsTests
    {
        [Theory]
        [InlineData(RestoreCaseStatus.Requested, RestoreCaseStatus.ScopeDefined)]
        [InlineData(RestoreCaseStatus.ScopeDefined, RestoreCaseStatus.Executed)]
        [InlineData(RestoreCaseStatus.Executed, RestoreCaseStatus.Verified)]
        [InlineData(RestoreCaseStatus.Verified, RestoreCaseStatus.Closed)]
        [BusinessRule("BR-BAK-005")]
        public void Legal_moves_are_allowed(RestoreCaseStatus from, RestoreCaseStatus to)
        {
            Assert.True(RestoreCaseStatusTransitions.CanTransition(from, to));
        }

        [Theory]
        [InlineData(RestoreCaseStatus.Requested, RestoreCaseStatus.Executed)]
        [InlineData(RestoreCaseStatus.Requested, RestoreCaseStatus.Closed)]
        [InlineData(RestoreCaseStatus.Closed, RestoreCaseStatus.Requested)]
        [BusinessRule("BR-BAK-005")]
        public void Illegal_moves_are_rejected(RestoreCaseStatus from, RestoreCaseStatus to)
        {
            Assert.False(RestoreCaseStatusTransitions.CanTransition(from, to));
        }
    }
}
