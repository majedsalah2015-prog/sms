using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Security;
using Sms.Domain.Common;
using Sms.Domain.Employees;
using Sms.Domain.Parents;
using Sms.Domain.Security;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Security;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// Module 36 §8.1's account lifecycle over a real Sqlite-backed <see cref="AppDbContext"/> — the
    /// screen that ended "a new employee's login is an INSERT somebody writes by hand".
    /// <para>
    /// Most of what follows is refusals. Provisioning is a short method; what makes it worth having
    /// is that it will not issue a second login for the same person, will not reissue a name a
    /// deactivated account still holds, and will not let an administrator remove the last person who
    /// can administer permissions — including by removing themselves.
    /// </para>
    /// </summary>
    public sealed class UserAccountAdminTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 8, 26, 9, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; } = 1;
        }

        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;

            public int AcademicYearId => 2026;
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();
        private readonly IPasswordHasher _hasher = new PasswordHasher();

        public UserAccountAdminTests()
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

        private UserAccountAdmin CreateService(AppDbContext db)
        {
            var auditEvents = new AuditEventWriter(db, _tenant, _tenant, _user, _clock, _audit);
            return new UserAccountAdmin(
                db, new AuthenticationService(db, _hasher, _clock, auditEvents), auditEvents, _user, _clock);
        }

        // ------------------------------------------------------------------ fixture

        private int AddEmployee(string employeeNo, string firstNameEn = "Ahmed", EmployeeStatus status = EmployeeStatus.Active)
        {
            using var db = CreateContext();
            var employee = new Employee
            {
                EmployeeNo = employeeNo,
                FirstNameAr = "أحمد",
                FatherNameAr = "سالم",
                GrandfatherNameAr = "علي",
                FamilyNameAr = "الحسن",
                FirstNameEn = firstNameEn,
                FatherNameEn = "Salem",
                GrandfatherNameEn = "Ali",
                FamilyNameEn = "Al-Hasan",
                Gender = Gender.Male,
                DateOfBirth = new DateTime(1990, 1, 1),
                NationalityLookupId = 1,
                Status = status,
            };

            db.Employees.Add(employee);
            db.SaveChanges();
            return employee.Id;
        }

        private int AddParent(string fileNo)
        {
            using var db = CreateContext();
            var parent = new Parent
            {
                ParentFileNo = fileNo,
                NameAr = "سالم الحسن",
                NameEn = "Salem Al-Hasan",
                PrimaryMobile = "0590000000",
            };

            db.Parents.Add(parent);
            db.SaveChanges();
            return parent.Id;
        }

        /// <summary>An account holding a role that carries SYS/Roles/Configure — the permission every refusal here is about.</summary>
        private async Task<int> AddAdministratorAsync(string userName)
        {
            using var db = CreateContext();
            var account = new UserAccount { UserName = userName, AccountType = AccountType.Staff, IsActive = true };

            // sec.Permission is product data keyed on the triple, so the second administrator shares
            // the first one's row rather than inserting a duplicate of it.
            var permission = await db.Permissions.SingleOrDefaultAsync(p =>
                p.ModuleCode == SecurityAdmin.Administration.ModuleCode
                && p.ScreenCode == SecurityAdmin.Administration.ScreenCode
                && p.Action == SecurityAdmin.Administration.Action);
            if (permission == null)
            {
                permission = new Permission
                {
                    ModuleCode = SecurityAdmin.Administration.ModuleCode,
                    ScreenCode = SecurityAdmin.Administration.ScreenCode,
                    Action = SecurityAdmin.Administration.Action,
                };
                db.Permissions.Add(permission);
            }

            var role = new Role { Code = $"ADMIN-{userName}", Name = new LocalizedName("مدير", "Administrator"), IsActive = true };

            db.UserAccounts.Add(account);
            db.Roles.Add(role);
            await db.SaveChangesAsync();

            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            db.RoleAssignments.Add(new RoleAssignment { UserAccountId = account.Id, RoleId = role.Id, IsActive = true });
            await db.SaveChangesAsync();
            return account.Id;
        }

        // ------------------------------------------------------------------ provisioning

        [Fact]
        [BusinessRule("BR-SEC-005")]
        public async Task Provisioning_creates_the_account_and_returns_a_password_that_must_be_changed()
        {
            var employeeId = AddEmployee("1042");

            ProvisionedAccount provisioned;
            using (var db = CreateContext())
            {
                provisioned = await CreateService(db).ProvisionAsync(
                    new NewUserAccount(ProvisionableAccountType.Staff, employeeId, "emp-1042"));
            }

            Assert.Equal("emp-1042", provisioned.UserName);
            Assert.NotEmpty(provisioned.TemporaryPassword);

            using var check = CreateContext();
            var account = await check.UserAccounts.SingleAsync(a => a.Id == provisioned.UserAccountId);
            Assert.Equal(AccountType.Staff, account.AccountType);
            Assert.Equal(employeeId, account.PersonId);
            Assert.True(account.IsActive);
            Assert.True(account.MustChangePassword);

            // Stored as a hash, not as the value that was shown: nothing can read it back.
            Assert.NotNull(account.PasswordHash);
            Assert.DoesNotContain(provisioned.TemporaryPassword, account.PasswordHash);
            Assert.True(_hasher.Verify(account.PasswordHash!, provisioned.TemporaryPassword));
        }

        [Fact]
        [BusinessRule("BR-GLB-002")]
        public async Task Provisioning_links_the_person_so_both_sides_agree_who_this_login_is()
        {
            var employeeId = AddEmployee("1042");

            using (var db = CreateContext())
            {
                await CreateService(db).ProvisionAsync(
                    new NewUserAccount(ProvisionableAccountType.Staff, employeeId, "emp-1042"));
            }

            using var check = CreateContext();
            var employee = await check.Employees.SingleAsync(e => e.Id == employeeId);
            var account = await check.UserAccounts.SingleAsync(a => a.UserName == "emp-1042");
            Assert.Equal(account.Id, employee.UserAccountId);
            Assert.Equal(employee.Id, account.PersonId);
        }

        [Fact]
        [BusinessRule("BR-GLB-002")]
        public async Task A_person_who_already_has_an_account_is_refused_a_second()
        {
            var employeeId = AddEmployee("1042");
            using (var db = CreateContext())
            {
                await CreateService(db).ProvisionAsync(
                    new NewUserAccount(ProvisionableAccountType.Staff, employeeId, "emp-1042"));
            }

            using var second = CreateContext();
            await Assert.ThrowsAsync<PersonAlreadyHasAccountException>(
                () => CreateService(second).ProvisionAsync(
                    new NewUserAccount(ProvisionableAccountType.Staff, employeeId, "a.hasan")));
        }

        [Fact]
        public async Task A_user_name_the_product_will_not_accept_is_refused_before_anything_is_written()
        {
            var employeeId = AddEmployee("1042");

            using var db = CreateContext();
            await Assert.ThrowsAsync<InvalidUserNameException>(
                () => CreateService(db).ProvisionAsync(
                    new NewUserAccount(ProvisionableAccountType.Staff, employeeId, "أحمد")));

            Assert.Empty(await db.UserAccounts.IgnoreQueryFilters().ToListAsync());
        }

        [Fact]
        public async Task A_name_a_deactivated_account_still_holds_is_taken()
        {
            using (var seed = CreateContext())
            {
                seed.UserAccounts.Add(new UserAccount
                {
                    UserName = "emp-1042",
                    AccountType = AccountType.Staff,
                    IsActive = false,
                });
                await seed.SaveChangesAsync();
            }

            var employeeId = AddEmployee("1042");

            using var db = CreateContext();
            var ex = await Assert.ThrowsAsync<DuplicateUserNameException>(
                () => CreateService(db).ProvisionAsync(
                    new NewUserAccount(ProvisionableAccountType.Staff, employeeId, "EMP-1042")));
            Assert.Equal("emp-1042", ex.UserName);
        }

        [Fact]
        [BusinessRule("BR-GLB-002")]
        public async Task A_parent_account_links_the_parent_row_the_portal_reads()
        {
            var parentId = AddParent("PAR-77");

            using (var db = CreateContext())
            {
                await CreateService(db).ProvisionAsync(
                    new NewUserAccount(ProvisionableAccountType.Parent, parentId, "par-77"));
            }

            using var check = CreateContext();
            var parent = await check.Parents.SingleAsync(p => p.Id == parentId);
            var account = await check.UserAccounts.SingleAsync(a => a.UserName == "par-77");
            Assert.Equal(AccountType.Parent, account.AccountType);
            Assert.Equal(account.Id, parent.UserAccountId);
        }

        // ------------------------------------------------------------------ the picker

        [Fact]
        [BusinessRule("BR-SYS-001")]
        public async Task The_picker_offers_people_without_an_account_and_proposes_their_user_name()
        {
            var withoutAccount = AddEmployee("1042", "Ahmed");
            var terminated = AddEmployee("1043", "Khaled", EmployeeStatus.Terminated);
            var withAccount = AddEmployee("1044", "Mona");

            using (var db = CreateContext())
            {
                await CreateService(db).ProvisionAsync(
                    new NewUserAccount(ProvisionableAccountType.Staff, withAccount, "emp-1044"));
            }

            using var read = CreateContext();
            var offered = await CreateService(read).ListProvisionableAsync(ProvisionableAccountType.Staff);

            Assert.Contains(offered, p => p.PersonId == withoutAccount && p.SuggestedUserName == "emp-1042");
            Assert.DoesNotContain(offered, p => p.PersonId == withAccount);

            // An offboarded employee is not somebody to be given a new login.
            Assert.DoesNotContain(offered, p => p.PersonId == terminated);
        }

        [Fact]
        public async Task The_picker_searches_by_reference_number_as_well_as_by_name()
        {
            AddEmployee("1042", "Ahmed");
            AddEmployee("2311", "Mona");

            using var db = CreateContext();
            var service = CreateService(db);

            var byNumber = await service.ListProvisionableAsync(ProvisionableAccountType.Staff, "2311");
            Assert.Single(byNumber);
            Assert.Equal("2311", byNumber[0].Reference);

            var byName = await service.ListProvisionableAsync(ProvisionableAccountType.Staff, "ahmed");
            Assert.Single(byName);
            Assert.Equal("1042", byName[0].Reference);
        }

        // ------------------------------------------------------------------ lifecycle refusals

        [Fact]
        public async Task Nobody_deactivates_the_account_they_are_signed_in_with()
        {
            var adminId = await AddAdministratorAsync("admin");
            var otherId = await AddAdministratorAsync("deputy");
            _user.UserId = adminId;

            using var db = CreateContext();
            await Assert.ThrowsAsync<SelfAccountDeactivationException>(
                () => CreateService(db).DeactivateAsync(adminId, "leaving"));

            // The colleague who inherits the job can still do it.
            await CreateService(db).DeactivateAsync(otherId, "left the school");
            Assert.False((await db.UserAccounts.IgnoreQueryFilters().SingleAsync(a => a.Id == otherId)).IsActive);
        }

        [Fact]
        public async Task Deactivating_the_last_permission_administrator_is_refused()
        {
            var adminId = await AddAdministratorAsync("admin");
            _user.UserId = 999;

            using var db = CreateContext();
            await Assert.ThrowsAsync<LastPermissionAdministratorException>(
                () => CreateService(db).DeactivateAsync(adminId, "left the school"));

            Assert.True((await db.UserAccounts.IgnoreQueryFilters().SingleAsync(a => a.Id == adminId)).IsActive);
        }

        [Fact]
        [BusinessRule("BR-SEC-004")]
        public async Task Deactivating_ends_the_live_sessions_rather_than_waiting_for_a_cookie_to_expire()
        {
            var adminId = await AddAdministratorAsync("admin");
            var leaverId = await AddAdministratorAsync("deputy");
            _user.UserId = adminId;

            using (var seed = CreateContext())
            {
                seed.UserSessions.Add(new UserSession
                {
                    UserAccountId = leaverId,
                    LastActivityAtUtc = _clock.UtcNow,
                    ExpiresAtUtc = _clock.UtcNow.AddHours(12),
                });
                await seed.SaveChangesAsync();
            }

            using var db = CreateContext();
            await CreateService(db).DeactivateAsync(leaverId, "left the school");

            var session = await db.UserSessions.SingleAsync(s => s.UserAccountId == leaverId);
            Assert.NotNull(session.RevokedAtUtc);
            Assert.Equal("left the school", session.RevokedReason);
        }

        [Fact]
        [BusinessRule("BR-SEC-005")]
        public async Task A_deactivated_account_is_not_given_a_new_password()
        {
            var employeeId = AddEmployee("1042");
            int accountId;
            using (var db = CreateContext())
            {
                accountId = (await CreateService(db).ProvisionAsync(
                    new NewUserAccount(ProvisionableAccountType.Staff, employeeId, "emp-1042"))).UserAccountId;
            }

            using (var db = CreateContext())
            {
                _user.UserId = 999;
                await CreateService(db).DeactivateAsync(accountId, "left the school");
            }

            using var check = CreateContext();
            await Assert.ThrowsAsync<InactiveAccountException>(
                () => CreateService(check).ResetPasswordAsync(accountId));
        }

        [Fact]
        [BusinessRule("BR-SEC-002")]
        public async Task Unlocking_clears_the_lockout_without_touching_the_password()
        {
            var employeeId = AddEmployee("1042");
            ProvisionedAccount provisioned;
            using (var db = CreateContext())
            {
                provisioned = await CreateService(db).ProvisionAsync(
                    new NewUserAccount(ProvisionableAccountType.Staff, employeeId, "emp-1042"));
            }

            using (var db = CreateContext())
            {
                var account = await db.UserAccounts.SingleAsync(a => a.Id == provisioned.UserAccountId);
                account.AccessFailedCount = 5;
                account.LockedOutUntilUtc = _clock.UtcNow.AddMinutes(15);
                await db.SaveChangesAsync();
            }

            using (var db = CreateContext())
            {
                await CreateService(db).UnlockAsync(provisioned.UserAccountId);
            }

            using var check = CreateContext();
            var unlocked = await check.UserAccounts.SingleAsync(a => a.Id == provisioned.UserAccountId);
            Assert.Equal(0, unlocked.AccessFailedCount);
            Assert.Null(unlocked.LockedOutUntilUtc);
            Assert.True(_hasher.Verify(unlocked.PasswordHash!, provisioned.TemporaryPassword));
        }

        // ------------------------------------------------------------------ the directory

        [Fact]
        [BusinessRule("BR-SEC-022")]
        public async Task The_directory_shows_deactivated_accounts_and_names_the_person_behind_each()
        {
            var employeeId = AddEmployee("1042");
            int accountId;
            using (var db = CreateContext())
            {
                accountId = (await CreateService(db).ProvisionAsync(
                    new NewUserAccount(ProvisionableAccountType.Staff, employeeId, "emp-1042"))).UserAccountId;
            }

            using (var db = CreateContext())
            {
                _user.UserId = 999;
                await CreateService(db).DeactivateAsync(accountId, "left the school");
            }

            using var read = CreateContext();
            var rows = await CreateService(read).ListAsync(new UserAccountFilter());
            var row = Assert.Single(rows);

            Assert.False(row.IsActive);
            Assert.Equal("1042", row.PersonReference);
            Assert.Equal("Ahmed Salem Al-Hasan", row.PersonNameEn);
            Assert.Null(row.LastSignInAtUtc);

            // Provisioned today, so not yet dormant — BR-SEC-022 measures a never-used account from
            // the day it was created, not from the epoch.
            Assert.False(row.IsDormant);

            var inactiveOnly = await CreateService(read).ListAsync(
                new UserAccountFilter { Status = AccountStatusFilter.Inactive });
            Assert.Single(inactiveOnly);
        }
    }
}
