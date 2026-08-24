using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Schools;
using Sms.Domain.Security;
using Sms.Infrastructure.Dashboards;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Seeding;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// Registering a panel is what makes it a widget — gated, orderable,
    /// personalizable. So the tests are about the consequences of registration
    /// rather than about rows: does every widget name a permission that exists, does
    /// no widget leak to the portal, and does a role's default layout survive a
    /// re-run without duplicating itself.
    /// </summary>
    public sealed class WidgetRegistrySeedContributorTests : IDisposable
    {
        private sealed class Tenant : ITenantContext
        {
            public int SchoolId => 1;
        }

        private sealed class User : ICurrentUser
        {
            public int UserId => 0;
        }

        private sealed class Clock : IClock
        {
            public DateTime UtcNow => new(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc);
        }

        private readonly SqliteConnection _connection;

        public WidgetRegistrySeedContributorTests()
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

        private async Task SeedAsync()
        {
            using var db = CreateContext();
            await new RoleTemplateSeedContributor(db).SeedAsync();
            await new PermissionSeedContributor(db).SeedAsync();
            await new WidgetRegistrySeedContributor(db, new DashboardAdmin(db)).SeedAsync();
        }

        [Fact]
        [BusinessRule("BR-DSH-001")]
        public async Task Every_computing_panel_is_registered_once_and_re_running_adds_none()
        {
            await SeedAsync();
            using var db = CreateContext();
            var codes = await db.WidgetDefinitions.AsNoTracking().Select(w => w.Code).ToListAsync();

            Assert.Equal(
                new[] { "DSH-ATT-001", "DSH-FEE-001", "DSH-GRD-001", "DSH-INS-001", "DSH-SET-001", "DSH-TCH-001" },
                codes.OrderBy(c => c, StringComparer.Ordinal).ToArray());

            await SeedAsync();
            using var again = CreateContext();
            Assert.Equal(codes.Count, await again.WidgetDefinitions.CountAsync());
        }

        [Fact]
        [BusinessRule("BR-DSH-001")]
        public async Task Every_widget_is_gated_by_a_view_permission_that_exists()
        {
            await SeedAsync();
            using var db = CreateContext();

            var permissions = await db.Permissions.AsNoTracking().ToDictionaryAsync(p => p.Id);
            foreach (var widget in await db.WidgetDefinitions.AsNoTracking().ToListAsync())
            {
                Assert.True(permissions.ContainsKey(widget.RequiredPermissionId), $"{widget.Code} names permission {widget.RequiredPermissionId}, which does not exist");
                var gate = permissions[widget.RequiredPermissionId];
                Assert.Equal(ActionVerb.View, gate.Action);
                Assert.Equal(widget.OwningModuleCode, gate.ModuleCode);

                Assert.False(string.IsNullOrWhiteSpace(widget.TitleAr), $"{widget.Code} has no Arabic title");
                Assert.False(string.IsNullOrWhiteSpace(widget.TitleEn), $"{widget.Code} has no English title");
                Assert.False(string.IsNullOrWhiteSpace(widget.DrillTargetCode), $"{widget.Code} drills nowhere");
            }
        }

        [Fact]
        [BusinessRule("BR-DSH-006")]
        public async Task No_staff_widget_is_portal_eligible()
        {
            await SeedAsync();
            using var db = CreateContext();
            Assert.Empty(await db.WidgetDefinitions.AsNoTracking().Where(w => w.IsPortalEligible).ToListAsync());
        }

        [Fact]
        [BusinessRule("BR-DSH-003")]
        public async Task Each_persona_gets_one_ordered_layout_and_re_running_adds_none()
        {
            await SeedAsync();

            using (var db = CreateContext())
            {
                var roles = await db.Roles.AsNoTracking().ToDictionaryAsync(r => r.Code, r => r.Id);
                var templates = await db.LayoutTemplates.AsNoTracking().ToListAsync();
                Assert.Equal(5, templates.Count);

                var principal = templates.Single(t => t.RoleId == roles["PRINCIPAL"]);
                var rows = await db.LayoutTemplateWidgets.AsNoTracking()
                    .Where(x => x.LayoutTemplateId == principal.Id).OrderBy(x => x.SortOrder).ToListAsync();
                var widgets = await db.WidgetDefinitions.AsNoTracking().ToDictionaryAsync(w => w.Id, w => w.Code);

                Assert.Equal(
                    new[] { "DSH-ATT-001", "DSH-GRD-001", "DSH-FEE-001", "DSH-INS-001", "DSH-TCH-001" },
                    rows.Select(r => widgets[r.WidgetDefinitionId]).ToArray());
                Assert.Equal(new[] { 10, 20, 30, 40, 50 }, rows.Select(r => r.SortOrder).ToArray());

                // The teacher persona's dashboard is the "My classes" workspace, which
                // does not exist yet — and TEACHER holds Attendance/Capture, not
                // Attendance/Analytics, so an eager template would render empty.
                Assert.DoesNotContain(templates, t => t.RoleId == roles["TEACHER"]);
            }

            await SeedAsync();

            using var after = CreateContext();
            Assert.Equal(5, await after.LayoutTemplates.CountAsync());
            Assert.Equal(11, await after.LayoutTemplateWidgets.CountAsync());
        }

        /// <summary>
        /// A school that reorders or drops a widget from a role's template owns that
        /// decision; re-seeding must not put it back.
        /// </summary>
        [Fact]
        [BusinessRule("BR-DSH-003")]
        public async Task A_school_that_edits_a_template_keeps_its_edit()
        {
            await SeedAsync();

            using (var db = CreateContext())
            {
                var roleId = await db.Roles.Where(r => r.Code == "FINANCE_MANAGER").Select(r => r.Id).SingleAsync();
                var template = await db.LayoutTemplates.SingleAsync(t => t.RoleId == roleId);
                var rows = await db.LayoutTemplateWidgets.Where(x => x.LayoutTemplateId == template.Id).ToListAsync();
                db.LayoutTemplateWidgets.RemoveRange(rows.Take(1));
                await db.SaveChangesAsync();
            }

            await SeedAsync();

            using var after = CreateContext();
            var financeRoleId = await after.Roles.Where(r => r.Code == "FINANCE_MANAGER").Select(r => r.Id).SingleAsync();
            var financeTemplate = await after.LayoutTemplates.SingleAsync(t => t.RoleId == financeRoleId);
            Assert.Single(await after.LayoutTemplateWidgets.Where(x => x.LayoutTemplateId == financeTemplate.Id).ToListAsync());
        }
    }
}
