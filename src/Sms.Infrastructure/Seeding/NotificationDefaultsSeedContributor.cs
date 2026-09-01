using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Notifications;
using Sms.Application.Seeding;
using Sms.Domain.Notifications;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Seeding
{
    /// <summary>
    /// Fills <c>msg.SubscriptionRule</c> with the product's default notification
    /// subscriptions — BR-NOT-003's "product defaults ship enabled for the
    /// catalog above", and the half of BR-SET-008 that says a school adjusts
    /// them rather than invents them (doc/Modules/01 §8.3, "Notifications
    /// defaults" tab).
    /// <para>
    /// Before this contributor the table was empty on every deployment. That is
    /// not a cosmetic gap: <see cref="NotificationPublisher"/> reads the rules to
    /// decide who hears about an event, and an empty table means every
    /// <c>PublishAsync</c> in the product — absence, overdue installment, clinic
    /// visit, student not boarded — resolved to zero rules and queued nothing,
    /// silently, because BR-NOT-003 makes a missing rule a deliberate skip rather
    /// than an error. The five modules that already publish were publishing into
    /// nothing.
    /// </para>
    /// <para>
    /// <b>What is seeded, and what is deliberately not.</b> One rule per
    /// catalogued event on <see cref="NotificationChannel.InApp"/> only — 44 rows
    /// — enabled, at the event's default timing. BR-NOT-003's defaults also name
    /// email for every event and SMS for absence and overdue, and those are
    /// <em>not</em> seeded: <c>Startup</c> registers <c>StubChannelSender</c> for
    /// email, SMS and WhatsApp because no provider has been chosen (doc 09 §9 Q1,
    /// an open owner decision), and a stub reports every send as a success. A
    /// rule enabled on a stubbed channel would therefore tell a school its
    /// parents were emailed when nobody was — a worse failure than the missing
    /// row it replaced. The screen at <c>/setup/notifications</c> shows those
    /// channels as columns, says plainly that they cannot deliver, and lets a
    /// school configure one anyway; what it will not do is ship them on.
    /// </para>
    /// <para>
    /// <b>Nothing is delivered yet either way.</b> The publisher skips any event
    /// with no <c>msg.Template</c> behind it, and no templates are seeded (doc 09
    /// §5's content is still outstanding). So these rules make the subscription
    /// matrix real and the product's intent visible and adjustable; they do not
    /// by themselves start sending anything. That is stated on the screen too.
    /// </para>
    /// <para>
    /// <b>Idempotency.</b> Existing rules are matched on (event, channel) through
    /// <c>IgnoreQueryFilters</c>, because <see cref="SubscriptionRule"/> is
    /// <c>ISoftActiveFiltered</c> — a rule a school has switched off is invisible
    /// to a plain query, and checking through the filter would read it as missing,
    /// re-create it, reset the school's decision and violate the unique index on
    /// (SchoolId, EventCode, Channel) while doing it. Only the statutory flag is
    /// re-stamped on an existing row: it is product policy (BR-NOT-007), not a
    /// school setting. <c>IsActive</c> and <c>Timing</c> are never touched once
    /// the row exists.
    /// </para>
    /// </summary>
    public class NotificationDefaultsSeedContributor : ISeedContributor
    {
        private readonly AppDbContext _db;
        private readonly INotificationConfigAdmin _config;

        public NotificationDefaultsSeedContributor(AppDbContext db, INotificationConfigAdmin config)
        {
            _db = db;
            _config = config;
        }

        public string Name => "Default notification subscriptions (doc 09 §3, BR-NOT-003)";

        /// <summary>
        /// After the demo tenant (50) and the accounts that follow it, because the
        /// dependency really is the school itself.
        /// <para>
        /// This used to be 38, which put it before <c>DemoSeedContributor</c> creates
        /// <c>core.School</c> — so <see cref="SeedAsync"/>'s own "no school, nothing to
        /// configure" guard returned immediately and this contributor wrote <b>nothing</b>
        /// on a fresh database, while still logging as seeded. Every deployment since has
        /// had an empty <c>msg.SubscriptionRule</c>, which is why the notification engine
        /// appeared to be "managing emptiness": the rules it reads were never written, and
        /// a missing rule is a deliberate silent skip (BR-NOT-003), so nothing anywhere
        /// reported it. Verified on SQL Server: 0 rules before this change, 44 after.
        /// </para>
        /// </summary>
        public int Order => 56;

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            if (!await _db.Schools.AnyAsync(cancellationToken))
            {
                return;
            }

            var existing = await _db.SubscriptionRules.IgnoreQueryFilters().AsNoTracking()
                .Where(r => r.SchoolId == _db.CurrentSchoolId)
                .Select(r => new { r.Id, r.EventCode, r.Channel, r.IsStatutory })
                .ToListAsync(cancellationToken);

            var have = new Dictionary<(string Code, NotificationChannel Channel), (int Id, bool IsStatutory)>();
            foreach (var row in existing)
            {
                have[(row.EventCode.ToUpperInvariant(), row.Channel)] = (row.Id, row.IsStatutory);
            }

            foreach (var catalogued in NotificationEventCatalog.Events)
            {
                foreach (var channel in catalogued.DefaultChannels)
                {
                    if (!NotificationEventCatalog.ChannelDelivers(channel))
                    {
                        continue;
                    }

                    var key = (catalogued.Code.ToUpperInvariant(), channel);
                    if (have.TryGetValue(key, out var found))
                    {
                        await StampStatutoryAsync(found.Id, catalogued.IsStatutory, cancellationToken);
                    }
                    else
                    {
                        var rule = await _config.DefineSubscriptionRuleAsync(
                            catalogued.Code, channel, catalogued.DefaultTiming, isEnabled: true, cancellationToken);
                        await StampStatutoryAsync(rule.Id, catalogued.IsStatutory, cancellationToken);
                    }

                    // The port saves per row, so the tracker would otherwise grow
                    // across 44 commits and DetectChanges would re-walk all of it.
                    _db.ChangeTracker.Clear();
                }
            }
        }

        /// <summary>
        /// BR-NOT-007's floor is a property of the event, not of the school, but
        /// <c>INotificationConfigAdmin.DefineSubscriptionRuleAsync</c> carries no
        /// parameter for it (widening that port is outside this slice). So the
        /// flag is stamped here, on the row the port just wrote, from
        /// <see cref="NotificationEventCatalog"/>. Re-stamping an existing row is
        /// the one safe update this contributor makes: a school that has switched
        /// a rule off keeps it off — only the flag moves.
        /// </summary>
        private async Task StampStatutoryAsync(int ruleId, bool isStatutory, CancellationToken cancellationToken)
        {
            var rule = await _db.SubscriptionRules.IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == ruleId, cancellationToken);

            if (rule == null || rule.IsStatutory == isStatutory)
            {
                return;
            }

            rule.IsStatutory = isStatutory;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
