namespace Sms.Domain.SysAdmin
{
    public enum ImportBatchStatus : short
    {
        DryRun = 1,
        Committed = 2,
        RolledBack = 3,
    }
}
