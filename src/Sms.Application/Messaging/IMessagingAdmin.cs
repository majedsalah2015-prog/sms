using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Messaging;

namespace Sms.Application.Messaging
{
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
            CancellationToken cancellationToken = default);

        Task ApproveAnnouncementAsync(int announcementId, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.AnnouncementNotApprovedException"/> for grade/stage/school-wide scopes that haven't been approved (BR-MSG-001).</summary>
        Task SendAnnouncementAsync(int announcementId, int reachCount, CancellationToken cancellationToken = default);

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
