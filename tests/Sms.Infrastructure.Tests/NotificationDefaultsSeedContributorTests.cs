using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Notifications;
using Sms.Domain.Schools;
using Sms.Infrastructure.Notifications;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    public sealed class NotificationDefaultsSeedContributorTests : IDisposable
    {
        private sealed class Tenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;

            public int AcademicYearId => 1;
        }

        private sealed class User : ICurrentUser
        {
            public int UserId => 0;
        }

        private sealed class Clock : IClock
        {
            public DateTime UtcNow => new(2026, 8, 24, 9, 0, 0, DateTimeKind.Utc);
        }

        private readonly SqliteConnection _connection;

        public NotificationDefaultsSeedContributorTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();
            db.Schools.Add(new School { NameAr = "مدرسة", NameEn = "School", LicenseNumber = "LIC-1", MinistryCode = "MIN-1" });
            db.SaveChanges();
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
            => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, new Tenant(), new User(), new Clock());

        /// <summary>
        /// The switch a school flips twice. <c>SubscriptionRule.IsActive</c> is the
        /// on/off itself and the entity is soft-active filtered, so a disabled rule was
        /// invisible to its own upsert: re-enabling it inserted a second row and died
        /// on the unique index over (school, event, channel) with a
        /// <c>DbUpdateException</c> — which is not an <c>InvalidOperationException</c>,
        /// so no controller's catch would have translated it. It would have shipped as
        /// a 500 on the second click of a toggle.
        /// </summary>
        [Fact]
        [BusinessRule("BR-NOT-003")]
        public async Task A_rule_the_school_switched_off_can_be_switched_back_on()
        {
            using (var db = CreateContext())
            {
                var admin = new NotificationConfigAdmin(db);
                await admin.DefineSubscriptionRuleAsync("Attendance.StudentAbsent", NotificationChannel.InApp, NotificationTiming.Immediate, isEnabled: true);
                await admin.DefineSubscriptionRuleAsync("Attendance.StudentAbsent", NotificationChannel.InApp, NotificationTiming.Immediate, isEnabled: false);
            }

            using (var db = CreateContext())
            {
                var admin = new NotificationConfigAdmin(db);
                var revived = await admin.DefineSubscriptionRuleAsync(
                    "Attendance.StudentAbsent", NotificationChannel.InApp, NotificationTiming.Digest, isEnabled: true);

                Assert.True(revived.IsActive);
                Assert.Equal(NotificationTiming.Digest, revived.Timing);
            }

            using var after = CreateContext();
            Assert.Single(after.SubscriptionRules.IgnoreQueryFilters()
                .Where(r => r.EventCode == "Attendance.StudentAbsent" && r.Channel == NotificationChannel.InApp)
                .ToList());
        }
    }
}
