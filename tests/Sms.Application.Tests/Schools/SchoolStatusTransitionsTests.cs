using Sms.Application.Schools;
using Sms.Domain.Schools;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Schools
{
    public class SchoolStatusTransitionsTests
    {
        [Theory]
        [InlineData(SchoolStatus.Setup, SchoolStatus.Active)]
        [InlineData(SchoolStatus.Active, SchoolStatus.Suspended)]
        [InlineData(SchoolStatus.Suspended, SchoolStatus.Active)]
        [InlineData(SchoolStatus.Active, SchoolStatus.Closed)]
        [InlineData(SchoolStatus.Suspended, SchoolStatus.Closed)]
        [BusinessRule("BR-SCH-005")]
        public void Legal_moves_are_allowed(SchoolStatus from, SchoolStatus to)
        {
            Assert.True(SchoolStatusTransitions.CanTransition(from, to));
        }

        [Theory]
        [InlineData(SchoolStatus.Setup, SchoolStatus.Suspended)]
        [InlineData(SchoolStatus.Setup, SchoolStatus.Closed)]
        [InlineData(SchoolStatus.Closed, SchoolStatus.Active)]
        [InlineData(SchoolStatus.Closed, SchoolStatus.Setup)]
        [InlineData(SchoolStatus.Active, SchoolStatus.Setup)]
        [BusinessRule("BR-SCH-005")]
        public void Illegal_moves_are_rejected(SchoolStatus from, SchoolStatus to)
        {
            Assert.False(SchoolStatusTransitions.CanTransition(from, to));
        }

        [Fact]
        [BusinessRule("BR-SCH-005")]
        public void Closed_is_terminal()
        {
            foreach (SchoolStatus target in System.Enum.GetValues(typeof(SchoolStatus)))
            {
                Assert.False(SchoolStatusTransitions.CanTransition(SchoolStatus.Closed, target));
            }
        }
    }
}
