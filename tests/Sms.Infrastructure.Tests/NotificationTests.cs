using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Notifications;
using Sms.Domain.Notifications;
using Sms.Domain.Security;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Notifications;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// E-007 notifications core over a real Sqlite-backed AppDbContext: the
    /// full doc 09 §2 pipeline — subscription config → publish (queues,
    /// never saves) → dispatch (drains, sends, retries, saves).
    /// </summary>
    public sealed class NotificationTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 8, 15, 8, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; }
        }

        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;

            public int AcademicYearId => 2027;
        }

        private sealed class FailingChannelSender : IChannelSender
        {
            public NotificationChannel Channel => NotificationChannel.Sms;

            public Task<ChannelSendOutcome> SendAsync(Delivery delivery, System.Threading.CancellationToken cancellationToken = default)
                => Task.FromResult(ChannelSendOutcome.Failure("gateway unreachable"));
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();
        private int _parentUserId;

        public NotificationTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            using var db = CreateContext();
            db.Database.EnsureCreated();

            var parent = new UserAccount { UserName = "parent1", AccountType = AccountType.Parent };
            db.UserAccounts.Add(parent);
            db.SaveChanges();
            _parentUserId = parent.Id;
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private static NotificationConfigAdmin Admin(AppDbContext db) => new(db);

        private static NotificationPublisher Publisher(AppDbContext db) => new(db);

        private NotificationDispatcher Dispatcher(AppDbContext db, params IChannelSender[] senders)
            => new(db, _clock, senders.Length > 0 ? senders : new IChannelSender[] { new InAppChannelSender() });

        // --- config: template versioning + subscription rules (BR-NOT-003/008) ---

        [Fact]
        [BusinessRule("BR-NOT-008")]
        public async Task Redefining_a_template_creates_a_new_version_and_never_touches_the_old_one()
        {
            using var db = CreateContext();
            var admin = Admin(db);
            var v1 = await admin.DefineTemplateAsync(
                "Attendance.StudentAbsent", NotificationChannel.InApp, null, null, "غاب {studentName}", "{studentName} was absent");

            var v2 = await admin.DefineTemplateAsync(
                "Attendance.StudentAbsent", NotificationChannel.InApp, null, null, "غاب {studentName} بتاريخ {date}", "{studentName} was absent on {date}");

            Assert.NotEqual(v1.Id, v2.Id);
            Assert.Equal(1, v1.VersionNumber);
            Assert.Equal(2, v2.VersionNumber);
            // v1's content is untouched — still readable exactly as it was sent.
            Assert.Equal("{studentName} was absent", db.TemplateVersions.Single(v => v.Id == v1.Id).BodyEn);
        }

        [Fact]
        [BusinessRule("BR-NOT-003")]
        public async Task A_disabled_subscription_rule_stops_new_publishes_from_queuing_anything()
        {
            using var db = CreateContext();
            await Admin(db).DefineTemplateAsync("Attendance.StudentAbsent", NotificationChannel.InApp, null, null, "غاب", "absent");
            await Admin(db).DefineSubscriptionRuleAsync("Attendance.StudentAbsent", NotificationChannel.InApp, NotificationTiming.Immediate, isEnabled: false);

            await Publisher(db).PublishAsync(
                "Attendance.StudentAbsent",
                new[] { new NotificationRecipient(_parentUserId, "en") },
                new Dictionary<string, string>());
            await db.SaveChangesAsync();

            Assert.Empty(db.Deliveries);
        }

        // --- publish: queues without saving, renders per language (BR-NOT-001/002) ---

        [Fact]
        [BusinessRule("BR-NOT-001")]
        public async Task Publish_renders_the_recipients_preferred_language_and_never_saves_itself()
        {
            using var db = CreateContext();
            await Admin(db).DefineTemplateAsync(
                "Attendance.StudentAbsent", NotificationChannel.InApp, null, null, "غاب {studentName} بتاريخ {date}", "{studentName} was absent on {date}");
            await Admin(db).DefineSubscriptionRuleAsync("Attendance.StudentAbsent", NotificationChannel.InApp, NotificationTiming.Immediate, isEnabled: true);

            var payload = new Dictionary<string, string> { ["studentName"] = "Layla", ["date"] = "2026-08-15" };
            await Publisher(db).PublishAsync(
                "Attendance.StudentAbsent", new[] { new NotificationRecipient(_parentUserId, "ar") }, payload);

            // Queued in the change tracker but not yet committed — proves PublishAsync
            // never calls SaveChanges itself (it rides the caller's transaction).
            var pending = Assert.Single(db.ChangeTracker.Entries<Delivery>());
            Assert.Equal(EntityState.Added, pending.State);

            await db.SaveChangesAsync();

            var delivery = db.Deliveries.Single();
            Assert.Equal("غاب Layla بتاريخ 2026-08-15", delivery.RenderedBody);
            Assert.Equal(DeliveryStatus.Queued, delivery.Status);
        }

        [Fact]
        [BusinessRule("BR-NOT-003")]
        public async Task Publish_skips_a_configured_channel_that_has_no_template_content_yet()
        {
            using var db = CreateContext();
            // Rule enabled for Email, but nobody has written the Email template.
            await Admin(db).DefineSubscriptionRuleAsync("Fees.InvoicePosted", NotificationChannel.Email, NotificationTiming.Immediate, isEnabled: true);

            await Publisher(db).PublishAsync(
                "Fees.InvoicePosted", new[] { new NotificationRecipient(_parentUserId, "en") }, new Dictionary<string, string>());
            await db.SaveChangesAsync();

            Assert.Empty(db.Deliveries);
        }

        // --- dispatch: sends, retries, budget counting (BR-NOT-006) ---

        [Fact]
        [BusinessRule("BR-NOT-006")]
        public async Task Dispatch_marks_in_app_deliveries_delivered_immediately()
        {
            using var db = CreateContext();
            await SeedQueuedDelivery(db, NotificationChannel.InApp);

            var processed = await Dispatcher(db, new InAppChannelSender()).DispatchQueuedAsync();

            Assert.Equal(1, processed);
            var delivery = db.Deliveries.Single();
            Assert.Equal(DeliveryStatus.Delivered, delivery.Status);
            Assert.Equal(1, delivery.AttemptCount);
        }

        [Fact]
        [BusinessRule("BR-NOT-006")]
        public async Task A_failed_send_stays_queued_for_retry_until_the_third_attempt_then_terminates()
        {
            using var db = CreateContext();
            await SeedQueuedDelivery(db, NotificationChannel.Sms);
            var failing = new FailingChannelSender();

            await Dispatcher(db, failing).DispatchQueuedAsync();
            Assert.Equal(DeliveryStatus.Queued, db.Deliveries.Single().Status); // attempt 1

            await Dispatcher(db, failing).DispatchQueuedAsync();
            Assert.Equal(DeliveryStatus.Queued, db.Deliveries.Single().Status); // attempt 2

            await Dispatcher(db, failing).DispatchQueuedAsync();
            var delivery = db.Deliveries.Single();
            Assert.Equal(DeliveryStatus.Failed, delivery.Status); // attempt 3 — terminal
            Assert.Equal(3, delivery.AttemptCount);
            Assert.Equal("gateway unreachable", delivery.FailureReason);
        }

        [Fact]
        [BusinessRule("BR-NOT-006")]
        public async Task A_successful_SMS_send_increments_the_schools_monthly_budget_counter()
        {
            using var db = CreateContext();
            await SeedQueuedDelivery(db, NotificationChannel.Sms);
            var stub = new StubChannelSender(NotificationChannel.Sms);

            await Dispatcher(db, stub).DispatchQueuedAsync();

            var counter = db.BudgetCounters.Single();
            Assert.Equal(NotificationChannel.Sms, counter.Channel);
            Assert.Equal("2026-08", counter.PeriodKey);
            Assert.Equal(1, counter.MessageCount);

            // A second SMS in the same month accumulates on the same row.
            await SeedQueuedDelivery(db, NotificationChannel.Sms);
            await Dispatcher(db, stub).DispatchQueuedAsync();
            Assert.Equal(2, db.BudgetCounters.Single().MessageCount);
        }

        [Fact]
        [BusinessRule("BR-NOT-006")]
        public async Task A_channel_with_no_registered_sender_fails_immediately_without_a_retry_loop()
        {
            using var db = CreateContext();
            await SeedQueuedDelivery(db, NotificationChannel.WhatsApp);

            await Dispatcher(db, new InAppChannelSender()).DispatchQueuedAsync(); // WhatsApp sender not in the list

            var delivery = db.Deliveries.Single();
            Assert.Equal(DeliveryStatus.Failed, delivery.Status);
            Assert.Contains("No sender registered", delivery.FailureReason);
        }

        private async Task SeedQueuedDelivery(AppDbContext db, NotificationChannel channel)
        {
            await Admin(db).DefineTemplateAsync("Test.Event", channel, null, null, "ar body", "en body");
            await Admin(db).DefineSubscriptionRuleAsync("Test.Event", channel, NotificationTiming.Immediate, isEnabled: true);
            await Publisher(db).PublishAsync("Test.Event", new[] { new NotificationRecipient(_parentUserId, "en") }, new Dictionary<string, string>());
            await db.SaveChangesAsync();
        }
    }
}
