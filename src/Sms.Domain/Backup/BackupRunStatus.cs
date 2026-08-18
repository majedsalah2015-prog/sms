namespace Sms.Domain.Backup
{
    public enum BackupRunStatus : short
    {
        Complete = 1,
        Degraded = 2,
        Failed = 3,
    }
}
