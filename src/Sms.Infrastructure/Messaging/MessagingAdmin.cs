using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Messaging;
using Sms.Application.Notifications;
using Sms.Application.Numbering;
using Sms.Domain.Messaging;
using Sms.Domain.Notifications;
using Sms.Domain.Students;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Messaging
{
    /// <summary>Standalone admin operations — save themselves, no larger transaction to ride.</summary>
    public class MessagingAdmin : IMessagingAdmin
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly INumberIssuer _numberIssuer;
        private readonly IWorkingYearContext _year;
        private readonly IRecipientAddressBook _addresses;

        public MessagingAdmin(
            AppDbContext db, IClock clock, INumberIssuer numberIssuer, IWorkingYearContext year, IRecipientAddressBook addresses)
        {
            _db = db;
            _clock = clock;
            _numberIssuer = numberIssuer;
            _year = year;
            _addresses = addresses;
        }

        public async Task<Announcement> DefineAnnouncementAsync(
            string titleAr, string titleEn, string bodyAr, string bodyEn, AudienceScope audienceScope,
            int? audienceTargetId = null, int channelMask = 0, CancellationToken cancellationToken = default)
        {
            RequireCoherentTarget(audienceScope, audienceTargetId);

            var status = AnnouncementApprovalGate.RequiresApproval(audienceScope) ? AnnouncementStatus.PendingApproval : AnnouncementStatus.Draft;
            var announcement = new Announcement
            {
                TitleAr = titleAr, TitleEn = titleEn, BodyAr = bodyAr, BodyEn = bodyEn, AudienceScope = audienceScope,
                AudienceTargetId = audienceTargetId, ChannelMask = channelMask, Status = status,
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

        public async Task<int> SendAnnouncementAsync(int announcementId, CancellationToken cancellationToken = default)
        {
            var announcement = await _db.Announcements.SingleAsync(a => a.Id == announcementId, cancellationToken);
            if (AnnouncementApprovalGate.RequiresApproval(announcement.AudienceScope) && announcement.ApprovedAtUtc == null)
            {
                throw new AnnouncementNotApprovedException(announcementId);
            }

            var guardians = await GuardiansAsync(announcement.AudienceScope, announcement.AudienceTargetId, cancellationToken);
            if (guardians.Count == 0)
            {
                throw new EmptyAudienceException(announcementId);
            }

            // In-app always, whatever the picker said: the portal copy is the archived,
            // bilingual record BR-MSG-007 requires, and a school that ticks only WhatsApp must
            // still be able to show a parent what was sent. The picked channels are additions
            // to it, not alternatives.
            var channels = new List<NotificationChannel> { NotificationChannel.InApp };
            channels.AddRange(AnnouncementChannels.FromMask(announcement.ChannelMask)
                .Where(c => c != NotificationChannel.InApp));

            var userIds = guardians.Select(g => g.UserAccountId).ToList();
            foreach (var channel in channels)
            {
                var addresses = await _addresses.ResolveAsync(userIds, channel, cancellationToken);

                foreach (var guardian in guardians)
                {
                    var arabic = string.Equals(guardian.PreferredLanguage, "ar", StringComparison.OrdinalIgnoreCase);

                    _db.Deliveries.Add(new Delivery
                    {
                        EventCode = AnnouncementEventCode,
                        Channel = channel,
                        RecipientUserId = guardian.UserAccountId,

                        // No template version: an announcement is human-composed, and its text is
                        // already snapshotted onto this row (BR-NOT-008's purpose, reached by a
                        // different route).
                        TemplateVersionId = null,
                        AnnouncementId = announcement.Id,
                        RenderedSubject = arabic ? announcement.TitleAr : announcement.TitleEn,
                        RenderedBody = arabic ? announcement.BodyAr : announcement.BodyEn,
                        RecipientAddress = addresses.TryGetValue(guardian.UserAccountId, out var address) ? address : null,
                        Status = DeliveryStatus.Queued,
                    });
                }
            }

            announcement.Status = AnnouncementStatus.Sent;
            announcement.ReachCount = guardians.Count;
            announcement.SentAtUtc = _clock.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
            return guardians.Count;
        }

        // ------------------------------------------------------------------ the audience builder

        public async Task<IReadOnlyList<AudienceOption>> ListAudienceTargetsAsync(
            AudienceScope scope, CancellationToken cancellationToken = default)
        {
            switch (scope)
            {
                case AudienceScope.Section:
                {
                    var sections = await _db.Sections
                        .Where(s => s.AcademicYearId == _year.AcademicYearId)
                        .Select(s => new { s.Id, s.NameAr, s.NameEn })
                        .ToListAsync(cancellationToken);

                    return await CountedAsync(sections.Select(s => (s.Id, s.NameAr, s.NameEn)).ToList(),
                        AudienceScope.Section, cancellationToken);
                }

                case AudienceScope.Grade:
                {
                    var grades = await _db.GradeLevels
                        .Select(g => new { g.Id, g.Name.NameAr, g.Name.NameEn, g.SequenceOrder })
                        .OrderBy(g => g.SequenceOrder)
                        .ToListAsync(cancellationToken);

                    return await CountedAsync(grades.Select(g => (g.Id, g.NameAr, g.NameEn)).ToList(),
                        AudienceScope.Grade, cancellationToken);
                }

                case AudienceScope.Stage:
                {
                    var stages = await _db.Stages
                        .Select(s => new { s.Id, s.Name.NameAr, s.Name.NameEn, s.SequenceOrder })
                        .OrderBy(s => s.SequenceOrder)
                        .ToListAsync(cancellationToken);

                    return await CountedAsync(stages.Select(s => (s.Id, s.NameAr, s.NameEn)).ToList(),
                        AudienceScope.Stage, cancellationToken);
                }

                default:
                    // School-wide takes no target; the screen shows the count rather than a picker.
                    return new List<AudienceOption>();
            }
        }

        public async Task<AudiencePreview> PreviewAudienceAsync(
            AudienceScope scope, int? audienceTargetId, int channelMask, CancellationToken cancellationToken = default)
        {
            var count = (await GuardiansAsync(scope, audienceTargetId, cancellationToken)).Count;

            return new AudiencePreview(
                count,
                AnnouncementChannels.CostedMessageCount(channelMask, count),
                AnnouncementApprovalGate.RequiresApproval(scope));
        }

        public async Task<IReadOnlyList<AnnouncementSummary>> ListAnnouncementsAsync(CancellationToken cancellationToken = default)
        {
            var announcements = await _db.Announcements
                .OrderByDescending(a => a.Id)
                .Take(200)
                .ToListAsync(cancellationToken);

            if (announcements.Count == 0)
            {
                return new List<AnnouncementSummary>();
            }

            var ids = announcements.Select(a => a.Id).ToList();
            var deliveryCounts = (await _db.Deliveries
                    .Where(d => d.AnnouncementId != null && ids.Contains(d.AnnouncementId!.Value))
                    .GroupBy(d => d.AnnouncementId!.Value)
                    .Select(g => new { AnnouncementId = g.Key, Count = g.Count() })
                    .ToListAsync(cancellationToken))
                .ToDictionary(g => g.AnnouncementId, g => g.Count);

            var labels = await AudienceLabelsAsync(announcements, cancellationToken);

            return announcements.Select(a =>
            {
                var label = labels.TryGetValue((a.AudienceScope, a.AudienceTargetId), out var found)
                    ? found
                    : (Ar: string.Empty, En: string.Empty);

                return new AnnouncementSummary(
                    a, label.Ar, label.En, deliveryCounts.TryGetValue(a.Id, out var count) ? count : 0);
            }).ToList();
        }

        public async Task<Announcement?> GetAnnouncementAsync(int announcementId, CancellationToken cancellationToken = default)
            => await _db.Announcements.SingleOrDefaultAsync(a => a.Id == announcementId, cancellationToken);

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

        // ------------------------------------------------------------------ audience resolution
        //
        // An announcement is addressed to a slice of the school's structure and delivered to
        // the guardians of the students in it. The walk is the same one every module makes:
        //   scope -> student ids -> current guardian links -> parents with a portal account.
        // What differs per scope is only how the student ids are reached, so that is the one
        // thing StudentIdsAsync branches on.

        /// <summary>doc/Modules/32 §7's reserved code for a human-composed broadcast — not one of doc 09 §3's events, and deliberately outside NotificationEventCatalog: no school subscribes to or unsubscribes from an announcement.</summary>
        public const string AnnouncementEventCode = "MessagingAnnouncement";

        private async Task<IReadOnlyList<GuardianRecipient>> GuardiansAsync(
            AudienceScope scope, int? audienceTargetId, CancellationToken cancellationToken)
        {
            var studentIds = await StudentIdsAsync(scope, audienceTargetId, cancellationToken);
            if (studentIds.Count == 0)
            {
                return new List<GuardianRecipient>();
            }

            var parentIds = await _db.StudentGuardianLinks
                .Where(l => studentIds.Contains(l.StudentId) && l.EffectiveToUtc == null)
                .Select(l => l.ParentId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (parentIds.Count == 0)
            {
                return new List<GuardianRecipient>();
            }

            // One row per parent, not per child: a family with three children in the grade gets
            // one message, which is what BR-MSG-007's "no message floods" is asking for and what
            // makes the reach count a count of people rather than of enrolments.
            return (await _db.Parents
                    .Where(p => parentIds.Contains(p.Id) && p.UserAccountId != null)
                    .Select(p => new { UserAccountId = p.UserAccountId!.Value, p.PreferredLanguage })
                    .ToListAsync(cancellationToken))
                .GroupBy(p => p.UserAccountId)
                .Select(g => new GuardianRecipient(g.Key, g.First().PreferredLanguage))
                .ToList();
        }

        private async Task<IReadOnlyList<int>> StudentIdsAsync(
            AudienceScope scope, int? audienceTargetId, CancellationToken cancellationToken)
        {
            RequireCoherentTarget(scope, audienceTargetId);
            var year = _year.AcademicYearId;

            // Enrolments, not students: a student who withdrew in October is on the register and
            // not in the class, and a broadcast to "grade 4's parents" does not mean them.
            var enrolments = _db.Enrollments.Where(e => e.AcademicYearId == year && e.Status == EnrollmentStatus.Active);

            switch (scope)
            {
                case AudienceScope.SchoolWide:
                    return await enrolments.Select(e => e.StudentId).Distinct().ToListAsync(cancellationToken);

                case AudienceScope.Section:
                {
                    var enrollmentIds = await _db.SectionMemberships
                        .Where(m => m.SectionId == audienceTargetId!.Value
                                    && m.AcademicYearId == year
                                    && m.EffectiveToUtc == null)
                        .Select(m => m.EnrollmentId)
                        .ToListAsync(cancellationToken);

                    return await enrolments
                        .Where(e => enrollmentIds.Contains(e.Id))
                        .Select(e => e.StudentId)
                        .Distinct()
                        .ToListAsync(cancellationToken);
                }

                case AudienceScope.Grade:
                {
                    var profileIds = await ProfileIdsForGradesAsync(new[] { audienceTargetId!.Value }, year, cancellationToken);
                    return await enrolments
                        .Where(e => profileIds.Contains(e.GradeYearProfileId))
                        .Select(e => e.StudentId)
                        .Distinct()
                        .ToListAsync(cancellationToken);
                }

                case AudienceScope.Stage:
                {
                    // IgnoreQueryFilters on the lookup, not on the picker: a stage retired in
                    // March still has students enrolled under it until the year rolls, and reading
                    // it through the soft-active filter is the "sequence contains no matching
                    // element" this codebase has paid for before.
                    var gradeIds = await _db.GradeLevels
                        .IgnoreQueryFilters()
                        .Where(g => g.SchoolId == _db.CurrentSchoolId && g.StageId == audienceTargetId!.Value)
                        .Select(g => g.Id)
                        .ToListAsync(cancellationToken);

                    var profileIds = await ProfileIdsForGradesAsync(gradeIds, year, cancellationToken);
                    return await enrolments
                        .Where(e => profileIds.Contains(e.GradeYearProfileId))
                        .Select(e => e.StudentId)
                        .Distinct()
                        .ToListAsync(cancellationToken);
                }

                default:
                    return new List<int>();
            }
        }

        private async Task<List<int>> ProfileIdsForGradesAsync(
            IReadOnlyCollection<int> gradeLevelIds, int year, CancellationToken cancellationToken)
            => gradeLevelIds.Count == 0
                ? new List<int>()
                : await _db.GradeYearProfiles
                    .IgnoreQueryFilters()
                    .Where(p => p.SchoolId == _db.CurrentSchoolId
                                && p.AcademicYearId == year
                                && gradeLevelIds.Contains(p.GradeLevelId))
                    .Select(p => p.Id)
                    .ToListAsync(cancellationToken);

        private async Task<IReadOnlyList<AudienceOption>> CountedAsync(
            IReadOnlyList<(int Id, string NameAr, string NameEn)> targets, AudienceScope scope, CancellationToken cancellationToken)
        {
            var options = new List<AudienceOption>(targets.Count);
            foreach (var target in targets)
            {
                var count = (await GuardiansAsync(scope, target.Id, cancellationToken)).Count;
                options.Add(new AudienceOption(target.Id, target.NameAr, target.NameEn, count));
            }

            return options;
        }

        /// <summary>
        /// Names each announcement's audience for the list. Read past the soft-active filter:
        /// an announcement sent to a section that has since been retired must still say which
        /// one, or the register of what was sent stops being a register.
        /// </summary>
        private async Task<Dictionary<(AudienceScope, int?), (string Ar, string En)>> AudienceLabelsAsync(
            IReadOnlyList<Announcement> announcements, CancellationToken cancellationToken)
        {
            var labels = new Dictionary<(AudienceScope, int?), (string Ar, string En)>();

            async Task AddAsync(AudienceScope scope, Func<IReadOnlyCollection<int>, Task<List<(int Id, string Ar, string En)>>> load)
            {
                var ids = announcements
                    .Where(a => a.AudienceScope == scope && a.AudienceTargetId != null)
                    .Select(a => a.AudienceTargetId!.Value)
                    .Distinct()
                    .ToList();

                if (ids.Count == 0)
                {
                    return;
                }

                foreach (var row in await load(ids))
                {
                    labels[(scope, row.Id)] = (row.Ar, row.En);
                }
            }

            await AddAsync(AudienceScope.Section, async ids => (await _db.Sections
                    .IgnoreQueryFilters()
                    .Where(s => s.SchoolId == _db.CurrentSchoolId && ids.Contains(s.Id))
                    .Select(s => new { s.Id, s.NameAr, s.NameEn })
                    .ToListAsync(cancellationToken))
                .Select(s => (s.Id, s.NameAr, s.NameEn)).ToList());

            await AddAsync(AudienceScope.Grade, async ids => (await _db.GradeLevels
                    .IgnoreQueryFilters()
                    .Where(g => g.SchoolId == _db.CurrentSchoolId && ids.Contains(g.Id))
                    .Select(g => new { g.Id, g.Name.NameAr, g.Name.NameEn })
                    .ToListAsync(cancellationToken))
                .Select(g => (g.Id, g.NameAr, g.NameEn)).ToList());

            await AddAsync(AudienceScope.Stage, async ids => (await _db.Stages
                    .IgnoreQueryFilters()
                    .Where(s => s.SchoolId == _db.CurrentSchoolId && ids.Contains(s.Id))
                    .Select(s => new { s.Id, s.Name.NameAr, s.Name.NameEn })
                    .ToListAsync(cancellationToken))
                .Select(s => (s.Id, s.NameAr, s.NameEn)).ToList());

            return labels;
        }

        /// <summary>School-wide takes no target and the other three require one — a mismatch is a mis-built form, not a send to everybody.</summary>
        private static void RequireCoherentTarget(AudienceScope scope, int? audienceTargetId)
        {
            var wantsTarget = scope != AudienceScope.SchoolWide;
            if (wantsTarget != audienceTargetId.HasValue)
            {
                throw new InvalidAudienceTargetException(scope);
            }
        }

        private sealed record GuardianRecipient(int UserAccountId, string PreferredLanguage);
    }
}
