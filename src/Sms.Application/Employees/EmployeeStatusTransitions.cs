using Sms.Domain.Employees;

namespace Sms.Application.Employees
{
    /// <summary>Pure BR-EMP-001/008 lifecycle — same split as every other status enum in this codebase.</summary>
    public static class EmployeeStatusTransitions
    {
        public static bool CanTransition(EmployeeStatus from, EmployeeStatus to)
        {
            return (from, to) switch
            {
                (EmployeeStatus.Active, EmployeeStatus.Suspended) => true,
                (EmployeeStatus.Suspended, EmployeeStatus.Active) => true,
                (EmployeeStatus.Active, EmployeeStatus.Terminated) => true,
                (EmployeeStatus.Suspended, EmployeeStatus.Terminated) => true,
                _ => false,
            };
        }
    }
}
