using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Messaging;
using Sms.Domain.Numbering;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Messaging;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>S7/E-703 (Messaging, doc/Modules/32, BR-MSG-001/002/004) over a real Sqlite-backed AppDbContext.</summary>
    public sealed class MessagingAdminTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2027, 3, 1, 8, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; } = 1;
        }

        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;

            public int AcademicYearId => 2027;
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();

        public MessagingAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            db.NumberingSeries.Add(new NumberingSeries
            {
                Code = "MSG", EntityName = "OfficialMessage", FormatTemplate = "MSG-{SEQ:6}",
                ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Normal, EffectiveFromUtc = _clock.UtcNow, IsActive = true,
            });
            db.SaveChanges();
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private MessagingAdmin CreateAdmin(AppDbContext db) => new MessagingAdmin(db, _clock, new NumberIssuer(db, _tenant, _tenant, _clock));

        // --- BR-MSG-001 approval gate --------------------------------------------------

        [Fact]
        [BusinessRule("BR-MSG-001")]
        public async Task A_section_announcement_needs_no_approval()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            var announcement = await admin.DefineAnnouncementAsync("عنوان", "Title", "نص", "Body", AudienceScope.Section);

            Assert.Equal(AnnouncementStatus.Draft, announcement.Status);
        }

        [Fact]
        [BusinessRule("BR-MSG-001")]
        public async Task A_school_wide_announcement_requires_approval()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            var announcement = await admin.DefineAnnouncementAsync("عنوان", "Title", "نص", "Body", AudienceScope.SchoolWide);

            Assert.Equal(AnnouncementStatus.PendingApproval, announcement.Status);
        }

        [Fact]
        [BusinessRule("BR-MSG-001")]
        public async Task Sending_an_unapproved_school_wide_announcement_is_rejected()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var announcement = await admin.DefineAnnouncementAsync("عنوان", "Title", "نص", "Body", AudienceScope.SchoolWide);

            await Assert.ThrowsAsync<AnnouncementNotApprovedException>(() => admin.SendAnnouncementAsync(announcement.Id, reachCount: 500));
        }

        [Fact]
        [BusinessRule("BR-MSG-001")]
        public async Task Approving_then_sending_succeeds()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var announcement = await admin.DefineAnnouncementAsync("عنوان", "Title", "نص", "Body", AudienceScope.SchoolWide);
            await admin.ApproveAnnouncementAsync(announcement.Id);

            await admin.SendAnnouncementAsync(announcement.Id, reachCount: 500);

            var updated = db.Announcements.Single(a => a.Id == announcement.Id);
            Assert.Equal(AnnouncementStatus.Sent, updated.Status);
            Assert.Equal(500, updated.ReachCount);
        }

        // --- BR-MSG-002 communication matrix routing -------------------------------------

        [Fact]
        [BusinessRule("BR-MSG-002")]
        public async Task Starting_a_thread_for_an_unrouted_topic_is_rejected()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            await Assert.ThrowsAsync<UnroutableTopicException>(() => admin.StartThreadAsync("Absence", initiatedByUserId: 1, "My child is sick"));
        }

        [Fact]
        [BusinessRule("BR-MSG-002")]
        public async Task Starting_a_thread_routes_via_the_matrix_and_seeds_the_first_message()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await admin.DefineCommunicationMatrixEntryAsync("Absence", routedToRoleId: 5);

            var thread = await admin.StartThreadAsync("Absence", initiatedByUserId: 1, "My child is sick");

            Assert.Equal(5, thread.RoutedToRoleId);
            Assert.Single(db.ThreadMessages.Where(m => m.ThreadId == thread.Id));
        }

        [Fact]
        [BusinessRule("BR-MSG-002")]
        public async Task Replying_adds_a_second_message()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await admin.DefineCommunicationMatrixEntryAsync("Absence", routedToRoleId: 5);
            var thread = await admin.StartThreadAsync("Absence", initiatedByUserId: 1, "My child is sick");

            await admin.ReplyToThreadAsync(thread.Id, senderUserId: 5, "Noted, thank you.");

            Assert.Equal(2, db.ThreadMessages.Count(m => m.ThreadId == thread.Id));
        }

        [Fact]
        [BusinessRule("BR-MSG-002")]
        public async Task Closing_a_thread_updates_its_status()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await admin.DefineCommunicationMatrixEntryAsync("Absence", routedToRoleId: 5);
            var thread = await admin.StartThreadAsync("Absence", initiatedByUserId: 1, "My child is sick");

            await admin.CloseThreadAsync(thread.Id);

            Assert.Equal(ThreadStatus.Closed, db.MessageThreads.Single(t => t.Id == thread.Id).Status);
        }

        // --- BR-MSG-004 official letters --------------------------------------------------

        [Fact]
        [BusinessRule("BR-MSG-004")]
        public async Task Issuing_a_letter_uses_the_real_msg_series()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            var letter = await admin.IssueOfficialLetterAsync("Summons", recipientUserId: 1, "You are summoned to a meeting.", requiresAcknowledgment: true);

            Assert.Equal("MSG-000001", letter.LetterNo);
        }

        [Fact]
        [BusinessRule("BR-MSG-004")]
        public async Task Acknowledging_a_letter_stamps_the_timestamp()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var letter = await admin.IssueOfficialLetterAsync("Summons", recipientUserId: 1, "You are summoned to a meeting.", requiresAcknowledgment: true);

            await admin.AcknowledgeLetterAsync(letter.Id);

            Assert.NotNull(db.OfficialLetters.Single(l => l.Id == letter.Id).AcknowledgedAtUtc);
        }
    }
}
