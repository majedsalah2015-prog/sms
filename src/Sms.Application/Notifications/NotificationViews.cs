using System;
using System.Collections.Generic;
using Sms.Domain.Notifications;

namespace Sms.Application.Notifications
{
    /// <summary>
    /// One row of the template studio's list — a template and the state of its
    /// latest version, which is the pair the screen shows and neither entity
    /// answers alone.
    /// </summary>
    public sealed record TemplateSummary(
        int TemplateId,
        string EventCode,
        NotificationChannel Channel,
        bool IsActive,
        int CurrentVersionNumber,
        int LatestVersionId,
        int LatestVersionNumber,
        TemplatePublishStatus LatestStatus,
        DateTime LatestModifiedAtUtc)
    {
        /// <summary>
        /// Whether the publisher would actually use this template today. A template
        /// whose latest version is still a draft is not live — the publisher renders
        /// <c>CurrentVersionNumber</c>, which only moves on publish.
        /// </summary>
        public bool HasLiveVersion => LatestStatus == TemplatePublishStatus.Published
                                      || CurrentVersionNumber != LatestVersionNumber;
    }

    /// <summary>A template with its whole version history — the studio's editor view (doc/Modules/33 §8.2).</summary>
    public sealed record TemplateDetail(
        Template Template,
        IReadOnlyList<TemplateVersion> Versions,
        TemplateVersion Latest);

    /// <summary>
    /// One delivery as the operations log shows it (doc/Modules/33 §8.4), with the
    /// recipient named rather than left as an id.
    /// <para>
    /// <c>AddressMasked</c> rather than the address: the log answers "did it go out",
    /// and whoever may ask that is not automatically someone who may harvest every
    /// parent's mobile number off one screen.
    /// </para>
    /// </summary>
    public sealed record DeliveryRow(
        int DeliveryId,
        string EventCode,
        NotificationChannel Channel,
        int RecipientUserId,
        string RecipientUserName,
        string? AddressMasked,
        string RenderedSubject,
        DeliveryStatus Status,
        int AttemptCount,
        DateTime? LastAttemptAtUtc,
        string? FailureReason,
        DateTime QueuedAtUtc);

    /// <summary>What the delivery-operations header counts, so the screen does not tally a paged list.</summary>
    public sealed record DeliveryTotals(int Queued, int Sent, int Delivered, int Failed);

    /// <summary>One row of a user's own notification centre (doc/Modules/33 §8.6).</summary>
    public sealed record InboxItem(
        int DeliveryId,
        string EventCode,
        string Subject,
        string Body,
        bool IsRead,
        DateTime ReceivedAtUtc);

    /// <summary>
    /// A channel's spend against its ceiling for one period (doc/Modules/33 §8.5,
    /// BR-NTF-004). <paramref name="Limit"/> is null when the school has set none —
    /// which is not the same as zero, and the console must not draw it as a full bar.
    /// </summary>
    public sealed record BudgetRow(
        NotificationChannel Channel,
        string PeriodKey,
        int MessageCount,
        int? Limit,
        bool HardStopEnabled)
    {
        public bool HasLimit => Limit is > 0;

        public int PercentUsed => HasLimit ? (int)Math.Min(100, Math.Round(MessageCount * 100m / Limit!.Value)) : 0;

        /// <summary>BR-NTF-004's 80% alert.</summary>
        public bool IsAlerting => HasLimit && MessageCount * 100 >= Limit!.Value * 80;

        public bool IsOverLimit => HasLimit && MessageCount >= Limit!.Value;
    }
}
