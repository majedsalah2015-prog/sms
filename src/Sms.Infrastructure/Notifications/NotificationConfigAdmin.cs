using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Notifications;
using Sms.Domain.Notifications;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Notifications
{
    /// <summary>Standalone admin operations — saves themselves, no larger transaction to ride.</summary>
    public class NotificationConfigAdmin : INotificationConfigAdmin
    {
        private readonly AppDbContext _db;

        public NotificationConfigAdmin(AppDbContext db)
        {
            _db = db;
        }

        public async Task<TemplateVersion> DefineTemplateAsync(
            string eventCode,
            NotificationChannel channel,
            string? subjectAr,
            string? subjectEn,
            string bodyAr,
            string bodyEn,
            CancellationToken cancellationToken = default)
        {
            var template = await _db.Templates.SingleOrDefaultAsync(
                t => t.EventCode == eventCode && t.Channel == channel, cancellationToken);

            if (template == null)
            {
                template = new Template { EventCode = eventCode, Channel = channel, CurrentVersionNumber = 1 };
                _db.Templates.Add(template);
            }
            else
            {
                template.IsActive = true;
                template.CurrentVersionNumber += 1;
            }

            var version = new TemplateVersion
            {
                VersionNumber = template.CurrentVersionNumber,
                SubjectAr = subjectAr,
                SubjectEn = subjectEn,
                BodyAr = bodyAr,
                BodyEn = bodyEn,
            };
            // Added via the navigation collection (not TemplateId directly) so EF
            // fixes up the FK once `template` gets its own id in this same save —
            // needed the first time, harmless afterwards.
            template.Versions.Add(version);

            await _db.SaveChangesAsync(cancellationToken);
            return version;
        }

        public async Task<SubscriptionRule> DefineSubscriptionRuleAsync(
            string eventCode,
            NotificationChannel channel,
            NotificationTiming timing,
            bool isEnabled,
            CancellationToken cancellationToken = default)
        {
            var rule = await _db.SubscriptionRules.SingleOrDefaultAsync(
                r => r.EventCode == eventCode && r.Channel == channel, cancellationToken);

            if (rule == null)
            {
                rule = new SubscriptionRule { EventCode = eventCode, Channel = channel };
                _db.SubscriptionRules.Add(rule);
            }

            rule.Timing = timing;
            rule.IsActive = isEnabled;

            await _db.SaveChangesAsync(cancellationToken);
            return rule;
        }
    }
}
