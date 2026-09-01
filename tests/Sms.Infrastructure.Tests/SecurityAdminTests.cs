using System;
using System.Collections.Generic;
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
    /// Module 36's role designer (doc 06 §4) over a real Sqlite-backed <see cref="AppDbContext"/> —
    /// the screen <c>RoleTemplateSeedContributor</c> shipped 21 empty roles for and named as
    /// deferred.
    /// <para>
    /// Most of what follows is about one refusal. A permission system that can be edited into a
    /// state where nobody may edit it has no way back from inside the product: the screen that would
    /// undo the mistake is the screen the mistake just closed. Every path that could reach that state
    /// — narrowing a role, deactivating one, revoking an assignment — is tested here, and so is the
    /// mirror case, because a guard that refuses everything is as broken as one that refuses nothing.
    /// </para>
    /// </summary>
    public sealed class SecurityAdminTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 8, 22, 9, 0, 0, DateTimeKind.Utc);
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

        public SecurityAdminTests()
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

        private static readonly PermissionKey Admin = SecurityAdmin.Administration;

        private static readonly PermissionKey ViewStudents = new(
            ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, ActionVerb.View);

        private static readonly PermissionKey ViewRoles = new(
            ScreenCatalog.Modules.SystemAdministration, ScreenCatalog.SystemAdministration.Roles, ActionVerb.View);

        // ------------------------------------------------------------------ fixture

        /// <summary>An account, a role, and the assignment binding them. Returns (userId, roleId).</summary>
        private async Task<(int UserId, int RoleId)> ProvisionAsync(
            string userName, string roleCode, params PermissionKey[] grants)
        {
            using var db = CreateContext();
            var user = new UserAccount { UserName = userName, AccountType = AccountType.Staff, IsActive = true };
            var role = new Role
            {
                Code = roleCode,
                Name = new LocalizedName(roleCode, roleCode),
                IsActive = true,
            };
            db.UserAccounts.Add(user);
            db.Roles.Add(role);
            await db.SaveChangesAsync();

            foreach (var grant in grants)
            {
                // sec.Permission is product data with a unique (module, screen, action) index, so a
                // second role granted the same thing points at the same row rather than adding one.
                var permission = await db.Permissions.SingleOrDefaultAsync(p =>
                    p.ModuleCode == grant.ModuleCode && p.ScreenCode == grant.ScreenCode && p.Action == grant.Action);
                if (permission == null)
                {
                    permission = new Permission { ModuleCode = grant.ModuleCode, ScreenCode = grant.ScreenCode, Action = grant.Action };
                    db.Permissions.Add(permission);
                    await db.SaveChangesAsync();
                }

                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            }

            db.RoleAssignments.Add(new RoleAssignment { UserAccountId = user.Id, RoleId = role.Id, IsActive = true });
            await db.SaveChangesAsync();
            return (user.Id, role.Id);
        }

        private SecurityAdmin Admin_(AppDbContext db) => new(db);

        // ------------------------------------------------------------------ roles and grants

        [Fact]
        public async Task A_role_reports_what_it_grants_and_who_holds_it()
        {
            var (_, roleId) = await ProvisionAsync("amina", "REGISTRAR", ViewStudents, ViewRoles);

            using var db = CreateContext();
            var roles = await Admin_(db).ListRolesAsync();

            var role = Assert.Single(roles, r => r.Id == roleId);
            Assert.Equal(2, role.GrantCount);
            Assert.Equal(1, role.HolderCount);
            Assert.False(role.CanAdministerPermissions);
        }

        /// <summary>
        /// The designer offers the whole catalogue, not only what has been catalogued into
        /// <c>sec.Permission</c> — otherwise a screen added in a release would be ungrantable until
        /// the seeder next ran.
        /// </summary>
        [Fact]
        public async Task The_designer_offers_every_screen_the_catalogue_defines()
        {
            var (_, roleId) = await ProvisionAsync("amina", "REGISTRAR", ViewStudents);

            using var db = CreateContext();
            var detail = await Admin_(db).GetRoleAsync(roleId);

            Assert.Equal(ScreenCatalog.Screens.Count, detail.Screens.Count);

            var students = Assert.Single(detail.Screens, s =>
                s.ModuleCode == ViewStudents.ModuleCode && s.ScreenCode == ViewStudents.ScreenCode);
            Assert.Contains(ActionVerb.View, students.GrantedVerbs);
            Assert.Contains(ActionVerb.Create, students.AvailableVerbs);
            Assert.DoesNotContain(ActionVerb.Create, students.GrantedVerbs);
        }

        [Fact]
        public async Task Saving_the_grid_grants_what_is_ticked_and_revokes_what_is_not()
        {
            var (_, roleId) = await ProvisionAsync("amina", "REGISTRAR", ViewStudents);

            using (var db = CreateContext())
            {
                await Admin_(db).SetRolePermissionsAsync(roleId, new[]
                {
                    new PermissionKey(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, ActionVerb.Create),
                    new PermissionKey(ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.Directory, ActionVerb.View),
                });
            }

            using var check = CreateContext();
            var detail = await Admin_(check).GetRoleAsync(roleId);
            var granted = detail.Screens
                .SelectMany(s => s.GrantedVerbs.Select(v => (s.ModuleCode, s.ScreenCode, v)))
                .ToList();

            Assert.Equal(2, granted.Count);
            Assert.Contains((ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, ActionVerb.Create), granted);
            Assert.Contains((ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.Directory, ActionVerb.View), granted);
            // The View on Students was ticked before and is not in the post, so it is gone.
            Assert.DoesNotContain((ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, ActionVerb.View), granted);
        }

        /// <summary>
        /// A triple the catalogue does not define would become a <c>sec.Permission</c> row no screen
        /// ever checks — access that reads as granted on this screen and does nothing anywhere else.
        /// </summary>
        [Fact]
        public async Task A_permission_no_screen_checks_is_refused()
        {
            var (_, roleId) = await ProvisionAsync("amina", "REGISTRAR", ViewStudents);

            using var db = CreateContext();
            await Assert.ThrowsAsync<UncataloguedPermissionException>(() =>
                Admin_(db).SetRolePermissionsAsync(roleId, new[]
                {
                    new PermissionKey("PAYROLL", "Runs", ActionVerb.Post),
                }));
        }

        /// <summary>A grant the seeder has not catalogued yet creates its row rather than failing.</summary>
        [Fact]
        public async Task A_catalogued_screen_with_no_permission_row_yet_gets_one()
        {
            var (_, roleId) = await ProvisionAsync("amina", "REGISTRAR");

            using (var db = CreateContext())
            {
                Assert.Empty(await db.Permissions.ToListAsync());
                await Admin_(db).SetRolePermissionsAsync(roleId, new[] { ViewStudents });
            }

            using var check = CreateContext();
            var permission = Assert.Single(await check.Permissions.ToListAsync());
            Assert.Equal(ViewStudents.ModuleCode, permission.ModuleCode);
            Assert.Equal(ViewStudents.ScreenCode, permission.ScreenCode);
            Assert.Equal(ViewStudents.Action, permission.Action);
        }

        // ------------------------------------------------------------------ the lockout guard

        [Fact]
        public async Task Narrowing_the_only_administering_role_is_refused()
        {
            var (_, roleId) = await ProvisionAsync("root", "SYSADMIN", Admin);

            using var db = CreateContext();
            var ex = await Assert.ThrowsAsync<LastPermissionAdministratorException>(() =>
                Admin_(db).SetRolePermissionsAsync(roleId, new[] { ViewStudents }));

            Assert.Contains("SYSADMIN", ex.Message);
        }

        /// <summary>The mirror case — a guard that refuses everything is as broken as one that refuses nothing.</summary>
        [Fact]
        public async Task Narrowing_it_is_allowed_once_somebody_else_can_administer()
        {
            var (_, sysadmin) = await ProvisionAsync("root", "SYSADMIN", Admin);
            await ProvisionAsync("deputy", "SECURITY_OFFICER", Admin);

            using (var db = CreateContext())
            {
                await Admin_(db).SetRolePermissionsAsync(sysadmin, new[] { ViewStudents });
            }

            using var check = CreateContext();
            var role = Assert.Single(await check.Roles.Where(r => r.Id == sysadmin).ToListAsync());
            Assert.False((await Admin_(check).ListRolesAsync()).Single(r => r.Id == role.Id).CanAdministerPermissions);
        }

        /// <summary>Keeping the administration grant while changing everything else must not trip the guard.</summary>
        [Fact]
        public async Task Editing_the_only_administering_role_is_allowed_while_it_keeps_administering()
        {
            var (_, roleId) = await ProvisionAsync("root", "SYSADMIN", Admin, ViewStudents);

            using (var db = CreateContext())
            {
                await Admin_(db).SetRolePermissionsAsync(roleId, new[] { Admin, ViewRoles });
            }

            using var check = CreateContext();
            var role = (await Admin_(check).ListRolesAsync()).Single(r => r.Id == roleId);
            Assert.True(role.CanAdministerPermissions);
            Assert.Equal(2, role.GrantCount);
        }

        /// <summary>
        /// The rule is "do not remove the last administrator", not "there must always be one".
        /// On a database where nobody administers yet, refusing every edit would block the one that
        /// creates the first administrator — the same lockout wearing the opposite sign.
        /// </summary>
        [Fact]
        public async Task Where_nobody_administers_yet_the_guard_does_not_block_bootstrapping()
        {
            var (_, roleId) = await ProvisionAsync("clerk", "REGISTRAR", ViewStudents);

            using (var db = CreateContext())
            {
                await Admin_(db).SetRolePermissionsAsync(roleId, new[] { Admin });
            }

            using var check = CreateContext();
            Assert.True((await Admin_(check).ListRolesAsync()).Single(r => r.Id == roleId).CanAdministerPermissions);
        }

        [Fact]
        public async Task Deactivating_the_only_administering_role_is_refused()
        {
            var (_, roleId) = await ProvisionAsync("root", "SYSADMIN", Admin);

            using var db = CreateContext();
            await Assert.ThrowsAsync<LastPermissionAdministratorException>(() =>
                Admin_(db).DeactivateRoleAsync(roleId));
        }

        [Fact]
        public async Task Revoking_the_last_administering_assignment_is_refused()
        {
            var (userId, roleId) = await ProvisionAsync("root", "SYSADMIN", Admin);

            using var db = CreateContext();
            await Assert.ThrowsAsync<LastPermissionAdministratorException>(() =>
                Admin_(db).RevokeRoleAsync(userId, roleId));
        }

        [Fact]
        public async Task Revoking_one_of_two_administrators_is_allowed()
        {
            var (firstUser, firstRole) = await ProvisionAsync("root", "SYSADMIN", Admin);
            await ProvisionAsync("deputy", "SECURITY_OFFICER", Admin);

            using (var db = CreateContext())
            {
                await Admin_(db).RevokeRoleAsync(firstUser, firstRole);
            }

            using var check = CreateContext();
            var users = await Admin_(check).ListUserRolesAsync();
            Assert.Empty(users.Single(u => u.UserName == "root").Roles);
            Assert.Single(users.Single(u => u.UserName == "deputy").Roles);
        }

        /// <summary>
        /// A deactivated account cannot administer anything, so it cannot be what keeps the guard
        /// satisfied. Otherwise "the last administrator" could be somebody who can no longer sign in.
        /// </summary>
        [Fact]
        public async Task A_deactivated_account_does_not_count_as_an_administrator()
        {
            var (rootId, rootRole) = await ProvisionAsync("root", "SYSADMIN", Admin);
            var (deputyId, _) = await ProvisionAsync("deputy", "SECURITY_OFFICER", Admin);

            using (var db = CreateContext())
            {
                var deputy = await db.UserAccounts.SingleAsync(u => u.Id == deputyId);
                deputy.IsActive = false;
                await db.SaveChangesAsync();
            }

            using var check = CreateContext();
            await Assert.ThrowsAsync<LastPermissionAdministratorException>(() =>
                Admin_(check).RevokeRoleAsync(rootId, rootRole));
        }

        // ------------------------------------------------------------------ assignments

        [Fact]
        public async Task Assigning_a_role_twice_does_not_create_a_second_assignment()
        {
            var (userId, _) = await ProvisionAsync("root", "SYSADMIN", Admin);
            using var db = CreateContext();
            var role = await Admin_(db).CreateRoleAsync(new RoleDefinition("CASHIER", "أمين صندوق", "Cashier", false, true));

            await Admin_(db).AssignRoleAsync(userId, role.Id);
            await Admin_(db).AssignRoleAsync(userId, role.Id);

            using var check = CreateContext();
            var assignments = await check.RoleAssignments.IgnoreQueryFilters()
                .Where(a => a.UserAccountId == userId && a.RoleId == role.Id)
                .ToListAsync();
            Assert.Single(assignments);
        }

        /// <summary>
        /// Re-granting revives the revoked row rather than inserting a second one — the (user, role)
        /// pair is unique, so an insert would fail at the index instead of doing the obvious thing.
        /// </summary>
        [Fact]
        public async Task Re_granting_a_revoked_role_revives_the_assignment()
        {
            var (rootId, _) = await ProvisionAsync("root", "SYSADMIN", Admin);
            var (clerkId, clerkRole) = await ProvisionAsync("clerk", "REGISTRAR", ViewStudents);
            _ = rootId;

            using (var db = CreateContext())
            {
                await Admin_(db).RevokeRoleAsync(clerkId, clerkRole);
            }

            using (var db = CreateContext())
            {
                await Admin_(db).AssignRoleAsync(clerkId, clerkRole);
            }

            using var check = CreateContext();
            var assignments = await check.RoleAssignments.IgnoreQueryFilters()
                .Where(a => a.UserAccountId == clerkId && a.RoleId == clerkRole)
                .ToListAsync();
            var assignment = Assert.Single(assignments);
            Assert.True(assignment.IsActive);
        }

        [Fact]
        public async Task Users_are_listed_with_the_roles_they_hold()
        {
            await ProvisionAsync("root", "SYSADMIN", Admin);
            await ProvisionAsync("clerk", "REGISTRAR", ViewStudents);

            using var db = CreateContext();
            var users = await Admin_(db).ListUserRolesAsync();

            Assert.Equal(2, users.Count);
            Assert.True(users.Single(u => u.UserName == "root").Roles.Single().CanAdministerPermissions);
            Assert.False(users.Single(u => u.UserName == "clerk").Roles.Single().CanAdministerPermissions);
        }

        [Fact]
        public async Task Searching_narrows_the_account_list()
        {
            await ProvisionAsync("root", "SYSADMIN", Admin);
            await ProvisionAsync("clerk", "REGISTRAR", ViewStudents);

            using var db = CreateContext();
            var users = await Admin_(db).ListUserRolesAsync("cler");

            Assert.Equal("clerk", Assert.Single(users).UserName);
        }

        // ------------------------------------------------------------------ who the account belongs to

        /// <summary>
        /// The screen's own question is "who holds this role", and a column of user names does not
        /// answer it — nobody in a school office knows who <c>emp-1042</c> is. The person's name and
        /// file number come back with the account so the list can be read without translating it.
        /// </summary>
        [Fact]
        public async Task Each_account_is_listed_with_the_person_it_belongs_to()
        {
            AddAccount("emp-1042", AccountType.Staff, AddEmployee("1042", "Ahmed", "أحمد"));

            using var db = CreateContext();
            var row = Assert.Single(await Admin_(db).ListUserRolesAsync());

            Assert.Equal("Ahmed Salem Al-Hasan", row.PersonNameEn);
            Assert.Equal("أحمد سالم الحسن", row.PersonNameAr);
            Assert.Equal("1042", row.PersonReference);
        }

        /// <summary>
        /// What an administrator types is the name on the colleague's door or the number on their
        /// file, in whichever language the screen is being read in. Matching the user name alone
        /// meant the search only worked for somebody who already knew the answer.
        /// </summary>
        [Fact]
        public async Task An_account_is_found_by_the_person_name_in_either_language_or_by_the_file_number()
        {
            AddAccount("emp-1042", AccountType.Staff, AddEmployee("1042", "Ahmed", "أحمد"));
            AddAccount("emp-2311", AccountType.Staff, AddEmployee("2311", "Mona", "منى"));

            using var db = CreateContext();
            var admin = Admin_(db);

            Assert.Equal("emp-2311", Assert.Single(await admin.ListUserRolesAsync("mona")).UserName);
            Assert.Equal("emp-1042", Assert.Single(await admin.ListUserRolesAsync("أحمد")).UserName);
            Assert.Equal("emp-2311", Assert.Single(await admin.ListUserRolesAsync("2311")).UserName);

            // And the user name still finds it, which is what the box did before.
            Assert.Equal("emp-1042", Assert.Single(await admin.ListUserRolesAsync("emp-1042")).UserName);
        }

        /// <summary>
        /// Three registers, three tables, and the account type is what says which one to look in:
        /// employee 7, parent 7 and student 7 are three different people.
        /// </summary>
        [Fact]
        public async Task A_parent_account_is_named_from_the_parent_register_and_not_the_staff_one()
        {
            AddEmployee("1042", "Ahmed", "أحمد");
            AddAccount("par-77", AccountType.Parent, AddParent("77"));

            using var db = CreateContext();
            var row = Assert.Single(await Admin_(db).ListUserRolesAsync());

            Assert.Equal("Salem Al-Hasan", row.PersonNameEn);
            Assert.Equal("77", row.PersonReference);
        }

        /// <summary>
        /// The lookup reads past the soft-active filter. A deactivated parent still holds an account
        /// somebody has come here to deal with, and a list that went anonymous the moment a person
        /// was retired would go blank at the least useful moment (the trap
        /// <c>SoftActiveLookupTests</c> exists for).
        /// </summary>
        [Fact]
        public async Task A_deactivated_person_is_still_named_beside_their_account()
        {
            var parentId = AddParent("77");
            AddAccount("par-77", AccountType.Parent, parentId);

            using (var db = CreateContext())
            {
                var parent = await db.Parents.SingleAsync(p => p.Id == parentId);
                parent.IsActive = false;
                await db.SaveChangesAsync();
            }

            using var check = CreateContext();
            var row = Assert.Single(await Admin_(check).ListUserRolesAsync());

            Assert.Equal("Salem Al-Hasan", row.PersonNameEn);
            Assert.Equal("77", row.PersonReference);
        }

        /// <summary>
        /// An integration account belongs to nobody by design (doc 06 §2), and so does any account
        /// written before this product could link one. Both are listed as themselves rather than
        /// throwing the whole screen away.
        /// </summary>
        [Fact]
        public async Task An_account_that_belongs_to_no_person_is_still_listed()
        {
            await ProvisionAsync("root", "SYSADMIN", Admin);

            using var db = CreateContext();
            var row = Assert.Single(await Admin_(db).ListUserRolesAsync());

            Assert.Equal("root", row.UserName);
            Assert.Null(row.PersonNameEn);
            Assert.Null(row.PersonNameAr);
            Assert.Null(row.PersonReference);
        }

        /// <summary>
        /// The everyday list answers "who can reach what today", so a closed account stays out of it.
        /// But there is no delete in this product (BR-GLB-005) and no other screen that shows one, so
        /// asking must bring it back — read past the soft-active filter, with the school scope
        /// restated, or the request would also reach the next school's accounts.
        /// </summary>
        [Fact]
        [BusinessRule("BR-GLB-005")]
        public async Task A_deactivated_account_is_out_of_the_list_until_it_is_asked_for()
        {
            await ProvisionAsync("root", "SYSADMIN", Admin);
            var (clerkId, _) = await ProvisionAsync("clerk", "REGISTRAR", ViewStudents);

            using (var db = CreateContext())
            {
                var clerk = await db.UserAccounts.SingleAsync(u => u.Id == clerkId);
                clerk.IsActive = false;
                await db.SaveChangesAsync();
            }

            using var check = CreateContext();
            var admin = Admin_(check);

            Assert.Equal("root", Assert.Single(await admin.ListUserRolesAsync()).UserName);

            var withInactive = await admin.ListUserRolesAsync(includeInactive: true);
            Assert.Equal(2, withInactive.Count);
            Assert.False(withInactive.Single(u => u.UserName == "clerk").IsActive);

            // The roles it still holds come with it: what a closed account could reach is the
            // question somebody re-reading it has come to answer.
            Assert.Single(withInactive.Single(u => u.UserName == "clerk").Roles);
        }

        /// <summary>The search narrows the same list whichever accounts are in it.</summary>
        [Fact]
        public async Task Searching_still_narrows_the_list_when_deactivated_accounts_are_shown()
        {
            await ProvisionAsync("root", "SYSADMIN", Admin);
            var (clerkId, _) = await ProvisionAsync("clerk", "REGISTRAR", ViewStudents);

            using (var db = CreateContext())
            {
                var clerk = await db.UserAccounts.SingleAsync(u => u.Id == clerkId);
                clerk.IsActive = false;
                await db.SaveChangesAsync();
            }

            using var check = CreateContext();
            var found = await Admin_(check).ListUserRolesAsync("cler", includeInactive: true);

            Assert.Equal("clerk", Assert.Single(found).UserName);
        }

        // ------------------------------------------------------------------ people fixture

        private int AddEmployee(string employeeNo, string firstNameEn, string firstNameAr)
        {
            using var db = CreateContext();
            var employee = new Employee
            {
                EmployeeNo = employeeNo,
                FirstNameAr = firstNameAr,
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
                Status = EmployeeStatus.Active,
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

        private int AddAccount(string userName, AccountType accountType, int personId)
        {
            using var db = CreateContext();
            var account = new UserAccount
            {
                UserName = userName,
                AccountType = accountType,
                PersonId = personId,
                IsActive = true,
            };

            db.UserAccounts.Add(account);
            db.SaveChanges();
            return account.Id;
        }

        // ------------------------------------------------------------------ role lifecycle

        [Fact]
        public async Task A_role_code_is_upper_cased_and_may_not_repeat()
        {
            using var db = CreateContext();
            var admin = Admin_(db);

            var role = await admin.CreateRoleAsync(new RoleDefinition(" library_clerk ", "أمين مكتبة", "Library clerk", false, false));

            Assert.Equal("LIBRARY_CLERK", role.Code);
            await Assert.ThrowsAsync<DuplicateRoleCodeException>(() =>
                admin.CreateRoleAsync(new RoleDefinition("library_clerk", "آخر", "Other", false, false)));
        }

        /// <summary>
        /// The code is the key the seeder, the ERP permission bridge and every grant use, so editing
        /// a role must not move it. The form posts the code back; the service ignores it.
        /// </summary>
        [Fact]
        public async Task Editing_a_role_changes_its_name_and_policy_but_never_its_code()
        {
            using var db = CreateContext();
            var admin = Admin_(db);
            var role = await admin.CreateRoleAsync(new RoleDefinition("CASHIER", "أمين صندوق", "Cashier", false, false));

            await admin.UpdateRoleAsync(role.Id, new RoleDefinition("SOMETHING_ELSE", "الصندوق", "Till operator", true, true));

            using var check = CreateContext();
            var updated = (await Admin_(check).ListRolesAsync()).Single(r => r.Id == role.Id);
            Assert.Equal("CASHIER", updated.Code);
            Assert.Equal("Till operator", updated.NameEn);
            Assert.True(updated.RequireTwoFactor);
            Assert.True(updated.EnforceSingleSession);
        }

        [Fact]
        public async Task A_deactivated_role_is_hidden_by_default_and_can_be_brought_back()
        {
            await ProvisionAsync("root", "SYSADMIN", Admin);
            var (_, clerkRole) = await ProvisionAsync("clerk", "REGISTRAR", ViewStudents);

            using (var db = CreateContext())
            {
                await Admin_(db).DeactivateRoleAsync(clerkRole);
            }

            using (var db = CreateContext())
            {
                Assert.DoesNotContain(await Admin_(db).ListRolesAsync(), r => r.Id == clerkRole);
                Assert.Contains(await Admin_(db).ListRolesAsync(includeInactive: true), r => r.Id == clerkRole);
                await Admin_(db).ReactivateRoleAsync(clerkRole);
            }

            using var check = CreateContext();
            Assert.Contains(await Admin_(check).ListRolesAsync(), r => r.Id == clerkRole);
        }

        [Fact]
        public async Task An_unknown_role_is_reported_rather_than_returned_empty()
        {
            using var db = CreateContext();

            await Assert.ThrowsAsync<InvalidOperationException>(() => Admin_(db).GetRoleAsync(9999));
        }

        // ------------------------------------------------------------------ the seam to enforcement

        /// <summary>
        /// The whole point: what this screen writes is what <see cref="PermissionService"/> reads.
        /// Granting through the designer must actually open the screen, and revoking must close it —
        /// tested through the real evaluator rather than by re-reading the rows just written.
        /// </summary>
        [Fact]
        [BusinessRule("BR-GLB-070")]
        public async Task What_the_designer_grants_is_what_the_screen_guard_sees()
        {
            var (userId, roleId) = await ProvisionAsync("clerk", "REGISTRAR");
            await ProvisionAsync("root", "SYSADMIN", Admin);
            _user.UserId = userId;

            using (var before = CreateContext())
            {
                Assert.False(await new PermissionService(before, _user)
                    .HasPermissionAsync(ViewStudents.ModuleCode, ViewStudents.ScreenCode, ViewStudents.Action));
            }

            using (var db = CreateContext())
            {
                await Admin_(db).SetRolePermissionsAsync(roleId, new[] { ViewStudents });
            }

            using (var after = CreateContext())
            {
                Assert.True(await new PermissionService(after, _user)
                    .HasPermissionAsync(ViewStudents.ModuleCode, ViewStudents.ScreenCode, ViewStudents.Action));
            }

            using (var db = CreateContext())
            {
                await Admin_(db).SetRolePermissionsAsync(roleId, Array.Empty<PermissionKey>());
            }

            using var revoked = CreateContext();
            Assert.False(await new PermissionService(revoked, _user)
                .HasPermissionAsync(ViewStudents.ModuleCode, ViewStudents.ScreenCode, ViewStudents.Action));
        }
    }
}
