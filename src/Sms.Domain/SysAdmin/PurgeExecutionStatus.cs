namespace Sms.Domain.SysAdmin
{
    public enum PurgeExecutionStatus : short
    {
        Requested = 1,
        Approved = 2,
        Executed = 3,
        Blocked = 4,
    }
}
