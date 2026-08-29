using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Messaging;

namespace Sms.Application.Messaging
{
    /// <summary>One pickable audience target — a section, a grade or a stage — with how many people it currently reaches.</summary>
    public sealed record AudienceOption(int Id, string NameAr, string NameEn, int RecipientCount);

    /// <summary>
    /// What a proposed send would actually do: how many guardians it resolves to, and
    /// how many metered messages that buys. The compose screen shows both before the
    /// button is pressed, which is doc/Modules/32 §8.1's "live count" and §14 Q4's
    /// sender-level cost visibility.
    /// </summary>
    public sealed record AudiencePreview(int RecipientCount, int CostedMessageCount, bool RequiresApproval);

    /// <summary>One announcement as the list shows it, with its reach already counted.</summary>
    public sealed record AnnouncementSummary(
        Announcement Announcement,
        string AudienceLabelAr,
        string AudienceLabelEn,
        int DeliveryCount);

    /// <summary>
    /// doc/Modules/32 §8 Compose announcement / Inbox-threads / Official
    /// letter center screens backing (screens deferred, the operations
    /// are core). Delivery itself rides doc 09's channel infrastructure
    /// (E-007) — not re-implemented here; ReachCount/Delivery wiring is a
    /// straightforward follow-up once a screen actually needs to show
    /// per-recipient delivery status. Abuse reporting/moderation
    /// (BR-MSG-006) is deferred entirely.
    /// </summary>
    public interface IMessagingAdmin
    {
        /// <summary>CreatedByUserId is stamped automatically from the ambient ICurrentUser (AuditableEntity's own mechanism) — not a parameter here.</summary>
        Task<Announcement> DefineAnnouncementAsync(
            string titleAr, string titleEn, string bodyAr, string bodyEn, AudienceScope audienceScope,
            int? audienceTargetId = null, int channelMask = 0,
            CancellationToken cancellationToken = default);

        Task ApproveAnnouncementAsync(int announcementId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves the audience, queues one delivery per guardian per picked channel, and
        /// stamps the announcement sent with the count it actually reached.
        /// <para>
        /// This is the send that makes an announcement a message rather than a record of
        /// one. It used to take the reach count as a parameter and write nothing to the
        /// delivery queue at all — the row said "Sent" and no parent heard anything.
        /// </para>
        /// <para>
        /// Throws <see cref="Common.Exceptions.AnnouncementNotApprovedException"/> for
        /// grade/stage/school-wide scopes that have not been approved (BR-MSG-001), and
        /// <see cref="Common.Exceptions.EmptyAudienceException"/> when the audience resolves
        /// to nobody (doc/Modules/32 §9's "audience must resolve &gt; 0").
        /// </para>
        /// </summary>
        Task<int> SendAnnouncementAsync(int announcementId, CancellationToken cancellationToken = default);

        // ------------------------------------------------------------------ the compose screen's reads

        /// <summary>The targets a given scope can be pointed at, each with its current guardian count — the audience builder's picker.</summary>
        Task<IReadOnlyList<AudienceOption>> ListAudienceTargetsAsync(AudienceScope scope, CancellationToken cancellationToken = default);

        /// <summary>What sending to this scope/target on these channels would cost and reach, before it is sent.</summary>
        Task<AudiencePreview> PreviewAudienceAsync(
            AudienceScope scope, int? audienceTargetId, int channelMask, CancellationToken cancellationToken = default);

        /// <summary>Announcements newest first, with their audience named and their queued deliveries counted.</summary>
        Task<IReadOnlyList<AnnouncementSummary>> ListAnnouncementsAsync(CancellationToken cancellationToken = default);

        Task<Announcement?> GetAnnouncementAsync(int announcementId, CancellationToken cancellationToken = default);

        Task<CommunicationMatrix> DefineCommunicationMatrixEntryAsync(string topicCode, int routedToRoleId, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.UnroutableTopicException"/> when no matrix entry exists for the topic (BR-MSG-002).</summary>
        Task<MessageThread> StartThreadAsync(string topicCode, int initiatedByUserId, string firstMessageBody, CancellationToken cancellationToken = default);

        Task<ThreadMessage> ReplyToThreadAsync(int threadId, int senderUserId, string body, CancellationToken cancellationToken = default);

        Task CloseThreadAsync(int threadId, CancellationToken cancellationToken = default);

        /// <summary>BR-MSG-004: issues via doc 08's strict "MSG" series.</summary>
        Task<OfficialLetter> IssueOfficialLetterAsync(
            string templateCode, int recipientUserId, string bodySnapshot, bool requiresAcknowledgment, CancellationToken cancellationToken = default);

        Task AcknowledgeLetterAsync(int officialLetterId, CancellationToken cancellationToken = default);
    }
}
