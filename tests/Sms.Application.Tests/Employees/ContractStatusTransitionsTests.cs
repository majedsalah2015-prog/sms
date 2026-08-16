using Sms.Application.Employees;
using Sms.Domain.Employees;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Employees
{
    public class ContractStatusTransitionsTests
    {
        [Theory]
        [InlineData(ContractStatus.Draft, ContractStatus.Active)]
        [InlineData(ContractStatus.Active, ContractStatus.Terminated)]
        [BusinessRule("BR-EMP-003")]
        public void Legal_moves_are_allowed(ContractStatus from, ContractStatus to)
        {
            Assert.True(ContractStatusTransitions.CanTransition(from, to));
        }

        [Theory]
        [InlineData(ContractStatus.Draft, ContractStatus.Terminated)]
        [InlineData(ContractStatus.Terminated, ContractStatus.Active)]
        [BusinessRule("BR-EMP-003")]
        public void Illegal_moves_are_rejected(ContractStatus from, ContractStatus to)
        {
            Assert.False(ContractStatusTransitions.CanTransition(from, to));
        }
    }
}
