namespace Sms.Domain.Notifications
{
    /// <summary>doc 09 §2. Portal push is future (doc 09 §8) — not modeled yet.</summary>
    public enum NotificationChannel : short
    {
        InApp = 1,
        Email = 2,
        Sms = 3,
        WhatsApp = 4,
    }
}
