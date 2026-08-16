namespace Sms.Domain.Employees
{
    /// <summary>Natural expiry (EndDate passed) is derived at read time, not a stored state transition — no scheduled job flips it.</summary>
    public enum ContractStatus : short
    {
        Draft = 1,
        Active = 2,
        Terminated = 3,
    }
}
