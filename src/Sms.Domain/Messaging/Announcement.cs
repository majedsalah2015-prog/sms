using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Messaging
{
    /// <summary>msg.Announcement (doc/Modules/32 §7, BR-MSG-001): audience resolved and reach-counted at send time (the doc's "recipient list snapshotted at send" — the actual resolved recipient list itself isn't stored, only the count, since the full audience-builder query engine is out of this slice's scope).</summary>
    [Audited(AuditTier.T2)]
    public class Announcement : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public string TitleAr { get; set; } = string.Empty;

        public string TitleEn { get; set; } = string.Empty;

        public string BodyAr { get; set; } = string.Empty;

        public string BodyEn { get; set; } = string.Empty;

        public AudienceScope AudienceScope { get; set; }

        public AnnouncementStatus Status { get; set; } = AnnouncementStatus.Draft;

        public DateTime? ApprovedAtUtc { get; set; }

        public int? ReachCount { get; set; }

        public DateTime? SentAtUtc { get; set; }
    }
}
