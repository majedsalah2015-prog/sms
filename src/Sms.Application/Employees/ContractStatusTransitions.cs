using Sms.Domain.Employees;

namespace Sms.Application.Employees
{
    /// <summary>Pure BR-EMP-003 lifecycle: Draft (HR drafts) -> Active (Principal approves, P2 not enforced here) -> Terminated (early end).</summary>
    public static class ContractStatusTransitions
    {
        public static bool CanTransition(ContractStatus from, ContractStatus to)
        {
            return (from, to) switch
            {
                (ContractStatus.Draft, ContractStatus.Active) => true,
                (ContractStatus.Active, ContractStatus.Terminated) => true,
                _ => false,
            };
        }
    }
}
