using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Numbering;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Parents;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>E-202 (slice: Parents, doc/Modules/11, BR-PAR-001) over a real Sqlite-backed AppDbContext with a real INumberIssuer (PAR series).</summary>
    public sealed class ParentAdminTests : IDisposable
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

        public ParentAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();
            db.NumberingSeries.Add(new NumberingSeries
            {
                Code = "PAR", EntityName = "Parent", FormatTemplate = "PAR-{SEQ:6}",
                ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Normal, EffectiveFromUtc = _clock.UtcNow, IsActive = true,
            });
            db.SaveChanges();
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        [Fact]
        [BusinessRule("BR-PAR-001")]
        public async Task Registering_a_parent_issues_a_real_permanent_file_number_via_the_PAR_series()
        {
            using var db = CreateContext();
            var admin = new ParentAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));

            var parent = await admin.RegisterParentAsync("أحمد محمد", "Ahmad Mohammed", "0501234567");

            Assert.Equal("PAR-000001", parent.ParentFileNo);
            Assert.Equal("0501234567", db.Parents.Single(p => p.Id == parent.Id).PrimaryMobile);
        }

        [Fact]
        [BusinessRule("BR-PAR-001")]
        public async Task Two_parents_never_share_a_file_number()
        {
            using var db = CreateContext();
            var admin = new ParentAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));

            var first = await admin.RegisterParentAsync("أحمد", "Ahmad", "0501111111");
            var second = await admin.RegisterParentAsync("سارة", "Sarah", "0502222222");

            Assert.NotEqual(first.ParentFileNo, second.ParentFileNo);
        }

        [Fact]
        [BusinessRule("BR-PAR-001")]
        public async Task Renaming_a_parent_requires_an_audit_reason_but_contact_edits_do_not()
        {
            using var db = CreateContext();
            var admin = new ParentAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var parent = await admin.RegisterParentAsync("أحمد", "Ahmad", "0501111111");

            _audit.Reason = null;
            var contactOnly = await admin.UpdateParentAsync(parent.Id, "أحمد", "Ahmad", "0509999999", email: "a@example.com");
            Assert.Equal("0509999999", contactOnly.PrimaryMobile);

            await Assert.ThrowsAsync<Sms.Application.Common.Exceptions.MissingAuditReasonException>(() =>
                admin.UpdateParentAsync(parent.Id, "أحمد علي", "Ahmad Ali", "0509999999"));

            _audit.Reason = "ID correction";
            Assert.Equal("Ahmad Ali", (await admin.UpdateParentAsync(parent.Id, "أحمد علي", "Ahmad Ali", "0509999999")).NameEn);
            _audit.Reason = null;
        }
    }
}
