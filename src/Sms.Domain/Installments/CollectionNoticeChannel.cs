namespace Sms.Domain.Installments
{
    /// <summary>
    /// How a collection notice reached the family (doc/Modules/20 §8.5 "pending
    /// letter batches"). Two ways, because a school's collection officer has
    /// exactly two: hand the guardian a sheet of paper, or put a message in
    /// front of them the next time they open the portal.
    /// <para>
    /// Deliberately not <c>NotificationChannel</c>. That enum is the notification
    /// engine's dispatch route — InApp/Email/Sms/WhatsApp, each with a provider
    /// behind it — and paper has no provider and no delivery status. What this
    /// enum records is the *act* the officer performed, which is why a paper
    /// notice is a row here and never a <c>Delivery</c>.
    /// </para>
    /// </summary>
    public enum CollectionNoticeChannel : short
    {
        /// <summary>Printed and handed over. The system records that it was issued; it cannot know it was read.</summary>
        Paper = 1,

        /// <summary>Queued as an in-app notification to the guardian's portal account, through doc 09's engine.</summary>
        Portal = 2,
    }
}
