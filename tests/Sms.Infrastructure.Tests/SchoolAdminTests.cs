using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Schools;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Schools;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// E-102 slice 1: School module (BR-SCH-001..008) over a real
    /// Sqlite-backed AppDbContext.
    /// </summary>
    public sealed class SchoolAdminTests : IDisposable
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

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();

        public SchoolAdminTests()
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

        // --- identity + BR-SCH-002 reason requirement ---------------------------

        [Fact]
        [BusinessRule("BR-SCH-001")]
        public async Task Defining_a_new_school_sets_its_identity_and_defaults_to_Setup()
        {
            using var db = CreateContext();
            var admin = new SchoolAdmin(db);

            var school = await admin.DefineSchoolAsync(
                null, "مدرسة الأندلس", "Al-Andalus School", "LIC-001", "MIN-001", "Arab Standard Time", "SAR");

            Assert.Equal(SchoolStatus.Setup, school.Status);
            Assert.Equal("Al-Andalus School", db.Schools.Single().NameEn);
        }

        [Fact]
        [BusinessRule("BR-SCH-002")]
        public async Task Editing_identity_fields_without_a_reason_is_rejected()
        {
            using var db = CreateContext();
            var admin = new SchoolAdmin(db);
            var school = await admin.DefineSchoolAsync(
                null, "مدرسة الأندلس", "Al-Andalus School", "LIC-001", "MIN-001", "Arab Standard Time", "SAR");

            _audit.Reason = null;
            await Assert.ThrowsAsync<MissingAuditReasonException>(() =>
                admin.DefineSchoolAsync(school.Id, "مدرسة الأندلس الجديدة", "New Name", "LIC-001", "MIN-001", "Arab Standard Time", "SAR"));
        }

        [Fact]
        [BusinessRule("BR-SCH-002")]
        public async Task Editing_identity_fields_with_a_reason_is_recorded_and_T1_audited()
        {
            using var db = CreateContext();
            var admin = new SchoolAdmin(db);
            var school = await admin.DefineSchoolAsync(
                null, "مدرسة الأندلس", "Al-Andalus School", "LIC-001", "MIN-001", "Arab Standard Time", "SAR");

            _audit.Reason = "Rebranding after merger";
            await admin.DefineSchoolAsync(school.Id, "مدرسة الأندلس الجديدة", "New Name", "LIC-001", "MIN-001", "Arab Standard Time", "SAR");

            Assert.Equal("New Name", db.Schools.Single().NameEn);
            Assert.Contains(db.AuditEntries, e => e.EntityType == nameof(School) && e.FieldName == nameof(School.NameEn) && e.Reason == "Rebranding after merger");
        }

        // --- BR-SCH-005 status lifecycle -----------------------------------------

        [Fact]
        [BusinessRule("BR-SCH-005")]
        public async Task Activation_moves_Setup_to_Active()
        {
            using var db = CreateContext();
            var admin = new SchoolAdmin(db);
            var school = await admin.DefineSchoolAsync(
                null, "مدرسة الأندلس", "Al-Andalus School", "LIC-001", "MIN-001", "Arab Standard Time", "SAR");

            _audit.Reason = "Wizard complete";
            await admin.ChangeStatusAsync(school.Id, SchoolStatus.Active);

            Assert.Equal(SchoolStatus.Active, db.Schools.Single().Status);
        }

        [Fact]
        [BusinessRule("BR-SCH-005")]
        public async Task An_illegal_transition_is_rejected_and_never_persisted()
        {
            using var db = CreateContext();
            var admin = new SchoolAdmin(db);
            var school = await admin.DefineSchoolAsync(
                null, "مدرسة الأندلس", "Al-Andalus School", "LIC-001", "MIN-001", "Arab Standard Time", "SAR");

            _audit.Reason = "attempt";
            await Assert.ThrowsAsync<InvalidSchoolStatusTransitionException>(() =>
                admin.ChangeStatusAsync(school.Id, SchoolStatus.Closed)); // Setup -> Closed is illegal

            Assert.Equal(SchoolStatus.Setup, db.Schools.Single().Status);
        }

        [Fact]
        [BusinessRule("BR-SCH-005")]
        public async Task Closed_is_terminal_even_via_the_admin_service()
        {
            using var db = CreateContext();
            var admin = new SchoolAdmin(db);
            var school = await admin.DefineSchoolAsync(
                null, "مدرسة الأندلس", "Al-Andalus School", "LIC-001", "MIN-001", "Arab Standard Time", "SAR");

            _audit.Reason = "lifecycle";
            await admin.ChangeStatusAsync(school.Id, SchoolStatus.Active);
            await admin.ChangeStatusAsync(school.Id, SchoolStatus.Closed);

            await Assert.ThrowsAsync<InvalidSchoolStatusTransitionException>(() =>
                admin.ChangeStatusAsync(school.Id, SchoolStatus.Active));
        }

        // --- BR-SCH-004 signatory effective-dating -------------------------------

        [Fact]
        [BusinessRule("BR-SCH-004")]
        public async Task A_new_signatory_closes_out_the_previous_one_for_the_same_document_class()
        {
            using var db = CreateContext();
            var admin = new SchoolAdmin(db);
            var first = await admin.DefineSignatoryAsync(
                "Certificate", "أحمد", "Ahmad", "المدير", "Principal", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            var second = await admin.DefineSignatoryAsync(
                "Certificate", "سارة", "Sarah", "المديرة", "Principal", new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));

            var storedFirst = db.Signatories.Single(s => s.Id == first.Id);
            Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), storedFirst.EffectiveToUtc);
            Assert.Null(db.Signatories.Single(s => s.Id == second.Id).EffectiveToUtc);
        }

        [Fact]
        [BusinessRule("BR-SCH-004")]
        public async Task Different_document_classes_have_independent_current_signatories()
        {
            using var db = CreateContext();
            var admin = new SchoolAdmin(db);
            await admin.DefineSignatoryAsync("Certificate", "أحمد", "Ahmad", "المدير", "Principal", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            await admin.DefineSignatoryAsync("Financial", "منى", "Mona", "المديرة المالية", "Finance Manager", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            Assert.Equal(2, db.Signatories.Count(s => s.EffectiveToUtc == null));
        }
    }
}
