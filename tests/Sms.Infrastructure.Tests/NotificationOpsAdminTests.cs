using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Notifications;
using Sms.Domain.Notifications;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Setup;
using Sms.Infrastructure.Notifications;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>S7/E-703 (Notifications Administration, doc/Modules/33, BR-NTF-001/002/004) over a real Sqlite-backed AppDbContext.</summary>
    public sealed class NotificationOpsAdminTests : IDisposable
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

        public NotificationOpsAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        /// <summary>
        /// The ops admin with its collaborators. No channel sender is registered by default:
        /// these tests exercise the publish lifecycle and the budget, and a provider test in a
        /// deployment with no HTTP transport should report that rather than pass.
        /// </summary>
        private NotificationOpsAdmin CreateOps(
            AppDbContext db, TestAddressBook? addresses = null, params IChannelSender[] senders)
            => new(db, _clock, new PassthroughProtector(), CreateSetup(db), addresses ?? new TestAddressBook(), senders);

        private SystemSetupAdmin CreateSetup(AppDbContext db)
            => new(db, _tenant, _clock, _user, _audit, new NotificationPublisher(db, new TestAddressBook()));

        /// <summary>
        /// Stands in for data protection, which needs a host key ring these tests do not have.
        /// <para>
        /// Base64 is emphatically not encryption. It is used here because it has the two
        /// properties the tests need and the real protector also has: the stored value is not
        /// the value that was typed, and it round-trips. A passthrough would satisfy neither,
        /// and a test that asserts the token is not on the row would then be asserting nothing.
        /// </para>
        /// </summary>
        private sealed class PassthroughProtector : ISecretProtector
        {
            private const string Prefix = "sealed:";

            public string Protect(string plaintext)
                => Prefix + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext));

            public string? Unprotect(string? cipher)
                => cipher != null && cipher.StartsWith(Prefix, StringComparison.Ordinal)
                    ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cipher.Substring(Prefix.Length)))
                    : null;
        }

        // --- BR-NTF-001 template publish lifecycle ---------------------------------------

        [Fact]
        [BusinessRule("BR-NTF-001")]
        public async Task Publishing_before_test_send_is_rejected()
        {
            using var db = CreateContext();
            var configAdmin = new NotificationConfigAdmin(db);
            var opsAdmin = CreateOps(db);
            var version = await configAdmin.DefineTemplateAsync("Attendance.StudentAbsent", NotificationChannel.InApp, null, null, "غاب", "Absent");

            await Assert.ThrowsAsync<InvalidTemplatePublishTransitionException>(() => opsAdmin.PublishTemplateVersionAsync(version.Id));
        }

        [Fact]
        [BusinessRule("BR-NTF-001")]
        public async Task Test_sending_then_publishing_succeeds()
        {
            using var db = CreateContext();
            var configAdmin = new NotificationConfigAdmin(db);
            var opsAdmin = CreateOps(db);
            var version = await configAdmin.DefineTemplateAsync("Attendance.StudentAbsent", NotificationChannel.InApp, null, null, "غاب", "Absent");

            await opsAdmin.MarkTemplateVersionTestSentAsync(version.Id);
            await opsAdmin.PublishTemplateVersionAsync(version.Id);

            Assert.Equal(TemplatePublishStatus.Published, db.TemplateVersions.Single(v => v.Id == version.Id).PublishStatus);
        }

        // --- BR-NTF-002 statutory subscription floor -------------------------------------

        [Fact]
        [BusinessRule("BR-NTF-002")]
        public async Task Disabling_a_non_statutory_rule_succeeds_without_approval()
        {
            using var db = CreateContext();
            var configAdmin = new NotificationConfigAdmin(db);
            var opsAdmin = CreateOps(db);
            var rule = await configAdmin.DefineSubscriptionRuleAsync("Library.OverdueReminder", NotificationChannel.Email, NotificationTiming.Immediate, isEnabled: true);

            await opsAdmin.DisableSubscriptionAsync(rule.Id);

            Assert.False(db.SubscriptionRules.IgnoreQueryFilters().Single(r => r.Id == rule.Id).IsActive);
        }

        [Fact]
        [BusinessRule("BR-NTF-002")]
        public async Task Disabling_a_statutory_rule_without_approval_is_rejected()
        {
            using var db = CreateContext();
            var configAdmin = new NotificationConfigAdmin(db);
            var opsAdmin = CreateOps(db);
            var rule = await configAdmin.DefineSubscriptionRuleAsync("Attendance.StudentAbsent", NotificationChannel.Sms, NotificationTiming.Immediate, isEnabled: true);
            await opsAdmin.SetSubscriptionStatutoryAsync(rule.Id, isStatutory: true);

            await Assert.ThrowsAsync<StatutorySubscriptionChangeDeniedException>(() => opsAdmin.DisableSubscriptionAsync(rule.Id));
        }

        [Fact]
        [BusinessRule("BR-NTF-002")]
        public async Task Disabling_a_statutory_rule_with_approval_succeeds()
        {
            using var db = CreateContext();
            var configAdmin = new NotificationConfigAdmin(db);
            var opsAdmin = CreateOps(db);
            var rule = await configAdmin.DefineSubscriptionRuleAsync("Attendance.StudentAbsent", NotificationChannel.Sms, NotificationTiming.Immediate, isEnabled: true);
            await opsAdmin.SetSubscriptionStatutoryAsync(rule.Id, isStatutory: true);

            await opsAdmin.DisableSubscriptionAsync(rule.Id, principalApprovalGranted: true);

            Assert.False(db.SubscriptionRules.IgnoreQueryFilters().Single(r => r.Id == rule.Id).IsActive);
        }

        // --- BR-NTF-004 budget threshold --------------------------------------------------

        [Fact]
        [BusinessRule("BR-NTF-004")]
        public async Task Evaluating_budget_reads_the_existing_counter_and_flags_the_hard_stop()
        {
            using var db = CreateContext();
            db.BudgetCounters.Add(new BudgetCounter { Channel = NotificationChannel.Sms, PeriodKey = "2027-03", MessageCount = 100 });
            await db.SaveChangesAsync();
            var opsAdmin = CreateOps(db);

            var result = await opsAdmin.EvaluateBudgetAsync(NotificationChannel.Sms, "2027-03", budgetLimit: 100, isSafetyClass: false);

            Assert.Equal(100, result.CurrentCount);
            Assert.True(result.ShouldAlert);
            Assert.True(result.ShouldBlock);
        }

        [Fact]
        [BusinessRule("BR-NTF-004")]
        public async Task Safety_class_messages_are_exempt_from_the_hard_stop()
        {
            using var db = CreateContext();
            db.BudgetCounters.Add(new BudgetCounter { Channel = NotificationChannel.Sms, PeriodKey = "2027-03", MessageCount = 100 });
            await db.SaveChangesAsync();
            var opsAdmin = CreateOps(db);

            var result = await opsAdmin.EvaluateBudgetAsync(NotificationChannel.Sms, "2027-03", budgetLimit: 100, isSafetyClass: true);

            Assert.False(result.ShouldBlock);
        }

        // --- BR-NTF-003 the gateway registry and its credentials --------------------------

        [Fact]
        [BusinessRule("BR-NTF-003")]
        public async Task A_gateway_token_is_never_stored_as_it_was_typed()
        {
            using var db = CreateContext();
            var ops = CreateOps(db);

            var provider = await ops.SaveProviderAsync(
                null, NotificationChannel.WhatsApp, ProviderCatalog.Twilio, "Main line",
                "AC123", "the-auth-token", "+970599123456", null, 1);

            var stored = db.Providers.IgnoreQueryFilters().Single(p => p.Id == provider.Id);
            Assert.NotNull(stored.SecretCipher);
            Assert.DoesNotContain("the-auth-token", stored.SecretCipher!, StringComparison.Ordinal);
            Assert.True(stored.IsConfigured);
        }

        [Fact]
        [BusinessRule("BR-NTF-003")]
        public async Task Saving_without_a_secret_keeps_the_one_already_stored()
        {
            using var db = CreateContext();
            var ops = CreateOps(db);
            var provider = await ops.SaveProviderAsync(
                null, NotificationChannel.WhatsApp, ProviderCatalog.Twilio, "Main line",
                "AC123", "the-auth-token", "+970599123456", null, 1);
            var original = db.Providers.IgnoreQueryFilters().Single(p => p.Id == provider.Id).SecretCipher;

            // The console cannot show the token, so retyping it must not be the price of
            // changing the sender number.
            await ops.SaveProviderAsync(
                provider.Id, NotificationChannel.WhatsApp, ProviderCatalog.Twilio, "Main line",
                "AC123", null, "+970599999999", null, 1);

            var updated = db.Providers.IgnoreQueryFilters().Single(p => p.Id == provider.Id);
            Assert.Equal(original, updated.SecretCipher);
            Assert.Equal("+970599999999", updated.SenderId);
        }

        [Fact]
        [BusinessRule("BR-NTF-003")]
        public async Task Rotating_the_token_invalidates_the_last_test_result()
        {
            using var db = CreateContext();
            var ops = CreateOps(db);
            var provider = await ops.SaveProviderAsync(
                null, NotificationChannel.Sms, ProviderCatalog.Twilio, "SMS", "AC1", "first", "+970599123456", null, 1);

            db.Providers.IgnoreQueryFilters().Single(p => p.Id == provider.Id).LastTestOutcome = ProviderTestOutcome.Passed;
            await db.SaveChangesAsync();

            await ops.SaveProviderAsync(
                provider.Id, NotificationChannel.Sms, ProviderCatalog.Twilio, "SMS", "AC1", "second", "+970599123456", null, 1);

            // The credentials that passed are not the credentials on the row any more.
            Assert.Equal(
                ProviderTestOutcome.NeverTested,
                db.Providers.IgnoreQueryFilters().Single(p => p.Id == provider.Id).LastTestOutcome);
        }

        [Fact]
        [BusinessRule("BR-NTF-003")]
        public async Task A_gateway_that_does_not_serve_the_channel_is_refused()
        {
            using var db = CreateContext();
            var ops = CreateOps(db);

            // 360dialog is WhatsApp-only; registering it on SMS would be a row nothing answers to.
            await Assert.ThrowsAsync<ProviderChannelMismatchException>(() => ops.SaveProviderAsync(
                null, NotificationChannel.Sms, ProviderCatalog.Dialog360, "SMS", "ch1", "k", "+970599123456", null, 1));

            await Assert.ThrowsAsync<UnknownProviderCodeException>(() => ops.SaveProviderAsync(
                null, NotificationChannel.Sms, "NOT-A-GATEWAY", "SMS", "x", "k", "+970599123456", null, 1));
        }

        [Fact]
        [BusinessRule("BR-NTF-003")]
        public async Task The_last_gateway_on_a_subscribed_channel_cannot_be_switched_off()
        {
            using var db = CreateContext();
            var config = new NotificationConfigAdmin(db);
            var ops = CreateOps(db);

            await config.DefineSubscriptionRuleAsync(
                "AttendanceStudentAbsent", NotificationChannel.WhatsApp, NotificationTiming.Immediate, isEnabled: true);
            var provider = await ops.SaveProviderAsync(
                null, NotificationChannel.WhatsApp, ProviderCatalog.Twilio, "Only line", "AC1", "k", "+970599123456", null, 1);

            await Assert.ThrowsAsync<ProviderInUseException>(() => ops.DeactivateProviderAsync(provider.Id));

            // A second active gateway makes it a failover change rather than a blackout.
            await ops.SaveProviderAsync(
                null, NotificationChannel.WhatsApp, ProviderCatalog.Dialog360, "Spare", "ch1", "k", "+970599123457", null, 2);
            await ops.DeactivateProviderAsync(provider.Id);

            Assert.False(db.Providers.IgnoreQueryFilters().Single(p => p.Id == provider.Id).IsActive);
        }

        // --- BR-NTF-005 the failure queue -------------------------------------------------

        [Fact]
        [BusinessRule("BR-NTF-005")]
        public async Task Retrying_a_failed_delivery_requeues_it_with_its_attempts_reset()
        {
            using var db = CreateContext();
            var userId = SeedAccount(db, "recipient");
            var failed = new Delivery
            {
                EventCode = "AttendanceStudentAbsent", Channel = NotificationChannel.WhatsApp, RecipientUserId = userId,
                RenderedBody = "x", Status = DeliveryStatus.Failed, AttemptCount = 3, FailureReason = "HTTP 401",
            };
            var delivered = new Delivery
            {
                EventCode = "AttendanceStudentAbsent", Channel = NotificationChannel.InApp, RecipientUserId = userId,
                RenderedBody = "x", Status = DeliveryStatus.Delivered,
            };
            db.Deliveries.AddRange(failed, delivered);
            await db.SaveChangesAsync();

            var moved = await CreateOps(db).RetryDeliveriesAsync(new[] { failed.Id, delivered.Id });

            // Only the failed one moves; a delivered row is skipped rather than being an error.
            Assert.Equal(1, moved);
            var requeued = db.Deliveries.Single(d => d.Id == failed.Id);
            Assert.Equal(DeliveryStatus.Queued, requeued.Status);

            // Reset, not decremented: the dispatcher's three strikes are three per attempt to
            // deliver, and leaving it at 3 would have it fail on sight.
            Assert.Equal(0, requeued.AttemptCount);
            Assert.Null(requeued.FailureReason);
        }

        // --- doc/Modules/33 §8.6 the notification centre -----------------------------------

        /// <summary>A real account, because Delivery.RecipientUserId is a real foreign key to sec.UserAccount.</summary>
        private static int SeedAccount(AppDbContext db, string userName)
        {
            var account = new Sms.Domain.Security.UserAccount { UserName = userName, AccountType = Sms.Domain.Security.AccountType.Parent };
            db.UserAccounts.Add(account);
            db.SaveChanges();
            return account.Id;
        }

        [Fact]
        public async Task A_user_can_only_read_their_own_notifications()
        {
            using var db = CreateContext();
            var meId = SeedAccount(db, "me");
            var themId = SeedAccount(db, "them");

            var mine = new Delivery
            {
                EventCode = "AttendanceStudentAbsent", Channel = NotificationChannel.InApp, RecipientUserId = meId,
                RenderedBody = "mine", Status = DeliveryStatus.Delivered,
            };
            var theirs = new Delivery
            {
                EventCode = "AttendanceStudentAbsent", Channel = NotificationChannel.InApp, RecipientUserId = themId,
                RenderedBody = "theirs", Status = DeliveryStatus.Delivered,
            };
            db.Deliveries.AddRange(mine, theirs);
            await db.SaveChangesAsync();

            var ops = CreateOps(db);

            Assert.Equal("mine", Assert.Single(await ops.ListInboxAsync(meId, includeRead: false)).Body);

            // This is the one screen in the module with no permission behind it, so the port is
            // what has to refuse: marking somebody else's notification read must do nothing.
            await ops.MarkInAppReadAsync(theirs.Id, meId);
            Assert.False(db.Deliveries.Single(d => d.Id == theirs.Id).IsRead);

            await ops.MarkInAppReadAsync(mine.Id, meId);
            Assert.True(db.Deliveries.Single(d => d.Id == mine.Id).IsRead);
        }

        [Fact]
        public async Task A_queued_in_app_notification_is_not_in_the_inbox_yet()
        {
            using var db = CreateContext();
            var userId = SeedAccount(db, "recipient");
            db.Deliveries.Add(new Delivery
            {
                EventCode = "AttendanceStudentAbsent", Channel = NotificationChannel.InApp, RecipientUserId = userId,
                RenderedBody = "not yet", Status = DeliveryStatus.Queued,
            });
            await db.SaveChangesAsync();

            // Showing it before the dispatcher marks it delivered would let a reader see a
            // message the log still says was never delivered.
            Assert.Empty(await CreateOps(db).ListInboxAsync(userId, includeRead: true));
        }
    }
}
