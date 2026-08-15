namespace Sms.Domain.Schools
{
    /// <summary>BR-SCH-005: Setup → Active → Suspended → Closed; Closed is terminal.</summary>
    public enum SchoolStatus : short
    {
        Setup = 1,
        Active = 2,
        Suspended = 3,
        Closed = 4,
    }
}
