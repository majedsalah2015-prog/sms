using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.Messaging;
using Sms.Application.Notifications;
using Sms.Domain.Messaging;
using Sms.Domain.Notifications;

namespace Sms.Web.Models
{
    /// <summary>doc/Modules/33 §8.6 — the signed-in user's own in-app inbox.</summary>
    public class NotificationCentreViewModel
    {
        public IReadOnlyList<InboxItem> Items { get; set; } = Array.Empty<InboxItem>();

        public bool IncludeRead { get; set; }

        public int UnreadCount { get; set; }
    }

    /// <summary>doc/Modules/33 §8.2 — the studio's list, plus what a new template may be written for.</summary>
    public class TemplateStudioViewModel
    {
        public IReadOnlyList<TemplateSummary> Templates { get; set; } = Array.Empty<TemplateSummary>();

        public IReadOnlyList<(string EventCode, NotificationChannel Channel)> TakenPairs { get; set; } =
            Array.Empty<(string, NotificationChannel)>();

        /// <summary>
        /// Whether a template already exists for this pair. The new-template form still
        /// offers it — <c>DefineTemplateAsync</c> writes a new version rather than colliding —
        /// but the screen says so, because "add" and "add a version to the existing one" are
        /// not the same intention.
        /// </summary>
        public bool IsTaken(string eventCode, NotificationChannel channel)
            => TakenPairs.Any(p => p.Channel == channel && string.Equals(p.EventCode, eventCode, StringComparison.Ordinal));
    }

    /// <summary>doc/Modules/33 §8.2 — one template, its versions, and the placeholders its event supplies.</summary>
    public class TemplateEditorViewModel
    {
        public TemplateDetail Detail { get; set; } = null!;

        public IReadOnlyList<string> Placeholders { get; set; } = Array.Empty<string>();

        /// <summary>
        /// True when the event has no publisher yet, so nothing validates its placeholders.
        /// The editor says so rather than showing an empty picker that reads as "this event
        /// supplies nothing".
        /// </summary>
        public bool PlaceholdersUnknown => Placeholders.Count == 0;
    }

    /// <summary>doc/Modules/33 §8.3 — the registered gateways and what they are missing.</summary>
    public class ProviderConsoleViewModel
    {
        public IReadOnlyList<Provider> Providers { get; set; } = Array.Empty<Provider>();

        /// <summary>The school's dialling code, shown because it is what completes a parent's national-format mobile — a blank one is why WhatsApp reaches nobody.</summary>
        public string? DiallingCode { get; set; }

        public IReadOnlyList<ProviderCatalog.GatewayDefinition> Gateways => ProviderCatalog.Gateways;

        public IReadOnlyList<Provider> For(NotificationChannel channel)
            => Providers.Where(p => p.Channel == channel).ToList();

        /// <summary>The next free failover rank on a channel — the unique index means the form must not propose one already taken.</summary>
        public int NextFailoverOrder(NotificationChannel channel)
        {
            var used = Providers.Where(p => p.Channel == channel).Select(p => p.FailoverOrder).ToList();
            var candidate = 1;
            while (used.Contains(candidate))
            {
                candidate++;
            }

            return candidate;
        }
    }

    /// <summary>doc/Modules/33 §8.4 — the delivery log and its filters.</summary>
    public class DeliveryOperationsViewModel
    {
        public IReadOnlyList<DeliveryRow> Rows { get; set; } = Array.Empty<DeliveryRow>();

        public DeliveryTotals Totals { get; set; } = new(0, 0, 0, 0);

        public DeliveryStatus? Status { get; set; }

        public NotificationChannel? Channel { get; set; }

        public string? EventCode { get; set; }

        /// <summary>The page is capped at 200; say so when it is full rather than implying it is all there is.</summary>
        public bool IsCapped => Rows.Count >= 200;
    }

    /// <summary>doc/Modules/33 §8.5 — spend against ceiling for one month (BR-NTF-004).</summary>
    public class BudgetConsoleViewModel
    {
        public string PeriodKey { get; set; } = string.Empty;

        public IReadOnlyList<BudgetRow> Rows { get; set; } = Array.Empty<BudgetRow>();

        public bool HardStopEnabled => Rows.Any(r => r.HardStopEnabled);

        public int LimitFor(NotificationChannel channel)
            => Rows.FirstOrDefault(r => r.Channel == channel)?.Limit ?? 0;
    }

    /// <summary>doc/Modules/32 §8.1 — the announcement register.</summary>
    public class AnnouncementListViewModel
    {
        public IReadOnlyList<AnnouncementSummary> Announcements { get; set; } = Array.Empty<AnnouncementSummary>();
    }

    /// <summary>doc/Modules/32 §8.1 — the compose form, its audience builder and its cost estimate.</summary>
    public class ComposeAnnouncementViewModel
    {
        public string? TitleAr { get; set; }

        public string? TitleEn { get; set; }

        public string? BodyAr { get; set; }

        public string? BodyEn { get; set; }

        public AudienceScope Scope { get; set; } = AudienceScope.Section;

        public int? TargetId { get; set; }

        public int ChannelMask { get; set; }

        public IReadOnlyList<AudienceOption> Targets { get; set; } = Array.Empty<AudienceOption>();

        /// <summary>Null until a target is chosen — school-wide needs none, the rest do.</summary>
        public AudiencePreview? Preview { get; set; }

        public IReadOnlyList<Provider> Providers { get; set; } = Array.Empty<Provider>();

        public bool Picked(NotificationChannel channel) => AnnouncementChannels.Includes(ChannelMask, channel);

        /// <summary>
        /// Whether a metered channel could actually deliver: the transport exists, but only a
        /// registered, configured, active gateway makes it real. The picker shows the tick
        /// either way and says what would happen — a school ticking WhatsApp with no gateway
        /// should be told now, not by an empty inbox tomorrow.
        /// </summary>
        public bool CanDeliverOn(NotificationChannel channel)
            => channel == NotificationChannel.InApp
               || Providers.Any(p => p.Channel == channel && p.IsActive && p.IsConfigured);
    }
}
