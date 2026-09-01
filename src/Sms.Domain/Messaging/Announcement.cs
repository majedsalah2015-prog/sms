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

        /// <summary>
        /// Which section, grade or stage — null for <see cref="AudienceScope.SchoolWide"/>,
        /// which needs no target.
        /// <para>
        /// Added with the compose screen (doc/Modules/32 §8.1's "audience builder with live
        /// count"). Before it, the scope said <em>how wide</em> a send was but never
        /// <em>whose</em>, so the reach count could only be a number the caller passed in
        /// and no audience could actually be resolved. Deliberately not a foreign key: it
        /// points at a different table per scope, and a nullable FK to three tables at once
        /// is a worse lie than an int the resolver reads with the scope in hand.
        /// </para>
        /// </summary>
        public int? AudienceTargetId { get; set; }

        /// <summary>
        /// The channels this announcement was sent on, as bit flags over
        /// <c>NotificationChannel</c> — see <c>AnnouncementChannels</c>. Zero means the
        /// portal only, which is what an announcement was before it could pick a channel.
        /// </summary>
        public int ChannelMask { get; set; }

        public AnnouncementStatus Status { get; set; } = AnnouncementStatus.Draft;

        public DateTime? ApprovedAtUtc { get; set; }

        /// <summary>How many people it actually reached — set from the resolved audience at send, not supplied by the caller.</summary>
        public int? ReachCount { get; set; }

        public DateTime? SentAtUtc { get; set; }
    }
}
