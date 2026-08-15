namespace Sms.Domain.Attachments
{
    /// <summary>BR-ATT-009: a version is never downloadable while Pending or Infected.</summary>
    public enum ScanStatus : short
    {
        Pending = 1,
        Clean = 2,
        Infected = 3,
    }
}
