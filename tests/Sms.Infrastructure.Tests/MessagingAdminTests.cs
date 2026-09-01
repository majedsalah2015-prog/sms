using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Messaging;
using Sms.Domain.Messaging;
using Sms.Domain.Notifications;
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

            /// <summary>Set from the seeded year rather than fixed: sections and enrolments carry a real foreign key to it.</summary>
            public int AcademicYearId { get; set; } = 1;
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

            var year = new Sms.Domain.Schools.AcademicYear
            {
                LabelAr = "٢٠٢٦-٢٠٢٧", LabelEn = "2026-2027", HijriLabel = "١٤٤٨هـ",
                StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 6, 30),
                Status = Sms.Domain.Schools.AcademicYearStatus.Active,
            };
            db.AcademicYears.Add(year);
            db.SaveChanges();

            _tenant.AcademicYearId = year.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private MessagingAdmin CreateAdmin(AppDbContext db, TestAddressBook? addresses = null)
            => new MessagingAdmin(db, _clock, new NumberIssuer(db, _tenant, _tenant, _clock), _tenant, addresses ?? new TestAddressBook());

        /// <summary>
        /// A portal login for the guardian. Deliveries carry a real foreign key to
        /// <c>sec.UserAccount</c>, so a fixture that invents a recipient id gets a foreign-key
        /// violation rather than a delivery — which is the schema doing its job.
        /// </summary>
        private int SeedAccount(AppDbContext db, string userName)
        {
            var account = new Sms.Domain.Security.UserAccount
            {
                UserName = userName, AccountType = Sms.Domain.Security.AccountType.Parent,
            };
            db.UserAccounts.Add(account);
            db.SaveChanges();
            return account.Id;
        }

        /// <summary>
        /// The minimum shape an announcement can actually be sent to: an account, a stage, a
        /// grade under it, that grade's profile for the working year, a section, an enrolled
        /// student in it, and a guardian linked to that student. Returns the section's id.
        /// <para>
        /// Written out rather than faked because the send walks every one of those joins — a
        /// test that stubbed the middle of it would prove the assertion and not the walk.
        /// </para>
        /// </summary>
        private int SeedOneStudentWithAGuardian(AppDbContext db, string userName = "guardian", string language = "ar")
        {
            var userAccountId = SeedAccount(db, userName);
            var stage = new Sms.Domain.Grades.Stage { Name = new Sms.Domain.Common.LocalizedName("مرحلة", "Stage"), SequenceOrder = 1 };
            db.Stages.Add(stage);
            db.SaveChanges();

            var grade = new Sms.Domain.Grades.GradeLevel
            {
                StageId = stage.Id, Code = "G1", Name = new Sms.Domain.Common.LocalizedName("الأول", "Grade 1"), SequenceOrder = 1,
            };
            db.GradeLevels.Add(grade);
            db.SaveChanges();

            var profile = new Sms.Domain.Grades.GradeYearProfile
            {
                AcademicYearId = _tenant.AcademicYearId, GradeLevelId = grade.Id, TargetSections = 1, TargetSectionSize = 25,
            };
            db.GradeYearProfiles.Add(profile);
            db.SaveChanges();

            var section = new Sms.Domain.Sections.Section
            {
                AcademicYearId = _tenant.AcademicYearId, GradeYearProfileId = profile.Id,
                NameAr = "أ", NameEn = "A", Capacity = 25,
            };
            db.Sections.Add(section);

            var student = new Sms.Domain.Students.Student
            {
                StudentNo = "S-1", FirstNameAr = "طالب", FamilyNameAr = "تجربة", FirstNameEn = "Student", FamilyNameEn = "Test",
                DateOfBirth = new DateTime(2015, 1, 1), Gender = Sms.Domain.Common.Gender.Male,
            };
            db.Students.Add(student);

            var parent = new Sms.Domain.Parents.Parent
            {
                NameAr = "ولي", NameEn = "Guardian", PrimaryMobile = "0599123456",
                PreferredLanguage = language, UserAccountId = userAccountId,
            };
            db.Parents.Add(parent);
            db.SaveChanges();

            var enrollment = new Sms.Domain.Students.Enrollment
            {
                AcademicYearId = _tenant.AcademicYearId, StudentId = student.Id, GradeYearProfileId = profile.Id,
                EnrollmentDate = _clock.UtcNow, Status = Sms.Domain.Students.EnrollmentStatus.Active,
            };
            db.Enrollments.Add(enrollment);
            db.SaveChanges();

            db.SectionMemberships.Add(new Sms.Domain.Sections.SectionMembership
            {
                AcademicYearId = _tenant.AcademicYearId, SectionId = section.Id, EnrollmentId = enrollment.Id,
                EffectiveFromUtc = _clock.UtcNow,
            });
            db.StudentGuardianLinks.Add(new Sms.Domain.Students.StudentGuardianLink
            {
                StudentId = student.Id, ParentId = parent.Id, EffectiveFromUtc = _clock.UtcNow,
            });
            db.SaveChanges();

            return section.Id;
        }

        private int GuardianAccountId(AppDbContext db) => db.Parents.Single().UserAccountId!.Value;

        // --- BR-MSG-001 approval gate --------------------------------------------------

        [Fact]
        [BusinessRule("BR-MSG-001")]
        public async Task A_section_announcement_needs_no_approval()
        {
            using var db = CreateContext();
            var sectionId = SeedOneStudentWithAGuardian(db);
            var admin = CreateAdmin(db);

            var announcement = await admin.DefineAnnouncementAsync("عنوان", "Title", "نص", "Body", AudienceScope.Section, sectionId);

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

            await Assert.ThrowsAsync<AnnouncementNotApprovedException>(() => admin.SendAnnouncementAsync(announcement.Id));
        }

        [Fact]
        [BusinessRule("BR-MSG-001")]
        public async Task Approving_then_sending_succeeds()
        {
            using var db = CreateContext();
            SeedOneStudentWithAGuardian(db);
            var admin = CreateAdmin(db);
            var announcement = await admin.DefineAnnouncementAsync("عنوان", "Title", "نص", "Body", AudienceScope.SchoolWide);
            await admin.ApproveAnnouncementAsync(announcement.Id);

            await admin.SendAnnouncementAsync(announcement.Id);

            var updated = db.Announcements.Single(a => a.Id == announcement.Id);
            Assert.Equal(AnnouncementStatus.Sent, updated.Status);

            // The reach is counted from the resolved audience now, not handed in by the caller.
            Assert.Equal(1, updated.ReachCount);
        }

        // --- the send actually delivers (doc/Modules/32 §8.1) -----------------------------

        [Fact]
        [BusinessRule("BR-MSG-001")]
        public async Task Sending_queues_one_portal_delivery_per_guardian_carrying_the_announcement_text()
        {
            using var db = CreateContext();
            var sectionId = SeedOneStudentWithAGuardian(db, language: "ar");
            var admin = CreateAdmin(db);

            var announcement = await admin.DefineAnnouncementAsync(
                "عنوان", "Title", "نص الإعلان", "Announcement body", AudienceScope.Section, sectionId);

            var reach = await admin.SendAnnouncementAsync(announcement.Id);

            Assert.Equal(1, reach);
            var delivery = db.Deliveries.Single();
            Assert.Equal(NotificationChannel.InApp, delivery.Channel);
            Assert.Equal(GuardianAccountId(db), delivery.RecipientUserId);
            Assert.Equal(announcement.Id, delivery.AnnouncementId);

            // Human-composed: there is no template behind it, and its text is snapshotted here.
            Assert.Null(delivery.TemplateVersionId);
            Assert.Equal("نص الإعلان", delivery.RenderedBody);
        }

        [Fact]
        [BusinessRule("BR-MSG-001")]
        public async Task A_picked_channel_snapshots_the_address_the_message_went_to()
        {
            using var db = CreateContext();
            var sectionId = SeedOneStudentWithAGuardian(db);
            var addresses = new TestAddressBook().With(GuardianAccountId(db), NotificationChannel.WhatsApp, "+970599123456");
            var admin = CreateAdmin(db, addresses);

            var announcement = await admin.DefineAnnouncementAsync(
                "عنوان", "Title", "نص", "Body", AudienceScope.Section, sectionId,
                AnnouncementChannels.ToMask(new[] { NotificationChannel.WhatsApp }));

            await admin.SendAnnouncementAsync(announcement.Id);

            // The portal copy is always written; WhatsApp is in addition to it, never instead.
            var whatsApp = db.Deliveries.Single(d => d.Channel == NotificationChannel.WhatsApp);
            Assert.Equal("+970599123456", whatsApp.RecipientAddress);
            Assert.Contains(db.Deliveries.ToList(), d => d.Channel == NotificationChannel.InApp);
        }

        [Fact]
        [BusinessRule("BR-MSG-001")]
        public async Task A_guardian_with_two_children_in_the_audience_is_messaged_once()
        {
            using var db = CreateContext();
            var sectionId = SeedOneStudentWithAGuardian(db);

            // A second child of the same guardian, in the same section.
            var parentId = db.Parents.Single().Id;
            var profileId = db.GradeYearProfiles.Single().Id;
            var sibling = new Sms.Domain.Students.Student
            {
                StudentNo = "S-2", FirstNameAr = "أخ", FamilyNameAr = "تجربة", FirstNameEn = "Sibling", FamilyNameEn = "Test",
                DateOfBirth = new DateTime(2016, 1, 1), Gender = Sms.Domain.Common.Gender.Female,
            };
            db.Students.Add(sibling);
            db.SaveChanges();

            var enrollment = new Sms.Domain.Students.Enrollment
            {
                AcademicYearId = _tenant.AcademicYearId, StudentId = sibling.Id, GradeYearProfileId = profileId,
                EnrollmentDate = _clock.UtcNow, Status = Sms.Domain.Students.EnrollmentStatus.Active,
            };
            db.Enrollments.Add(enrollment);
            db.SaveChanges();

            db.SectionMemberships.Add(new Sms.Domain.Sections.SectionMembership
            {
                AcademicYearId = _tenant.AcademicYearId, SectionId = sectionId, EnrollmentId = enrollment.Id,
                EffectiveFromUtc = _clock.UtcNow,
            });
            db.StudentGuardianLinks.Add(new Sms.Domain.Students.StudentGuardianLink
            {
                StudentId = sibling.Id, ParentId = parentId, EffectiveFromUtc = _clock.UtcNow,
            });
            db.SaveChanges();

            var admin = CreateAdmin(db);
            var announcement = await admin.DefineAnnouncementAsync(
                "عنوان", "Title", "نص", "Body", AudienceScope.Section, sectionId);

            // BR-MSG-007: one family, one message — not one per enrolment.
            Assert.Equal(1, await admin.SendAnnouncementAsync(announcement.Id));
            Assert.Single(db.Deliveries.ToList());
        }

        [Fact]
        public async Task Sending_to_an_audience_that_resolves_to_nobody_is_refused()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var announcement = await admin.DefineAnnouncementAsync("عنوان", "Title", "نص", "Body", AudienceScope.SchoolWide);
            await admin.ApproveAnnouncementAsync(announcement.Id);

            // Nothing is enrolled: a send would stamp it Sent with a reach of zero, which is
            // indistinguishable afterwards from one that failed (doc/Modules/32 §9).
            await Assert.ThrowsAsync<EmptyAudienceException>(() => admin.SendAnnouncementAsync(announcement.Id));
        }

        [Fact]
        public async Task A_scope_that_needs_a_target_is_refused_without_one()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            await Assert.ThrowsAsync<InvalidAudienceTargetException>(
                () => admin.DefineAnnouncementAsync("عنوان", "Title", "نص", "Body", AudienceScope.Grade));

            await Assert.ThrowsAsync<InvalidAudienceTargetException>(
                () => admin.DefineAnnouncementAsync("عنوان", "Title", "نص", "Body", AudienceScope.SchoolWide, audienceTargetId: 4));
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
