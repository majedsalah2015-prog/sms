namespace Sms.Domain.Employees
{
    /// <summary>doc/Modules/12 §3 (BR-EMP-001/008): simplified lifecycle — full offboarding clearance workflow is deferred.</summary>
    public enum EmployeeStatus : short
    {
        Active = 1,
        Suspended = 2,
        Terminated = 3,
    }
}
