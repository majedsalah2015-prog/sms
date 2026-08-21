using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Jobs;
using Sms.Application.Notifications;
using Sms.Domain.Audit;
using Sms.Domain.Jobs;
using Sms.Domain.Notifications;
using Sms.Domain.Security;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Jobs;
using Sms.Infrastructure.Notifications;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// E-011: JobRunner over a real Sqlite-backed AppDbContext, plus the two
    /// handlers that unlock E-004's checkpoint job and E-007's dispatch
    /// trigger.
    /// </summary>
    public sealed class JobRunnerTests : IDisposable
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

        private sealed class FailingHandler : IJobHandler
        {
            public string JobCode => "Failing";

            public Task RunAsync(CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("boom");
        }

        private sealed class SucceedingHandler : IJobHandler
        {
            public bool Ran { get; private set; }

            public string JobCode => "Succeeding";

            public Task RunAsync(CancellationToken cancellationToken = default)
            {
                Ran = true;
                return Task.CompletedTask;
            }
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();

        public JobRunnerTests()
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

        private JobRunner CreateRunner(AppDbContext db, params IJobHandler[] handlers)
            => new(db, _clock, new AuditEventWriter(db, _tenant, _tenant, _user, _clock, _audit), handlers);

        /// <summary>Counts how many times it was entered, so a second concurrent run is visible rather than inferred.</summary>
        private sealed class CountingHandler : IJobHandler
        {
            public int Entered;

            public string JobCode => "Counting";

            public Task RunAsync(CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref Entered);
                return Task.CompletedTask;
            }
        }

        [Fact]
        [BusinessRule("BR-GLB-090")]
        public async Task A_second_run_of_a_job_already_in_flight_does_not_start()
        {
            using (var db = CreateContext())
            {
                db.JobDefinitions.Add(new JobDefinition { Code = "Counting", CronExpression = "* * * * *" });
                await db.SaveChangesAsync();
            }

            var handler = new CountingHandler();

            // The first run's Running row is still open, which is exactly the state Hangfire leaves
            // when it enqueues one job per occurrence missed during downtime and several workers
            // pick them up together.
            int definitionId;
            using (var db = CreateContext())
            {
                definitionId = (await db.JobDefinitions.SingleAsync(j => j.Code == "Counting")).Id;
                db.JobRuns.Add(new JobRun
                {
                    JobDefinitionId = definitionId,
                    Status = JobStatus.Running,
                    TriggerType = JobTriggerType.Scheduled,
                    StartedAtUtc = _clock.UtcNow,
                });
                await db.SaveChangesAsync();
            }

            using (var db = CreateContext())
            {
                var run = await CreateRunner(db, handler).RunAsync("Counting", JobTriggerType.Scheduled);

                // It reports the run that is already going rather than starting a second one. Two
                // notification dispatches at once would read the same queued rows and send them twice.
                Assert.Equal(JobStatus.Running, run.Status);
            }

            Assert.Equal(0, handler.Entered);
            using (var db = CreateContext())
            {
                Assert.Equal(1, await db.JobRuns.CountAsync(r => r.JobDefinitionId == definitionId));
            }
        }

        [Fact]
        [BusinessRule("BR-GLB-090")]
        public async Task A_run_abandoned_by_a_crash_stops_blocking_the_job()
        {
            using (var db = CreateContext())
            {
                db.JobDefinitions.Add(new JobDefinition { Code = "Counting", CronExpression = "* * * * *" });
                await db.SaveChangesAsync();
                var definitionId = (await db.JobDefinitions.SingleAsync(j => j.Code == "Counting")).Id;

                // Killed mid-job seven hours ago: nothing ever wrote its outcome. Without the reaper
                // the in-flight index would refuse every successor from here to the end of time.
                db.JobRuns.Add(new JobRun
                {
                    JobDefinitionId = definitionId,
                    Status = JobStatus.Running,
                    TriggerType = JobTriggerType.Scheduled,
                    StartedAtUtc = _clock.UtcNow.AddHours(-7),
                });
                await db.SaveChangesAsync();
            }

            var handler = new CountingHandler();
            using (var db = CreateContext())
            {
                var run = await CreateRunner(db, handler).RunAsync("Counting", JobTriggerType.Scheduled);
                Assert.Equal(JobStatus.Succeeded, run.Status);
            }

            Assert.Equal(1, handler.Entered);
            using (var db = CreateContext())
            {
                var abandoned = await db.JobRuns.SingleAsync(r => r.StartedAtUtc < _clock.UtcNow.AddHours(-1));
                Assert.Equal(JobStatus.Failed, abandoned.Status);
                Assert.Contains("Abandoned", abandoned.ErrorMessage);
            }
        }

        [Fact]
        public async Task Running_an_unknown_job_code_throws()
        {
            using var db = CreateContext();
            var runner = CreateRunner(db);

            await Assert.ThrowsAsync<UnknownJobException>(() => runner.RunAsync("Nope", JobTriggerType.Manual));
        }

        [Fact]
        public async Task A_successful_run_records_a_succeeded_JobRun_and_calls_the_handler()
        {
            using var db = CreateContext();
            await new JobDefinitionAdmin(db).DefineJobAsync("Succeeding", "اختبار", "Test", "0 2 * * *", isEnabled: true);
            var handler = new SucceedingHandler();

            var run = await CreateRunner(db, handler).RunAsync("Succeeding", JobTriggerType.Manual);

            Assert.True(handler.Ran);
            Assert.Equal(JobStatus.Succeeded, run.Status);
            Assert.NotNull(run.CompletedAtUtc);
            Assert.Null(run.ErrorMessage);
            Assert.Contains(db.AuditEntries, e => e.Action == AuditAction.JobRun);
        }

        [Fact]
        public async Task A_throwing_handler_records_a_failed_JobRun_instead_of_propagating()
        {
            using var db = CreateContext();
            await new JobDefinitionAdmin(db).DefineJobAsync("Failing", "اختبار", "Test", "0 2 * * *", isEnabled: true);

            var run = await CreateRunner(db, new FailingHandler()).RunAsync("Failing", JobTriggerType.Scheduled);

            Assert.Equal(JobStatus.Failed, run.Status);
            Assert.Equal("boom", run.ErrorMessage);
        }

        [Fact]
        public async Task A_disabled_job_never_reaches_its_handler()
        {
            using var db = CreateContext();
            await new JobDefinitionAdmin(db).DefineJobAsync("Succeeding", "اختبار", "Test", "0 2 * * *", isEnabled: false);
            var handler = new SucceedingHandler();

            var run = await CreateRunner(db, handler).RunAsync("Succeeding", JobTriggerType.Scheduled);

            Assert.False(handler.Ran);
            Assert.Equal(JobStatus.Failed, run.Status);
            Assert.Contains("disabled", run.ErrorMessage);
        }

        [Fact]
        public async Task A_job_with_no_registered_handler_fails_cleanly()
        {
            using var db = CreateContext();
            await new JobDefinitionAdmin(db).DefineJobAsync("Succeeding", "اختبار", "Test", "0 2 * * *", isEnabled: true);

            var run = await CreateRunner(db /* no handlers */).RunAsync("Succeeding", JobTriggerType.Scheduled);

            Assert.Equal(JobStatus.Failed, run.Status);
            Assert.Contains("No handler registered", run.ErrorMessage);
        }

        [Fact]
        public async Task The_running_state_is_visible_before_the_handler_completes()
        {
            using var db = CreateContext();
            await new JobDefinitionAdmin(db).DefineJobAsync("Succeeding", "اختبار", "Test", "0 2 * * *", isEnabled: true);

            // A slow handler that lets us peek at storage mid-run via a second context.
            var started = new TaskCompletionSource<bool>();
            var release = new TaskCompletionSource<bool>();
            var slow = new SlowHandler(started, release);

            var runTask = CreateRunner(db, slow).RunAsync("Succeeding", JobTriggerType.Manual);
            await started.Task;

            using (var check = CreateContext())
            {
                Assert.Equal(JobStatus.Running, check.JobRuns.Single().Status);
            }

            release.SetResult(true);
            await runTask;
        }

        private sealed class SlowHandler : IJobHandler
        {
            private readonly TaskCompletionSource<bool> _started;
            private readonly TaskCompletionSource<bool> _release;

            public SlowHandler(TaskCompletionSource<bool> started, TaskCompletionSource<bool> release)
            {
                _started = started;
                _release = release;
            }

            public string JobCode => "Succeeding";

            public async Task RunAsync(CancellationToken cancellationToken = default)
            {
                _started.SetResult(true);
                await _release.Task;
            }
        }

        // --- the two unlocked handlers -----------------------------------------

        [Fact]
        [BusinessRule("BR-AUD-007")]
        public async Task Audit_checkpoint_handler_computes_the_just_completed_day()
        {
            using var db = CreateContext();
            var handler = new AuditCheckpointJobHandler(new IntegrityCheckpointService(db, _clock), _clock);

            await handler.RunAsync();

            var checkpoint = db.IntegrityCheckpoints.Single();
            Assert.Equal(new DateTime(2026, 8, 14), checkpoint.PeriodStartUtc);
            Assert.Equal(new DateTime(2026, 8, 15), checkpoint.PeriodEndUtc);
        }

        [Fact]
        [BusinessRule("BR-NOT-006")]
        public async Task Notification_dispatch_handler_drains_queued_deliveries()
        {
            using var db = CreateContext();
            var account = new UserAccount { UserName = "parent1", AccountType = AccountType.Parent };
            db.UserAccounts.Add(account);
            db.SaveChanges();

            var configAdmin = new NotificationConfigAdmin(db);
            await configAdmin.DefineTemplateAsync("Test.Event", NotificationChannel.InApp, null, null, "ar", "en");
            await configAdmin.DefineSubscriptionRuleAsync("Test.Event", NotificationChannel.InApp, NotificationTiming.Immediate, isEnabled: true);
            await new NotificationPublisher(db).PublishAsync(
                "Test.Event",
                new[] { new NotificationRecipient(account.Id, "en") },
                new Dictionary<string, string>());
            await db.SaveChangesAsync();

            var dispatcher = new NotificationDispatcher(db, _clock, new IChannelSender[] { new InAppChannelSender() });
            var handler = new NotificationDispatchJobHandler(dispatcher);

            await handler.RunAsync();

            Assert.Equal(DeliveryStatus.Delivered, db.Deliveries.Single().Status);
        }
    }
}
