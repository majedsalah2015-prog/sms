using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Jobs;
using Sms.Domain.Jobs;
using Sms.Infrastructure.Jobs;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// Pins the registry the runner resolves every job against. The bug behind
    /// these tests was total and silent: nothing anywhere called
    /// <c>DefineJobAsync</c>, so <c>ops.JobDefinition</c> was empty and all five
    /// recurring jobs threw <c>UnknownJobException</c> on every fire — while the
    /// scheduler and every handler worked exactly as designed.
    /// </summary>
    public sealed class JobDefinitionRegistrarTests : IDisposable
    {
        private sealed class Tenant : ITenantContext
        {
            public int SchoolId => 1;
        }

        private sealed class User : ICurrentUser
        {
            public int UserId => 1;
        }

        private sealed class Clock : IClock
        {
            public DateTime UtcNow => new(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);
        }

        private readonly SqliteConnection _connection;

        public JobDefinitionRegistrarTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
            => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, new Tenant(), new User(), new Clock());

        [Fact]
        [BusinessRule("BR-GLB-090")]
        public async Task Every_catalogued_job_gets_a_row_and_re_running_adds_none()
        {
            using (var db = CreateContext())
            {
                await JobDefinitionRegistrar.EnsureAsync(db);
            }

            using (var db = CreateContext())
            {
                var codes = await db.JobDefinitions.Select(j => j.Code).ToListAsync();
                Assert.Equal(JobCatalog.Jobs.Select(j => j.Code).OrderBy(c => c), codes.OrderBy(c => c));
            }

            using (var db = CreateContext())
            {
                await JobDefinitionRegistrar.EnsureAsync(db);
            }

            using (var db = CreateContext())
            {
                Assert.Equal(JobCatalog.Jobs.Count, await db.JobDefinitions.CountAsync());
            }
        }

        [Fact]
        [BusinessRule("BR-GLB-090")]
        public void Every_job_the_runner_can_be_asked_for_has_a_handler_behind_it()
        {
            // A definition with no handler is worse than no definition: the run is recorded, marked
            // Failed, and looks like the handler broke rather than like it was never registered.
            var handlerCodes = typeof(JobRunner).Assembly.GetTypes()
                .Where(t => typeof(IJobHandler).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                .Select(t => ((IJobHandler)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(t)).JobCode)
                .ToHashSet();

            foreach (var job in JobCatalog.Jobs)
            {
                Assert.Contains(job.Code, handlerCodes);
            }
        }

        [Fact]
        [BusinessRule("BR-GLB-090")]
        public async Task A_changed_schedule_is_pushed_but_a_disabled_job_stays_disabled()
        {
            using (var db = CreateContext())
            {
                await JobDefinitionRegistrar.EnsureAsync(db);
            }

            using (var db = CreateContext())
            {
                var job = await db.JobDefinitions.SingleAsync(j => j.Code == "NotificationDispatch");
                job.CronExpression = "0 0 1 1 *";   // someone edited the row by hand
                job.IsEnabled = false;              // an operator turned the job off
                await db.SaveChangesAsync();
            }

            using (var db = CreateContext())
            {
                await JobDefinitionRegistrar.EnsureAsync(db);
            }

            using (var db = CreateContext())
            {
                var job = await db.JobDefinitions.SingleAsync(j => j.Code == "NotificationDispatch");

                // The schedule comes back, because Hangfire is scheduled from the same catalogue and a
                // row saying otherwise would be a row that lies about when the job runs.
                Assert.Equal(JobCatalog.Jobs.Single(j => j.Code == "NotificationDispatch").CronExpression, job.CronExpression);

                // The switch does not, because it is the one field here an operator owns.
                Assert.False(job.IsEnabled);
            }
        }

    }
}
