using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Common;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Security;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    public sealed class PermissionServiceTests : IDisposable
    {
        private sealed class Tenant : ITenantContext
        {
            public int SchoolId => 1;
        }

        private sealed class User : ICurrentUser
        {
            public int UserId { get; set; }
        }

        private sealed class Clock : IClock
        {
            public DateTime UtcNow => new(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);
        }

        private readonly SqliteConnection _connection;
        private readonly User _user = new();
        private readonly int _teacherAccountId;
        private readonly int _teacherRoleId;

        public PermissionServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            using var db = CreateContext();
            db.Database.EnsureCreated();

            var view = new Permission { ModuleCode = "STU", ScreenCode = "StudentList", Action = ActionVerb.View };
            var edit = new Permission { ModuleCode = "STU", ScreenCode = "StudentList", Action = ActionVerb.Edit };
            db.Permissions.AddRange(view, edit);

            var teacher = new Role { Code = "TEACHER", Name = new LocalizedName("معلم", "Teacher") };
            db.Roles.Add(teacher);

            var account = new UserAccount { UserName = "t.ahmad", AccountType = AccountType.Staff };
            db.UserAccounts.Add(account);
            db.SaveChanges();

            db.RolePermissions.Add(new RolePermission { RoleId = teacher.Id, PermissionId = view.Id });
            var assignment = new RoleAssignment { UserAccountId = account.Id, RoleId = teacher.Id };
            assignment.ScopeGrants.Add(new ScopeGrant { Dimension = ScopeDimension.Section, ScopeValueId = null });
            db.RoleAssignments.Add(assignment);
            db.SaveChanges();

            _teacherAccountId = account.Id;
            _teacherRoleId = teacher.Id;
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            return new AppDbContext(options, new Tenant(), _user, new Clock());
        }

        [Fact]
        [BusinessRule("BR-GLB-070")]
        public async Task Granted_permission_resolves_true_and_ungranted_false()
        {
            _user.UserId = _teacherAccountId;
            using var db = CreateContext();
            var service = new PermissionService(db, _user);

            Assert.True(await service.HasPermissionAsync("STU", "StudentList", ActionVerb.View));
            Assert.False(await service.HasPermissionAsync("STU", "StudentList", ActionVerb.Edit));
            Assert.False(await service.HasPermissionAsync("FEE", "InvoiceList", ActionVerb.View));
        }

        [Fact]
        [BusinessRule("BR-GLB-071")]
        public async Task Dynamic_own_sections_scope_round_trips_from_the_database()
        {
            _user.UserId = _teacherAccountId;
            using var db = CreateContext();
            var service = new PermissionService(db, _user);

            var scope = await service.GetEffectiveScopeAsync("STU", "StudentList", ActionVerb.View);

            Assert.NotNull(scope);
            Assert.True(scope!.IncludesDynamicOwnSections);
        }

        [Fact]
        [BusinessRule("BR-GLB-070")]
        public async Task Deactivated_assignment_denies_immediately()
        {
            _user.UserId = _teacherAccountId;

            using (var db = CreateContext())
            {
                var assignment = await db.RoleAssignments.SingleAsync(a => a.UserAccountId == _teacherAccountId);
                assignment.IsActive = false;
                await db.SaveChangesAsync();
            }

            using (var db = CreateContext())
            {
                var service = new PermissionService(db, _user);
                Assert.False(await service.HasPermissionAsync("STU", "StudentList", ActionVerb.View));
            }
        }

        [Fact]
        [BusinessRule("BR-GLB-070")]
        public async Task Unknown_user_has_no_permissions()
        {
            _user.UserId = 99_999;
            using var db = CreateContext();
            var service = new PermissionService(db, _user);

            Assert.False(await service.HasPermissionAsync("STU", "StudentList", ActionVerb.View));
        }

        [Fact]
        [BusinessRule("BR-GLB-010")]
        public async Task Roles_are_tenant_isolated()
        {
            _user.UserId = _teacherAccountId;
            using var db = CreateContext();

            Assert.Equal(1, (await db.Roles.SingleAsync(r => r.Id == _teacherRoleId)).SchoolId);
        }
    }
}
