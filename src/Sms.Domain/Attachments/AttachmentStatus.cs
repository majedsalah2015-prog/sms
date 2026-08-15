namespace Sms.Domain.Attachments
{
    /// <summary>
    /// BR-ATT-009 quarantine + BR-ATT-007 void-not-delete. Deliberately not
    /// modeled via IActivatable/ISoftActiveFiltered — this status has richer
    /// semantics than a generic on/off toggle.
    /// </summary>
    public enum AttachmentStatus : short
    {
        PendingScan = 1,
        Active = 2,
        Quarantined = 3,
        Void = 4,
    }
}
