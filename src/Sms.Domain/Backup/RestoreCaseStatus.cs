namespace Sms.Domain.Backup
{
    /// <summary>BR-BAK-005: request -> scope definition -> support execution -> post-restore verification -> closed.</summary>
    public enum RestoreCaseStatus : short
    {
        Requested = 1,
        ScopeDefined = 2,
        Executed = 3,
        Verified = 4,
        Closed = 5,
    }
}
