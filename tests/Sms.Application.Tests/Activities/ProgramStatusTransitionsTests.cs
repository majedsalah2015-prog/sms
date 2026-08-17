using Sms.Application.Activities;
using Sms.Domain.Activities;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Activities
{
    public class ProgramStatusTransitionsTests
    {
        [Theory]
        [InlineData(ProgramStatus.Proposed, ProgramStatus.Approved)]
        [InlineData(ProgramStatus.Approved, ProgramStatus.Active)]
        [InlineData(ProgramStatus.Active, ProgramStatus.Closed)]
        public void Legal_moves_are_allowed(ProgramStatus from, ProgramStatus to)
        {
            Assert.True(ProgramStatusTransitions.CanTransition(from, to));
        }

        [Theory]
        [InlineData(ProgramStatus.Proposed, ProgramStatus.Active)]
        [InlineData(ProgramStatus.Closed, ProgramStatus.Active)]
        public void Illegal_moves_are_rejected(ProgramStatus from, ProgramStatus to)
        {
            Assert.False(ProgramStatusTransitions.CanTransition(from, to));
        }
    }
}
