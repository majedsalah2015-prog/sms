using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Messaging;
using Sms.Application.Numbering;
using Sms.Domain.Messaging;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Messaging
{
    /// <summary>Standalone admin operations — save themselves, no larger transaction to ride.</summary>
    public class MessagingAdmin : IMessagingAdmin
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly INumberIssuer _numberIssuer;

        public MessagingAdmin(AppDbContext db, IClock clock, INumberIssuer numberIssuer)
        {
            _db = db;
            _clock = clock;
            _numberIssuer = numberIssuer;
        }

        public async Task<Announcement> DefineAnnouncementAsync(
            string titleAr, string titleEn, string bodyAr, string bodyEn, AudienceScope audienceScope, CancellationToken cancellationToken = default)
        {
            var status = AnnouncementApprovalGate.RequiresApproval(audienceScope) ? AnnouncementStatus.PendingApproval : AnnouncementStatus.Draft;
            var announcement = new Announcement
            {
                TitleAr = titleAr, TitleEn = titleEn, BodyAr = bodyAr, BodyEn = bodyEn, AudienceScope = audienceScope, Status = status,
            };
            _db.Announcements.Add(announcement);
            await _db.SaveChangesAsync(cancellationToken);
            return announcement;
        }

        public async Task ApproveAnnouncementAsync(int announcementId, CancellationToken cancellationToken = default)
        {
            var announcement = await _db.Announcements.SingleAsync(a => a.Id == announcementId, cancellationToken);
            announcement.ApprovedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task SendAnnouncementAsync(int announcementId, int reachCount, CancellationToken cancellationToken = default)
        {
            var announcement = await _db.Announcements.SingleAsync(a => a.Id == announcementId, cancellationToken);
            if (AnnouncementApprovalGate.RequiresApproval(announcement.AudienceScope) && announcement.ApprovedAtUtc == null)
            {
                throw new AnnouncementNotApprovedException(announcementId);
            }

            announcement.Status = AnnouncementStatus.Sent;
            announcement.ReachCount = reachCount;
            announcement.SentAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<CommunicationMatrix> DefineCommunicationMatrixEntryAsync(string topicCode, int routedToRoleId, CancellationToken cancellationToken = default)
        {
            var entry = await _db.CommunicationMatrixEntries.SingleOrDefaultAsync(m => m.TopicCode == topicCode, cancellationToken);
            if (entry == null)
            {
                entry = new CommunicationMatrix { TopicCode = topicCode };
                _db.CommunicationMatrixEntries.Add(entry);
            }

            entry.RoutedToRoleId = routedToRoleId;
            await _db.SaveChangesAsync(cancellationToken);
            return entry;
        }

        public async Task<MessageThread> StartThreadAsync(string topicCode, int initiatedByUserId, string firstMessageBody, CancellationToken cancellationToken = default)
        {
            var matrixEntry = await _db.CommunicationMatrixEntries.SingleOrDefaultAsync(m => m.TopicCode == topicCode, cancellationToken);
            if (matrixEntry == null)
            {
                throw new UnroutableTopicException(topicCode);
            }

            var thread = new MessageThread
            {
                TopicCode = topicCode, InitiatedByUserId = initiatedByUserId, RoutedToRoleId = matrixEntry.RoutedToRoleId,
            };
            _db.MessageThreads.Add(thread);
            await _db.SaveChangesAsync(cancellationToken);

            _db.ThreadMessages.Add(new ThreadMessage
            {
                ThreadId = thread.Id, SenderUserId = initiatedByUserId, Body = firstMessageBody, SentAtUtc = _clock.UtcNow,
            });
            await _db.SaveChangesAsync(cancellationToken);

            return thread;
        }

        public async Task<ThreadMessage> ReplyToThreadAsync(int threadId, int senderUserId, string body, CancellationToken cancellationToken = default)
        {
            var message = new ThreadMessage { ThreadId = threadId, SenderUserId = senderUserId, Body = body, SentAtUtc = _clock.UtcNow };
            _db.ThreadMessages.Add(message);
            await _db.SaveChangesAsync(cancellationToken);
            return message;
        }

        public async Task CloseThreadAsync(int threadId, CancellationToken cancellationToken = default)
        {
            var thread = await _db.MessageThreads.SingleAsync(t => t.Id == threadId, cancellationToken);
            thread.Status = ThreadStatus.Closed;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<OfficialLetter> IssueOfficialLetterAsync(
            string templateCode, int recipientUserId, string bodySnapshot, bool requiresAcknowledgment, CancellationToken cancellationToken = default)
        {
            var letterNo = await _numberIssuer.IssueAsync("MSG", cancellationToken);
            var letter = new OfficialLetter
            {
                LetterNo = letterNo, TemplateCode = templateCode, RecipientUserId = recipientUserId, BodySnapshot = bodySnapshot,
                RequiresAcknowledgment = requiresAcknowledgment, IssuedAtUtc = _clock.UtcNow,
            };
            _db.OfficialLetters.Add(letter);
            await _db.SaveChangesAsync(cancellationToken);
            return letter;
        }

        public async Task AcknowledgeLetterAsync(int officialLetterId, CancellationToken cancellationToken = default)
        {
            var letter = await _db.OfficialLetters.SingleAsync(l => l.Id == officialLetterId, cancellationToken);
            letter.AcknowledgedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
