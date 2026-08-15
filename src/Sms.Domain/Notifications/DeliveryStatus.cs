namespace Sms.Domain.Notifications
{
    /// <summary>doc 09 §2 delivery log lifecycle.</summary>
    public enum DeliveryStatus : short
    {
        Queued = 1,
        Sent = 2,
        Delivered = 3,
        Failed = 4,
    }
}
