using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    public sealed class SmsDbContextConventionTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new DateTime(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; } = 42;
        }

        private sealed class FixedTenant : ITenantContext
        {
            public FixedTenant(int schoolId)
            {
                SchoolId = schoolId;
            }

            public int SchoolId { get; }
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();

        public SmsDbContextConventionTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            using var setup = CreateContext(schoolId: 1);
            setup.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        private TestDbContext CreateContext(int schoolId)
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlite(_connection)
                .Options;

            return new TestDbContext(options, new FixedTenant(schoolId), _user, _clock);
        }

        [Fact]
        [BusinessRule("BR-GLB-010")]
        public void Tenant_filter_isolates_schools()
        {
            using (var school1 = CreateContext(schoolId: 1))
            {
                school1.MasterItems.Add(new MasterItem { Name = "School1 item" });
                school1.SaveChanges();
            }

            using (var school2 = CreateContext(schoolId: 2))
            {
                school2.MasterItems.Add(new MasterItem { Name = "School2 item" });
                school2.SaveChanges();

                var visible = school2.MasterItems.ToList();

                Assert.Single(visible);
                Assert.Equal("School2 item", visible[0].Name);
            }
        }

        [Fact]
        [BusinessRule("BR-GLB-010")]
        public void Adding_a_row_for_another_school_throws()
        {
            using var context = CreateContext(schoolId: 1);
            context.MasterItems.Add(new MasterItem { Name = "Wrong school", SchoolId = 99 });

            Assert.Throws<CrossSchoolWriteException>(() => context.SaveChanges());
        }

        [Fact]
        [BusinessRule("BR-GLB-010")]
        public void Added_rows_inherit_the_tenant_school_id()
        {
            using var context = CreateContext(schoolId: 7);
            var item = new MasterItem { Name = "Implicit school" };
            context.MasterItems.Add(item);
            context.SaveChanges();

            Assert.Equal(7, item.SchoolId);
        }

        [Fact]
        [BusinessRule("BR-GLB-007")]
        public void Create_and_modify_stamps_are_applied_centrally()
        {
            using var context = CreateContext(schoolId: 1);
            var item = new MasterItem { Name = "Stamped" };
            context.MasterItems.Add(item);
            context.SaveChanges();

            Assert.Equal(_clock.UtcNow, item.CreatedAtUtc);
            Assert.Equal(42, item.CreatedByUserId);
            Assert.Null(item.ModifiedAtUtc);

            _clock.UtcNow = _clock.UtcNow.AddHours(1);
            _user.UserId = 43;
            item.Name = "Renamed";
            context.SaveChanges();

            Assert.Equal(_clock.UtcNow, item.ModifiedAtUtc);
            Assert.Equal(43, item.ModifiedByUserId);
        }

        [Fact]
        [BusinessRule("BR-GLB-005")]
        public void Deactivated_master_data_is_hidden_but_not_gone()
        {
            using var context = CreateContext(schoolId: 1);
            var item = new MasterItem { Name = "Retiring" };
            context.MasterItems.Add(item);
            context.SaveChanges();

            item.IsActive = false;
            context.SaveChanges();

            Assert.Empty(context.MasterItems.ToList());
            Assert.Single(context.MasterItems.IgnoreQueryFilters().Where(m => m.SchoolId == 1).ToList());
        }

        [Fact]
        [BusinessRule("BR-GLB-005")]
        public void Hard_delete_of_master_data_throws()
        {
            using var context = CreateContext(schoolId: 1);
            var item = new MasterItem { Name = "Protected" };
            context.MasterItems.Add(item);
            context.SaveChanges();

            context.MasterItems.Remove(item);

            Assert.Throws<HardDeleteForbiddenException>(() => context.SaveChanges());
        }

        [Fact]
        [BusinessRule("BR-GLB-020")]
        public void Transactional_rows_carry_school_and_year_scope()
        {
            using var context = CreateContext(schoolId: 1);
            context.TransactionItems.Add(new TransactionItem { Reference = "TX-1", AcademicYearId = 2027 });
            context.SaveChanges();

            var row = context.TransactionItems.Single();
            Assert.Equal(1, row.SchoolId);
            Assert.Equal(2027, row.AcademicYearId);
        }
    }
}
