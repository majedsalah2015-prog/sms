using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Notifications;
using Sms.Domain.Notifications;
using Sms.Domain.Schools;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Notifications;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Seeding;
using Sms.Infrastructure.Setup;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// The seeded notification wording (doc 09 §5). The interesting assertions are not that
    /// rows appear — it is that every placeholder in them is one the publishing module
    /// actually sends, and that a school's own rewrite survives the next deployment.
    /// </summary>
    public sealed class NotificationTemplateSeedContributorTests : IDisposable
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

            public int AcademicYearId { get; set; } = 1;
        }

        private sealed class NoProtector : ISecretProtector
        {
            public string Protect(string plaintext) => plaintext;

            public string? Unprotect(string? cipher) => cipher;
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();

        public NotificationTemplateSeedContributorTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            // The contributor is a no-op without a school, which is itself the guard that keeps
            // it from writing into an empty database.
            db.Schools.Add(new School { NameAr = "مدرسة", NameEn = "School", MinistryCode = "M-1" });
            db.SaveChanges();
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private NotificationTemplateSeedContributor Contributor(AppDbContext db)
        {
            var config = new NotificationConfigAdmin(db);
            var setup = new SystemSetupAdmin(db, _tenant, _clock, _user, _audit, new NotificationPublisher(db, new TestAddressBook()));
            var ops = new NotificationOpsAdmin(db, _clock, new NoProtector(), setup, new TestAddressBook(), Array.Empty<IChannelSender>());
            return new NotificationTemplateSeedContributor(db, config, ops);
        }

        [Fact]
        public async Task Every_seeded_template_only_uses_placeholders_its_event_supplies()
        {
            using var db = CreateContext();
            await Contributor(db).SeedAsync();

            var templates = await db.Templates.IgnoreQueryFilters().ToListAsync();
            Assert.NotEmpty(templates);

            foreach (var template in templates)
            {
                var version = db.TemplateVersions.IgnoreQueryFilters()
                    .Single(v => v.TemplateId == template.Id && v.VersionNumber == template.CurrentVersionNumber);

                // The whole point of the seed: a token the module never sends would reach a
                // parent as the word itself, because TemplateRenderer leaves unknowns in place.
                var unknown = TemplatePlaceholderRules.Unknown(
                    template.EventCode, version.SubjectAr, version.SubjectEn, version.BodyAr, version.BodyEn);

                Assert.True(unknown.Count == 0,
                    $"{template.EventCode} uses placeholders its publisher does not send: {string.Join(", ", unknown)}");
            }
        }

        [Fact]
        public async Task Seeded_templates_are_live_and_bilingual()
        {
            using var db = CreateContext();
            await Contributor(db).SeedAsync();

            foreach (var version in await db.TemplateVersions.IgnoreQueryFilters().ToListAsync())
            {
                // A draft would leave every notification one manual step short of working.
                Assert.Equal(TemplatePublishStatus.Published, version.PublishStatus);

                // Whichever language a parent chose has to have something in it.
                Assert.False(string.IsNullOrWhiteSpace(version.BodyAr));
                Assert.False(string.IsNullOrWhiteSpace(version.BodyEn));
                Assert.False(string.IsNullOrWhiteSpace(version.SubjectAr));
                Assert.False(string.IsNullOrWhiteSpace(version.SubjectEn));
            }
        }

        [Fact]
        public async Task Only_events_a_module_actually_raises_get_wording()
        {
            using var db = CreateContext();
            await Contributor(db).SeedAsync();

            foreach (var template in await db.Templates.IgnoreQueryFilters().ToListAsync())
            {
                Assert.True(NotificationEventCatalog.TryGet(template.EventCode, out var catalogued));

                // Content for an event nothing raises can never render; the studio is where a
                // school writes ahead of a module if it wants to.
                Assert.True(catalogued.HasPublisher, $"{template.EventCode} has no publisher and should not be seeded.");
            }
        }

        [Fact]
        public async Task Re_running_never_overwrites_what_a_school_rewrote()
        {
            using var db = CreateContext();
            await Contributor(db).SeedAsync();

            var before = db.Templates.IgnoreQueryFilters().Count();

            // The school rewrites one of them in the studio, which writes a new version.
            var config = new NotificationConfigAdmin(db);
            await config.DefineTemplateAsync(
                "LibraryOverdue", NotificationChannel.InApp, "عنوان المدرسة", "School subject", "نصنا", "Our words");

            using var second = CreateContext();
            await Contributor(second).SeedAsync();

            Assert.Equal(before, second.Templates.IgnoreQueryFilters().Count());

            var template = second.Templates.IgnoreQueryFilters().Single(t => t.EventCode == "LibraryOverdue");
            var live = second.TemplateVersions.IgnoreQueryFilters()
                .Single(v => v.TemplateId == template.Id && v.VersionNumber == template.CurrentVersionNumber);

            Assert.Equal("Our words", live.BodyEn);
        }

        [Fact]
        public async Task A_retired_template_is_left_retired_rather_than_duplicated()
        {
            using var db = CreateContext();
            await Contributor(db).SeedAsync();

            // Template is ISoftActiveFiltered: a retired row is invisible to a plain query, and
            // reading it as missing would insert a second one onto the unique index over
            // (SchoolId, EventCode, Channel).
            var retired = db.Templates.IgnoreQueryFilters().First(t => t.EventCode == "TransportSuspended");
            retired.IsActive = false;
            await db.SaveChangesAsync();

            var before = db.Templates.IgnoreQueryFilters().Count();

            using var second = CreateContext();
            await Contributor(second).SeedAsync();

            Assert.Equal(before, second.Templates.IgnoreQueryFilters().Count());
            Assert.False(second.Templates.IgnoreQueryFilters().Single(t => t.Id == retired.Id).IsActive);
        }
    }
}
