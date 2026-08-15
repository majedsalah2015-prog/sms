namespace Sms.Application.Notifications
{
    /// <summary>
    /// Doc 09 §2 "Recipient Resolution" is the publishing module's job (e.g.
    /// guardianship links honoring custody restrictions, BR-SEC-011) — it
    /// hands the engine a resolved user + language, not a role/scope query.
    /// </summary>
    public sealed class NotificationRecipient
    {
        public NotificationRecipient(int userId, string preferredLanguage)
        {
            UserId = userId;
            PreferredLanguage = preferredLanguage;
        }

        public int UserId { get; }

        /// <summary>"ar" or "en" (BR-NOT-001); anything else falls back to the school default.</summary>
        public string PreferredLanguage { get; }
    }
}
