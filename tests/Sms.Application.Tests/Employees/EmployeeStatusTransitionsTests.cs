using Sms.Application.Employees;
using Sms.Domain.Employees;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Employees
{
    public class EmployeeStatusTransitionsTests
    {
        [Theory]
        [InlineData(EmployeeStatus.Active, EmployeeStatus.Suspended)]
        [InlineData(EmployeeStatus.Suspended, EmployeeStatus.Active)]
        [InlineData(EmployeeStatus.Active, EmployeeStatus.Terminated)]
        [InlineData(EmployeeStatus.Suspended, EmployeeStatus.Terminated)]
        [BusinessRule("BR-EMP-001")]
        public void Legal_moves_are_allowed(EmployeeStatus from, EmployeeStatus to)
        {
            Assert.True(EmployeeStatusTransitions.CanTransition(from, to));
        }

        [Theory]
        [InlineData(EmployeeStatus.Terminated, EmployeeStatus.Active)]
        [InlineData(EmployeeStatus.Terminated, EmployeeStatus.Suspended)]
        [InlineData(EmployeeStatus.Active, EmployeeStatus.Active)]
        [BusinessRule("BR-EMP-001")]
        public void Illegal_moves_are_rejected(EmployeeStatus from, EmployeeStatus to)
        {
            Assert.False(EmployeeStatusTransitions.CanTransition(from, to));
        }
    }
}
