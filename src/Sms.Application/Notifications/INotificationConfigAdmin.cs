using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Notifications;

namespace Sms.Application.Notifications
{
    /// <summary>
    /// doc 09 §5 Template editor / subscription matrix backing (screens
    /// deferred, the operations themselves are core). Permission-gating is a
    /// later slice, same deferral as Configure-Security (E-003) and
    /// Configure-Series (E-006).
    /// </summary>
    public interface INotificationConfigAdmin
    {
        /// <summary>BR-NOT-008: always creates a new version — an edit never rewrites content already sent.</summary>
        Task<TemplateVersion> DefineTemplateAsync(
            string eventCode,
            NotificationChannel channel,
            string? subjectAr,
            string? subjectEn,
            string bodyAr,
            string bodyEn,
            CancellationToken cancellationToken = default);

        /// <summary>Upserts the school's on/off + timing switch for (event, channel) — BR-NOT-003.</summary>
        Task<SubscriptionRule> DefineSubscriptionRuleAsync(
            string eventCode,
            NotificationChannel channel,
            NotificationTiming timing,
            bool isEnabled,
            CancellationToken cancellationToken = default);
    }
}
