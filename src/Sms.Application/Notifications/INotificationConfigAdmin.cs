using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Notifications;

namespace Sms.Application.Notifications
{
    /// <summary>
    /// doc 09 §5 template editor / subscription matrix backing, and since this
    /// slice the reads the template studio (doc/Modules/33 §8.2) needs to render
    /// what is there before changing it.
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

        // ------------------------------------------------------------------ reads

        /// <summary>
        /// Every template with the state of its newest version, newest first.
        /// <para>
        /// Read past the soft-active filter deliberately: a deactivated template is
        /// exactly the row the studio is asked to show and revive, and reading through
        /// the filter would report it missing and create a second one on top of the
        /// unique index over (school, event, channel).
        /// </para>
        /// </summary>
        Task<IReadOnlyList<TemplateSummary>> ListTemplatesAsync(CancellationToken cancellationToken = default);

        /// <summary>One template with its whole version history, or null if no such template belongs to this school.</summary>
        Task<TemplateDetail?> GetTemplateAsync(int templateId, CancellationToken cancellationToken = default);

        /// <summary>The (event, channel) pairs a template already exists for — so the studio's "new" form does not offer a pair that would collide.</summary>
        Task<IReadOnlyList<(string EventCode, NotificationChannel Channel)>> ListTemplatedPairsAsync(CancellationToken cancellationToken = default);
    }
}
