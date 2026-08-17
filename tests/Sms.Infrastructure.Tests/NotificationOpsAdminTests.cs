using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Notifications;
using Sms.Infrastructure.Audit;
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

        // --- BR-NTF-001 template publish lifecycle ---------------------------------------

        [Fact]
        [BusinessRule("BR-NTF-001")]
        public async Task Publishing_before_test_send_is_rejected()
        {
            using var db = CreateContext();
            var configAdmin = new NotificationConfigAdmin(db);
            var opsAdmin = new NotificationOpsAdmin(db);
            var version = await configAdmin.DefineTemplateAsync("Attendance.StudentAbsent", NotificationChannel.InApp, null, null, "غاب", "Absent");

            await Assert.ThrowsAsync<InvalidTemplatePublishTransitionException>(() => opsAdmin.PublishTemplateVersionAsync(version.Id));
        }

        [Fact]
        [BusinessRule("BR-NTF-001")]
        public async Task Test_sending_then_publishing_succeeds()
        {
            using var db = CreateContext();
            var configAdmin = new NotificationConfigAdmin(db);
            var opsAdmin = new NotificationOpsAdmin(db);
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
            var opsAdmin = new NotificationOpsAdmin(db);
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
            var opsAdmin = new NotificationOpsAdmin(db);
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
            var opsAdmin = new NotificationOpsAdmin(db);
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
            var opsAdmin = new NotificationOpsAdmin(db);

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
            var opsAdmin = new NotificationOpsAdmin(db);

            var result = await opsAdmin.EvaluateBudgetAsync(NotificationChannel.Sms, "2027-03", budgetLimit: 100, isSafetyClass: true);

            Assert.False(result.ShouldBlock);
        }
    }
}
