using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Security;
using Sms.Infrastructure.Seeding;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// The role matrix is a security decision written as data, so it is tested as
    /// one: not "does the seeder insert rows" but "can the cashier reach the fee
    /// structure". A default that is wrong in the direction of generosity is the
    /// failure mode worth catching, and it is invisible in a row count.
    /// </summary>
    public sealed class PermissionSeedContributorTests : IDisposable
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
            public DateTime UtcNow => new(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);
        }

        private readonly SqliteConnection _connection;
        private readonly User _user = new();

        public PermissionSeedContributorTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
            => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, new Tenant(), _user, new Clock());

        private async Task SeedAsync()
        {
            using var db = CreateContext();
            await new RoleTemplateSeedContributor(db).SeedAsync();
            await new PermissionSeedContributor(db).SeedAsync();
        }

        /// <summary>Signs a fresh account into <paramref name="roleCode"/> and returns a service that answers for it.</summary>
        private async Task<(AppDbContext Db, PermissionService Service)> AsAsync(string roleCode)
        {
            var db = CreateContext();
            var role = await db.Roles.SingleAsync(r => r.Code == roleCode);
            var account = new UserAccount { UserName = $"{roleCode.ToLowerInvariant()}.test", AccountType = AccountType.Staff };
            db.UserAccounts.Add(account);
            await db.SaveChangesAsync();
            db.RoleAssignments.Add(new RoleAssignment { UserAccountId = account.Id, RoleId = role.Id });
            await db.SaveChangesAsync();

            _user.UserId = account.Id;
            return (db, new PermissionService(db, _user));
        }

        [Fact]
        [BusinessRule("BR-GLB-070")]
        public async Task Every_catalogued_permission_is_created_once_and_re_running_adds_none()
        {
            await SeedAsync();
            using var db = CreateContext();
            var expected = ScreenCatalog.Permissions().Count();
            Assert.Equal(expected, await db.Permissions.CountAsync());

            await SeedAsync();
            using var again = CreateContext();
            Assert.Equal(expected, await again.Permissions.CountAsync());
        }

        [Fact]
        [BusinessRule("BR-GLB-070")]
        public async Task The_system_administrator_holds_every_permission_except_the_portal_audience()
        {
            await SeedAsync();
            var (db, service) = await AsAsync("SYSADMIN");
            using var _ = db;

            foreach (var (module, screen, action) in ScreenCatalog.Permissions().Where(p => p.ModuleCode != ScreenCatalog.Modules.Portal))
            {
                Assert.True(await service.HasPermissionAsync(module, screen, action),
                    $"SYSADMIN is missing {module}/{screen}/{action}, and nobody else can grant it.");
            }
        }

        [Fact]
        [BusinessRule("BR-GLB-070")]
        public async Task A_cashier_takes_money_but_cannot_price_or_forgive_it()
        {
            await SeedAsync();
            var (db, service) = await AsAsync("CASHIER");
            using var _ = db;

            Assert.True(await service.HasPermissionAsync(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Cashier, ActionVerb.Create));
            Assert.True(await service.HasPermissionAsync(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Till, ActionVerb.Post));
            Assert.True(await service.HasPermissionAsync(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Charges, ActionVerb.View));

            // The three that separate a cashier from a finance manager.
            Assert.False(await service.HasPermissionAsync(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Structure, ActionVerb.Edit));
            Assert.False(await service.HasPermissionAsync(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Categories, ActionVerb.Edit));
            Assert.False(await service.HasPermissionAsync(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Grants, ActionVerb.Approve));

            // And the seam to the ledger is not a cashier's to operate.
            Assert.False(await service.HasPermissionAsync(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.GlExport, ActionVerb.Post));
        }

        [Fact]
        [BusinessRule("BR-GLB-072")]
        public async Task Only_the_roles_that_case_manage_a_family_see_the_social_profile()
        {
            await SeedAsync();

            var (registrarDb, registrar) = await AsAsync("REGISTRAR");
            using (registrarDb)
            {
                Assert.True(await registrar.HasPermissionAsync(ScreenCatalog.Modules.Students, ScreenCatalog.Students.SocialProfile, ActionVerb.View));
            }

            // A teacher holds the student file and not the family's circumstances — the whole reason
            // the social profile is a separate screen code rather than a section of the file.
            var (teacherDb, teacher) = await AsAsync("TEACHER");
            using (teacherDb)
            {
                Assert.True(await teacher.HasPermissionAsync(ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, ActionVerb.View));
                Assert.False(await teacher.HasPermissionAsync(ScreenCatalog.Modules.Students, ScreenCatalog.Students.SocialProfile, ActionVerb.View));
            }
        }

        [Fact]
        [BusinessRule("BR-GLB-070")]
        public async Task An_auditor_reads_everything_and_writes_nothing()
        {
            await SeedAsync();
            var (db, service) = await AsAsync("AUDITOR");
            using var _ = db;

            Assert.True(await service.HasPermissionAsync(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Charges, ActionVerb.View));
            Assert.True(await service.HasPermissionAsync(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Till, ActionVerb.View));

            var writes = new[] { ActionVerb.Create, ActionVerb.Edit, ActionVerb.Deactivate, ActionVerb.Approve, ActionVerb.Post, ActionVerb.Configure, ActionVerb.Import };
            foreach (var (module, screen, action) in ScreenCatalog.Permissions().Where(p => writes.Contains(p.Action)))
            {
                Assert.False(await service.HasPermissionAsync(module, screen, action),
                    $"AUDITOR should be read-only but holds {module}/{screen}/{action}.");
            }
        }

        [Fact]
        [BusinessRule("BR-SEC-010")]
        public async Task A_staff_wildcard_never_reaches_the_portal()
        {
            await SeedAsync();

            foreach (var roleCode in new[] { "SYSADMIN", "PRINCIPAL", "AUDITOR" })
            {
                var (db, service) = await AsAsync(roleCode);
                using var _ = db;
                Assert.False(await service.HasPermissionAsync(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Statement, ActionVerb.View),
                    $"{roleCode} picked up a portal permission from a wildcard.");
            }

            var (parentDb, parent) = await AsAsync("PARENT");
            using (parentDb)
            {
                Assert.True(await parent.HasPermissionAsync(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Statement, ActionVerb.View));
                Assert.False(await parent.HasPermissionAsync(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Charges, ActionVerb.View));
            }

            // A student is in the portal but is not shown what the family owes.
            var (studentDb, student) = await AsAsync("STUDENT");
            using (studentDb)
            {
                Assert.True(await student.HasPermissionAsync(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Home, ActionVerb.View));
                Assert.False(await student.HasPermissionAsync(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Statement, ActionVerb.View));
            }
        }

        [Fact]
        [BusinessRule("BR-GLB-070")]
        public async Task A_hosted_subsystems_grants_are_not_mistaken_for_curation()
        {
            // The bug this pins: the ERP catalogues its own permissions under its own module code
            // and grants them to SYSADMIN. Treating "holds any grant" as "already curated" meant the
            // administrator came out of a real seeding run with the accounting screens and nothing
            // else - locked out of the school system by the thing meant to provision it.
            using (var db = CreateContext())
            {
                await new RoleTemplateSeedContributor(db).SeedAsync();

                var foreign = new Permission { ModuleCode = "ERP", ScreenCode = "Accounting.JournalEntries.Post", Action = ActionVerb.View };
                db.Permissions.Add(foreign);
                await db.SaveChangesAsync();

                var sysadmin = await db.Roles.SingleAsync(r => r.Code == "SYSADMIN");
                db.RolePermissions.Add(new RolePermission { RoleId = sysadmin.Id, PermissionId = foreign.Id });
                await db.SaveChangesAsync();
            }

            using (var db = CreateContext())
            {
                await new PermissionSeedContributor(db).SeedAsync();
            }

            var (verifyDb, service) = await AsAsync("SYSADMIN");
            using var _ = verifyDb;
            Assert.True(await service.HasPermissionAsync(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Structure, ActionVerb.Approve));
            Assert.True(await service.HasPermissionAsync(ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, ActionVerb.View));
        }

        [Fact]
        [BusinessRule("BR-GLB-070")]
        public async Task A_permission_added_after_provisioning_still_reaches_the_system_administrator()
        {
            // The state a school reaches the moment anything grants SYSADMIN anything — the ERP's
            // own permissions do it during the very first seeding run. Without the top-up, "already
            // curated" would then stop every screen shipped afterwards from ever reaching the one
            // role that grants the others: invisible to the whole product, permanently and silently.
            using (var db = CreateContext())
            {
                await new RoleTemplateSeedContributor(db).SeedAsync();
                var sysadmin = await db.Roles.SingleAsync(r => r.Code == "SYSADMIN");
                var one = new Permission { ModuleCode = ScreenCatalog.Modules.Fees, ScreenCode = ScreenCatalog.Fees.Charges, Action = ActionVerb.View };
                db.Permissions.Add(one);
                await db.SaveChangesAsync();
                db.RolePermissions.Add(new RolePermission { RoleId = sysadmin.Id, PermissionId = one.Id });
                await db.SaveChangesAsync();
            }

            await SeedAsync();

            using (var db = CreateContext())
            {
                var sysadmin = await db.Roles.SingleAsync(r => r.Code == "SYSADMIN");
                var everything = await db.Permissions.Where(p => p.ModuleCode != ScreenCatalog.Modules.Portal).CountAsync();
                var held = await db.RolePermissions.CountAsync(rp => rp.RoleId == sysadmin.Id);
                Assert.Equal(everything, held);

                // And still exactly once each — a top-up must not duplicate what is already there.
                var duplicates = await db.RolePermissions
                    .Where(rp => rp.RoleId == sysadmin.Id)
                    .GroupBy(rp => rp.PermissionId)
                    .Where(g => g.Count() > 1)
                    .CountAsync();
                Assert.Equal(0, duplicates);
            }
        }

        [Fact]
        [BusinessRule("BR-GLB-070")]
        public async Task A_curated_role_is_never_re_seeded()
        {
            await SeedAsync();

            using (var db = CreateContext())
            {
                var cashier = await db.Roles.SingleAsync(r => r.Code == "CASHIER");
                var grants = await db.RolePermissions.Where(rp => rp.RoleId == cashier.Id).ToListAsync();
                Assert.NotEmpty(grants);

                // A school takes the till away from its cashiers. That is their decision to make.
                var till = await db.Permissions.SingleAsync(p =>
                    p.ModuleCode == ScreenCatalog.Modules.Payments && p.ScreenCode == ScreenCatalog.Payments.Till && p.Action == ActionVerb.Post);
                db.RolePermissions.RemoveRange(grants.Where(g => g.PermissionId == till.Id));
                await db.SaveChangesAsync();
            }

            await SeedAsync();

            var (verifyDb, service) = await AsAsync("CASHIER");
            using var __ = verifyDb;
            Assert.False(await service.HasPermissionAsync(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Till, ActionVerb.Post),
                "A revoked grant came back on the next start, which makes curation pointless.");
        }

        /// <summary>
        /// doc/Modules/37 §6 gives the planner and the library to "Teacher, HoD" and the homework
        /// desk to "Teacher"; BR-LRN-002 extends a head of department across their department's
        /// offerings for content and homework alike.
        /// <para>
        /// This is asserted for both roles rather than one because the failure it guards was not a
        /// refusal — the module had no matrix row at all, so BR-SEC-010 hid it from the sidebar and
        /// the launcher, and the teacher the module was built for was never shown that a lesson
        /// planner existed. A missing grant looks like a missing feature.
        /// </para>
        /// </summary>
        [Theory]
        [InlineData("TEACHER")]
        [InlineData("HEAD_OF_DEPARTMENT")]
        [BusinessRule("BR-LRN-002")]
        public async Task The_staff_who_teach_can_open_the_planner_the_library_and_the_homework_desk(string roleCode)
        {
            await SeedAsync();
            var (db, service) = await AsAsync(roleCode);
            using var _ = db;

            Assert.True(await service.HasPermissionAsync(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Planner, ActionVerb.View));
            Assert.True(await service.HasPermissionAsync(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Resources, ActionVerb.View));
            Assert.True(await service.HasPermissionAsync(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Homework, ActionVerb.View));

            // Approve is publish and issue (see the catalogue's own deviation note): without it the
            // planner writes drafts nobody can read, and no other role is named to publish them.
            Assert.True(await service.HasPermissionAsync(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Planner, ActionVerb.Approve));
            Assert.True(await service.HasPermissionAsync(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Homework, ActionVerb.Approve));

            // Reaching the staff desk is not reaching the family's copy of it — a different
            // audience with its own scoping (BR-SEC-010, BR-SEC-011).
            Assert.False(await service.HasPermissionAsync(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Work, ActionVerb.View));
        }

        /// <summary>
        /// doc/Modules/37 §6 and §8.10: "my work" is one page for the whole family, and a student
        /// account's family is itself. The parent reaches every portal screen through a wildcard;
        /// the student is enumerated screen by screen, so each new portal page has to be added here
        /// or it silently serves only half the audience — which is what happened to this one.
        /// </summary>
        [Fact]
        [BusinessRule("BR-LRN-003")]
        public async Task A_student_and_a_parent_both_reach_their_homework_in_the_portal()
        {
            await SeedAsync();

            var (studentDb, student) = await AsAsync("STUDENT");
            using (studentDb)
            {
                Assert.True(await student.HasPermissionAsync(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Work, ActionVerb.View));
                // Still not the family's money (the row above this one in the matrix).
                Assert.False(await student.HasPermissionAsync(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Statement, ActionVerb.View));
            }

            var (parentDb, parent) = await AsAsync("PARENT");
            using (parentDb)
            {
                Assert.True(await parent.HasPermissionAsync(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Work, ActionVerb.View));
            }
        }

        /// <summary>
        /// doc/Modules/37 §5 gives the student "read content" beside "submit homework". Same
        /// enumeration trap as the row above: the parent's wildcard picks a new portal screen up by
        /// itself and the student's list does not.
        /// </summary>
        [Fact]
        [BusinessRule("BR-LRN-003")]
        public async Task A_student_and_a_parent_both_reach_their_lessons_in_the_portal()
        {
            await SeedAsync();

            var (studentDb, student) = await AsAsync("STUDENT");
            using (studentDb)
            {
                Assert.True(await student.HasPermissionAsync(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Lessons, ActionVerb.View));
            }

            var (parentDb, parent) = await AsAsync("PARENT");
            using (parentDb)
            {
                Assert.True(await parent.HasPermissionAsync(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Lessons, ActionVerb.View));
            }
        }

        /// <summary>
        /// The defect that made the two tests above true in a fresh database and false in every
        /// real one. A portal screen added to the matrix after a school was provisioned reached
        /// nobody: "already holds a grant" was read as "the school has curated this role", which is
        /// right for a cashier and wrong for the portal — a portal role is not a decision, it
        /// follows from the account type (<c>RoleTemplates.ForPortalAccount</c>), and exactly one
        /// seeded role opens the portal at all. <c>POR|Work</c> was catalogued on the owner's
        /// databases, granted to nobody, and therefore hidden by the portal's own bar (BR-SEC-010):
        /// a page that existed, worked, and could not be reached.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SEC-010")]
        public async Task A_portal_screen_added_after_provisioning_still_reaches_the_family()
        {
            // A school provisioned before this screen existed: the portal roles hold their old
            // grants, and the catalogue has since grown.
            using (var db = CreateContext())
            {
                await new RoleTemplateSeedContributor(db).SeedAsync();

                var old = new Permission { ModuleCode = ScreenCatalog.Modules.Portal, ScreenCode = ScreenCatalog.Portal.Home, Action = ActionVerb.View };
                db.Permissions.Add(old);
                await db.SaveChangesAsync();

                foreach (var roleCode in new[] { "PARENT", "STUDENT" })
                {
                    var role = await db.Roles.SingleAsync(r => r.Code == roleCode);
                    db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = old.Id });
                }

                await db.SaveChangesAsync();
            }

            using (var db = CreateContext())
            {
                await new PermissionSeedContributor(db).SeedAsync();
            }

            foreach (var roleCode in new[] { "PARENT", "STUDENT" })
            {
                var (db, service) = await AsAsync(roleCode);
                using var _ = db;
                Assert.True(await service.HasPermissionAsync(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Work, ActionVerb.View),
                    $"{roleCode} never received POR/Work, so \"my work\" is invisible on every database provisioned before it shipped.");
                Assert.True(await service.HasPermissionAsync(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Lessons, ActionVerb.View),
                    $"{roleCode} never received POR/Lessons.");
            }

            // A top-up adds; it never duplicates, and it never reaches outside the portal.
            using (var db = CreateContext())
            {
                var parentRole = await db.Roles.SingleAsync(r => r.Code == "PARENT");
                var duplicates = await db.RolePermissions
                    .Where(rp => rp.RoleId == parentRole.Id)
                    .GroupBy(rp => rp.PermissionId)
                    .Where(g => g.Count() > 1)
                    .CountAsync();
                Assert.Equal(0, duplicates);

                var outsideThePortal = await db.RolePermissions
                    .Where(rp => rp.RoleId == parentRole.Id)
                    .Join(db.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.ModuleCode)
                    .Where(code => code != ScreenCatalog.Modules.Portal)
                    .CountAsync();
                Assert.Equal(0, outsideThePortal);
            }
        }

        /// <summary>
        /// The other half of that trade-off, stated so it stays deliberate: a top-up restores the
        /// portal role's own defaults, and it is still not a way into anybody else's screens. A
        /// staff role keeps its curation untouched — revoking from a cashier is a decision.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SEC-010")]
        public async Task A_staff_roles_curation_survives_a_re_run()
        {
            using (var db = CreateContext())
            {
                await new RoleTemplateSeedContributor(db).SeedAsync();
                var cashier = await db.Roles.SingleAsync(r => r.Code == "CASHIER");
                var one = new Permission { ModuleCode = ScreenCatalog.Modules.Payments, ScreenCode = ScreenCatalog.Payments.Cashier, Action = ActionVerb.View };
                db.Permissions.Add(one);
                await db.SaveChangesAsync();
                db.RolePermissions.Add(new RolePermission { RoleId = cashier.Id, PermissionId = one.Id });
                await db.SaveChangesAsync();
            }

            using (var db = CreateContext())
            {
                await new PermissionSeedContributor(db).SeedAsync();
            }

            using (var db = CreateContext())
            {
                var cashier = await db.Roles.SingleAsync(r => r.Code == "CASHIER");
                var held = await db.RolePermissions.CountAsync(rp => rp.RoleId == cashier.Id);
                Assert.Equal(1, held);
            }
        }
    }
}
