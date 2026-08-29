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
            // Past the soft-active filter with the school predicate restored by hand, for the
            // reason DefineSubscriptionRuleAsync below spells out: Template is
            // ISoftActiveFiltered, so a template a school has retired is invisible to a plain
            // query, and reading it as missing inserts a second row that dies on the unique
            // index over (SchoolId, EventCode, Channel) — as a DbUpdateException no
            // controller's catch would have translated.
            var template = await _db.Templates.IgnoreQueryFilters().SingleOrDefaultAsync(
                t => t.SchoolId == _db.CurrentSchoolId && t.EventCode == eventCode && t.Channel == channel, cancellationToken);

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
            // Past the soft-active filter, with the school predicate back on by hand.
            // The rule's IsActive flag *is* the school's on/off switch, so a disabled
            // rule is exactly the one this method is most often asked to touch — and
            // reading through the filter made it invisible, so re-enabling it inserted
            // a second row and died on the unique index over (school, event, channel).
            // A DbUpdateException, at that: not an InvalidOperationException, so no
            // controller's catch would have translated it either.
            var rule = await _db.SubscriptionRules.IgnoreQueryFilters().SingleOrDefaultAsync(
                r => r.SchoolId == _db.CurrentSchoolId && r.EventCode == eventCode && r.Channel == channel, cancellationToken);

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

        // ------------------------------------------------------------------ reads

        public async Task<IReadOnlyList<TemplateSummary>> ListTemplatesAsync(CancellationToken cancellationToken = default)
        {
            // IgnoreQueryFilters with the school predicate restored by hand: a deactivated
            // template is exactly the row the studio must be able to show and revive, and the
            // soft-active filter would report it missing.
            var templates = await _db.Templates
                .IgnoreQueryFilters()
                .Where(t => t.SchoolId == _db.CurrentSchoolId)
                .ToListAsync(cancellationToken);

            if (templates.Count == 0)
            {
                return new List<TemplateSummary>();
            }

            var templateIds = templates.Select(t => t.Id).ToList();
            var versions = await _db.TemplateVersions
                .Where(v => templateIds.Contains(v.TemplateId))
                .ToListAsync(cancellationToken);

            var latestByTemplate = versions
                .GroupBy(v => v.TemplateId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.VersionNumber).First());

            return templates
                // A template with no version at all cannot happen through DefineTemplateAsync,
                // which writes both in one save — but a hand-inserted row would, and a studio
                // that throws on one is worse than one that does not list it.
                .Where(t => latestByTemplate.ContainsKey(t.Id))
                .Select(t =>
                {
                    var latest = latestByTemplate[t.Id];
                    return new TemplateSummary(
                        t.Id, t.EventCode, t.Channel, t.IsActive, t.CurrentVersionNumber,
                        latest.Id, latest.VersionNumber, latest.PublishStatus, latest.ModifiedAtUtc ?? latest.CreatedAtUtc);
                })
                .OrderByDescending(t => t.LatestModifiedAtUtc)
                .ToList();
        }

        public async Task<TemplateDetail?> GetTemplateAsync(int templateId, CancellationToken cancellationToken = default)
        {
            var template = await _db.Templates
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(t => t.Id == templateId && t.SchoolId == _db.CurrentSchoolId, cancellationToken);

            if (template == null)
            {
                return null;
            }

            var versions = await _db.TemplateVersions
                .Where(v => v.TemplateId == templateId)
                .OrderByDescending(v => v.VersionNumber)
                .ToListAsync(cancellationToken);

            return versions.Count == 0 ? null : new TemplateDetail(template, versions, versions[0]);
        }

        public async Task<IReadOnlyList<(string EventCode, NotificationChannel Channel)>> ListTemplatedPairsAsync(
            CancellationToken cancellationToken = default)
        {
            var pairs = await _db.Templates
                .IgnoreQueryFilters()
                .Where(t => t.SchoolId == _db.CurrentSchoolId)
                .Select(t => new { t.EventCode, t.Channel })
                .ToListAsync(cancellationToken);

            return pairs.Select(p => (p.EventCode, p.Channel)).ToList();
        }
    }
}
