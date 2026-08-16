namespace Sms.Domain.Sections
{
    /// <summary>BR-SCN-007: a closed section remains in history, never deleted.</summary>
    public enum SectionStatus : short
    {
        Active = 1,
        Closed = 2,
    }
}
