using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Fees;
using Sms.Domain.Numbering;
using Sms.Domain.Schools;
using Sms.Domain.Teachers;
using Sms.Infrastructure.Attendance;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Calendar;
using Sms.Infrastructure.Employees;
using Sms.Infrastructure.Fees;
using Sms.Infrastructure.Grades;
using Sms.Infrastructure.Lookups;
using Sms.Infrastructure.Notifications;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Parents;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Schools;
using Sms.Infrastructure.Sections;
using Sms.Infrastructure.Setup;
using Sms.Infrastructure.Seeding;
using Sms.Infrastructure.Students;
using Sms.Infrastructure.Subjects;
using Sms.Infrastructure.Teachers;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// S3/E-305 "demo seed complete" over a real Sqlite-backed AppDbContext.
    /// This is the whole-stack smoke test: composing essentially every
    /// S0-S3 admin service into one demo tenant and asserting the chain
    /// held together end to end.
    /// </summary>
    public sealed class DemoSeedContributorTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            // Deliberately BEFORE yearStart (2027-09-01) - the normal pilot-onboarding case, and the scenario
            // that exposed DemoSeedContributor's contract-backdating fix (BR-TCH-001's active-contract check).
            public DateTime UtcNow { get; set; } = new(2027, 8, 15, 8, 0, 0, DateTimeKind.Utc);
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

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();

        public DemoSeedContributorTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext(new AuditContext());
            db.Database.EnsureCreated();

            foreach (var (code, entity, format) in new[]
            {
                ("STU", "Student", "STU-{SEQ:6}"), ("PAR", "Parent", "PAR-{SEQ:6}"),
                ("EMP", "Employee", "EMP-{SEQ:5}"), ("INV", "Charge", "INV-{SEQ:6}"),
            })
            {
                db.NumberingSeries.Add(new NumberingSeries
                {
                    Code = code, EntityName = entity, FormatTemplate = format,
                    ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Normal, EffectiveFromUtc = _clock.UtcNow, IsActive = true,
                });
            }

            db.SaveChanges();

            // E-101: the demo tenant now walks the Setup Wizard, which needs the
            // product Currency lookup (BR-GLB-112) and the KSA-01 country pack.
            var lookups = new LookupAdmin(db);
            lookups.DefineCategoryAsync("Currency", Sms.Domain.Lookups.LookupCategoryTier.ProductSeeded, "العملة", "Currency").GetAwaiter().GetResult();
            lookups.DefineValueAsync("Currency", "SAR", "ريال سعودي", "Saudi Riyal", sortOrder: 1).GetAwaiter().GetResult();
            new Ksa01ContentPackSeedContributor(lookups, CreateSetupAdmin(db, new AuditContext())).SeedAsync().GetAwaiter().GetResult();
        }

        public void Dispose() => _connection.Dispose();

        private SystemSetupAdmin CreateSetupAdmin(AppDbContext db, AuditContext audit) =>
            new(db, _tenant, _clock, _user, audit, new NotificationPublisher(db, new TestAddressBook()));

        private AppDbContext CreateContext(AuditContext audit)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, audit);
        }

        private DemoSeedContributor CreateContributor(AppDbContext db, AuditContext audit)
        {
            var numberIssuer = new NumberIssuer(db, _tenant, _tenant, _clock);
            return new DemoSeedContributor(
                db, audit, _clock, new SchoolAdmin(db), new AcademicYearAdmin(db), new GradeStructureAdmin(db),
                new SectionAdmin(db), new SubjectAdmin(db, new CurriculumOfferingUsageInspector(db)), new CalendarAdmin(db, _clock),
                new EmployeeAdmin(db, numberIssuer), new TeacherAdmin(db, _clock),
                new ParentAdmin(db, numberIssuer), new StudentAdmin(db, numberIssuer, new AuditEventWriter(db, _tenant, _tenant, _user, _clock, audit)),
                new AttendanceAdmin(db), new FeeAdmin(db, numberIssuer, _clock), CreateSetupAdmin(db, audit));
        }

        [Fact]
        [BusinessRule("BR-GLB-002")]
        public async Task Seeding_builds_a_coherent_demo_tenant_end_to_end()
        {
            var audit = new AuditContext();
            using var db = CreateContext(audit);
            var contributor = CreateContributor(db, audit);

            await contributor.SeedAsync();

            var school = db.Schools.Single();
            Assert.Equal(SchoolStatus.Active, school.Status);

            var year = db.AcademicYears.Single();
            Assert.Equal(AcademicYearStatus.Active, year.Status);

            var section = db.Sections.Single();
            Assert.Single(db.SectionMemberships.Where(m => m.SectionId == section.Id));

            var teacherAssignment = db.TeacherAssignments.Single();
            Assert.Equal(TeacherRole.Primary, teacherAssignment.Role);

            var charge = db.Charges.Single();
            Assert.Equal(ChargeStatus.Posted, charge.Status);
            Assert.Equal(12000m, charge.GrossAmount);

            Assert.Single(db.Students);
            Assert.Single(db.Parents);
            Assert.Equal(2, db.AttendanceDays.Count());
        }

        [Fact]
        [BusinessRule("BR-SET-002")]
        public async Task Re_running_does_not_duplicate_the_demo_tenant()
        {
            var audit = new AuditContext();
            using (var db = CreateContext(audit))
            {
                await CreateContributor(db, audit).SeedAsync();
            }

            using (var db = CreateContext(audit))
            {
                await CreateContributor(db, audit).SeedAsync();
                Assert.Single(db.Schools);
                Assert.Single(db.Students);
            }
        }
    }
}
