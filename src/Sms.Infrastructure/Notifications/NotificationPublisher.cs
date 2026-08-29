using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Notifications;
using Sms.Domain.Notifications;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Notifications
{
    /// <summary>
    /// Queues one Delivery per recipient per enabled+configured channel.
    /// Never calls SaveChanges (see <see cref="INotificationPublisher"/>) —
    /// the caller's own save commits the queue atomically with the business
    /// event. Missing config (no rule, no template) is a silent skip, not an
    /// error — BR-NOT-003 makes subscription opt-in by design.
    /// </summary>
    public class NotificationPublisher : INotificationPublisher
    {
        private const string SchoolDefaultLanguage = "en"; // stand-in until a School entity carries a real default (S1).

        private readonly AppDbContext _db;
        private readonly IRecipientAddressBook _addresses;

        public NotificationPublisher(AppDbContext db, IRecipientAddressBook addresses)
        {
            _db = db;
            _addresses = addresses;
        }

        public async Task PublishAsync(
            string eventCode,
            IReadOnlyCollection<NotificationRecipient> recipients,
            IReadOnlyDictionary<string, string> payload,
            CancellationToken cancellationToken = default)
        {
            if (recipients.Count == 0)
            {
                return;
            }

            var rules = await _db.SubscriptionRules
                .Where(r => r.EventCode == eventCode && r.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var rule in rules)
            {
                var template = await _db.Templates.SingleOrDefaultAsync(
                    t => t.EventCode == eventCode && t.Channel == rule.Channel && t.IsActive, cancellationToken);
                if (template == null)
                {
                    continue; // configured to notify, but nobody has written the content yet
                }

                var version = await _db.TemplateVersions.SingleAsync(
                    v => v.TemplateId == template.Id && v.VersionNumber == template.CurrentVersionNumber, cancellationToken);

                // Resolved once per channel rather than per recipient: the address book is a
                // query, and a hundred-guardian absence run would otherwise be a hundred of them.
                // A recipient missing from the answer gets a delivery with no address, which the
                // sender fails with a reason a registrar can act on (BR-NTF-005) — deliberately
                // not a silent skip, because a parent who was never written to and a parent whose
                // number is wrong need to look different in the log.
                var addresses = await _addresses.ResolveAsync(
                    recipients.Select(r => r.UserId).Distinct().ToList(), rule.Channel, cancellationToken);

                foreach (var recipient in recipients)
                {
                    var useArabic = string.Equals(
                        string.IsNullOrEmpty(recipient.PreferredLanguage) ? SchoolDefaultLanguage : recipient.PreferredLanguage,
                        "ar", StringComparison.OrdinalIgnoreCase);

                    var subjectTemplate = (useArabic ? version.SubjectAr : version.SubjectEn) ?? string.Empty;
                    var bodyTemplate = useArabic ? version.BodyAr : version.BodyEn;

                    _db.Deliveries.Add(new Delivery
                    {
                        EventCode = eventCode,
                        Channel = rule.Channel,
                        RecipientUserId = recipient.UserId,
                        TemplateVersionId = version.Id,
                        RenderedSubject = TemplateRenderer.Render(subjectTemplate, payload),
                        RenderedBody = TemplateRenderer.Render(bodyTemplate, payload),
                        RecipientAddress = addresses.TryGetValue(recipient.UserId, out var address) ? address : null,
                        Status = DeliveryStatus.Queued,
                    });
                }
            }
        }
    }
}
