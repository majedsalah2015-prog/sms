using Sms.Application.Fees;
using Sms.Domain.Fees;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Fees
{
    public class FeeStructureLineStatusTransitionsTests
    {
        [Fact]
        [BusinessRule("BR-FEE-002")]
        public void Draft_to_approved_is_legal()
        {
            Assert.True(FeeStructureLineStatusTransitions.CanTransition(FeeStructureLineStatus.Draft, FeeStructureLineStatus.Approved));
        }

        [Fact]
        [BusinessRule("BR-FEE-002")]
        public void Approved_to_draft_is_illegal()
        {
            Assert.False(FeeStructureLineStatusTransitions.CanTransition(FeeStructureLineStatus.Approved, FeeStructureLineStatus.Draft));
        }
    }
}
