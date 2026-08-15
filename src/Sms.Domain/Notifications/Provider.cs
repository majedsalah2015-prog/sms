using Sms.Domain.Common;

namespace Sms.Domain.Notifications
{
    /// <summary>
    /// msg.Provider (BR-NOT-009): per-school registry of which gateway serves
    /// a channel. Deliberately holds no credentials — secrets storage/config
    /// is a separate, not-yet-designed concern; this is a toggle/registry
    /// row, matched at dispatch time against the channel's <see cref="Application.Notifications.IChannelSender"/>.
    /// </summary>
    public class Provider : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public NotificationChannel Channel { get; set; }

        public string ProviderCode { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
