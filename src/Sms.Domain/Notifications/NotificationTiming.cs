namespace Sms.Domain.Notifications
{
    /// <summary>
    /// BR-NOT-005. Digest batching itself (accumulating a period's events into
    /// one message) needs a scheduler and is deferred to E-011 — this value
    /// just records the school's configured intent per event/channel.
    /// </summary>
    public enum NotificationTiming : short
    {
        Immediate = 1,
        Digest = 2,
    }
}
