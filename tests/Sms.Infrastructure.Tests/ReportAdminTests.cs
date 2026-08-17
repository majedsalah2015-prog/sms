using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Common;
using Sms.Domain.Reports;
using Sms.Domain.Security;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Reports;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>S7/E-701 (Reports platform, doc/Modules/30, BR-RPT-002/003/005/006) over a real Sqlite-backed AppDbContext.</summary>
    public sealed class ReportAdminTests : IDisposable
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

            public int AcademicYearId => 2027;
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();
        private int _permissionId;
        private int _viewerUserId;
        private int _exporterUserId;
        private int _strangerUserId;

        public ReportAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            var viewPermission = new Permission { ModuleCode = "RPT", ScreenCode = "RPT001", Action = ActionVerb.View };
            var exportPermission = new Permission { ModuleCode = "RPT", ScreenCode = "RPT001", Action = ActionVerb.Export };
            db.Permissions.Add(viewPermission);
            db.Permissions.Add(exportPermission);
            db.SaveChanges();

            var viewerRole = new Role { Code = "RPT_VIEWER", Name = new LocalizedName("مشاهد", "Report Viewer") };
            var exporterRole = new Role { Code = "RPT_EXPORTER", Name = new LocalizedName("مصدّر", "Report Exporter") };
            db.Roles.Add(viewerRole);
            db.Roles.Add(exporterRole);
            db.SaveChanges();
            db.RolePermissions.Add(new RolePermission { RoleId = viewerRole.Id, PermissionId = viewPermission.Id });
            db.RolePermissions.Add(new RolePermission { RoleId = exporterRole.Id, PermissionId = viewPermission.Id });
            db.RolePermissions.Add(new RolePermission { RoleId = exporterRole.Id, PermissionId = exportPermission.Id });
            db.SaveChanges();

            var viewerUser = new UserAccount { UserName = "viewer", AccountType = AccountType.Staff };
            var exporterUser = new UserAccount { UserName = "exporter", AccountType = AccountType.Staff };
            var strangerUser = new UserAccount { UserName = "stranger", AccountType = AccountType.Staff };
            db.UserAccounts.Add(viewerUser);
            db.UserAccounts.Add(exporterUser);
            db.UserAccounts.Add(strangerUser);
            db.SaveChanges();

            db.RoleAssignments.Add(new RoleAssignment { UserAccountId = viewerUser.Id, RoleId = viewerRole.Id });
            db.RoleAssignments.Add(new RoleAssignment { UserAccountId = exporterUser.Id, RoleId = exporterRole.Id });
            db.SaveChanges();

            _permissionId = viewPermission.Id;
            _viewerUserId = viewerUser.Id;
            _exporterUserId = exporterUser.Id;
            _strangerUserId = strangerUser.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private async Task<ReportDefinition> DefineReportAsync(
            ReportAdmin admin, ReportSensitivity sensitivity = ReportSensitivity.Normal, string? requiredParameterKeysCsv = null)
            => await admin.DefineReportAsync(
                "RPT-RPT-001", "RPT", "تقرير", "Report", OutputFormat.Html | OutputFormat.Pdf, sensitivity, _permissionId, requiredParameterKeysCsv);

        // --- BR-RPT-002 View-permission gate ------------------------------------------

        [Fact]
        [BusinessRule("BR-RPT-002")]
        public async Task Running_with_the_view_permission_succeeds()
        {
            using var db = CreateContext();
            var admin = new ReportAdmin(db, _clock);
            var definition = await DefineReportAsync(admin);

            var execution = await admin.RunReportAsync(
                definition.Id, _viewerUserId, "{}", System.Array.Empty<string>(), OutputFormat.Html, isExport: false, estimatedRowCount: 10);

            Assert.Equal(ReportExecutionStatus.Completed, execution.Status);
        }

        [Fact]
        [BusinessRule("BR-RPT-002")]
        public async Task Running_without_the_view_permission_is_rejected()
        {
            using var db = CreateContext();
            var admin = new ReportAdmin(db, _clock);
            var definition = await DefineReportAsync(admin);

            await Assert.ThrowsAsync<ReportPermissionDeniedException>(() =>
                admin.RunReportAsync(definition.Id, _strangerUserId, "{}", System.Array.Empty<string>(), OutputFormat.Html, isExport: false, estimatedRowCount: 10));
        }

        // --- doc §9 required parameters -------------------------------------------------

        [Fact]
        public async Task Running_without_a_required_parameter_is_rejected()
        {
            using var db = CreateContext();
            var admin = new ReportAdmin(db, _clock);
            var definition = await DefineReportAsync(admin, requiredParameterKeysCsv: "SectionId,TermId");

            await Assert.ThrowsAsync<MissingRequiredParametersException>(() =>
                admin.RunReportAsync(definition.Id, _viewerUserId, "{}", new[] { "SectionId" }, OutputFormat.Html, isExport: false, estimatedRowCount: 10));
        }

        [Fact]
        public async Task Running_with_all_required_parameters_supplied_succeeds()
        {
            using var db = CreateContext();
            var admin = new ReportAdmin(db, _clock);
            var definition = await DefineReportAsync(admin, requiredParameterKeysCsv: "SectionId,TermId");

            var execution = await admin.RunReportAsync(
                definition.Id, _viewerUserId, "{}", new[] { "sectionid", "termid" }, OutputFormat.Html, isExport: false, estimatedRowCount: 10);

            Assert.Equal(ReportExecutionStatus.Completed, execution.Status);
        }

        // --- BR-RPT-003 Export-permission gate -------------------------------------------

        [Fact]
        [BusinessRule("BR-RPT-003")]
        public async Task Exporting_a_personal_data_report_without_the_export_permission_is_rejected()
        {
            using var db = CreateContext();
            var admin = new ReportAdmin(db, _clock);
            var definition = await DefineReportAsync(admin, ReportSensitivity.PersonalData);

            await Assert.ThrowsAsync<ReportExportNotAllowedException>(() =>
                admin.RunReportAsync(definition.Id, _viewerUserId, "{}", System.Array.Empty<string>(), OutputFormat.Pdf, isExport: true, estimatedRowCount: 10));
        }

        [Fact]
        [BusinessRule("BR-RPT-003")]
        public async Task Exporting_a_personal_data_report_with_the_export_permission_succeeds()
        {
            using var db = CreateContext();
            var admin = new ReportAdmin(db, _clock);
            var definition = await DefineReportAsync(admin, ReportSensitivity.PersonalData);

            var execution = await admin.RunReportAsync(
                definition.Id, _exporterUserId, "{}", System.Array.Empty<string>(), OutputFormat.Pdf, isExport: true, estimatedRowCount: 10);

            Assert.True(execution.WasExport);
        }

        [Fact]
        [BusinessRule("BR-RPT-003")]
        public async Task Exporting_a_normal_report_never_needs_the_export_permission()
        {
            using var db = CreateContext();
            var admin = new ReportAdmin(db, _clock);
            var definition = await DefineReportAsync(admin, ReportSensitivity.Normal);

            var execution = await admin.RunReportAsync(
                definition.Id, _viewerUserId, "{}", System.Array.Empty<string>(), OutputFormat.Pdf, isExport: true, estimatedRowCount: 10);

            Assert.True(execution.WasExport);
        }

        // --- BR-RPT-005 heavy report queueing --------------------------------------------

        [Fact]
        [BusinessRule("BR-RPT-005")]
        public async Task A_heavy_report_run_is_queued_not_completed_inline()
        {
            using var db = CreateContext();
            var admin = new ReportAdmin(db, _clock);
            var definition = await DefineReportAsync(admin);

            var execution = await admin.RunReportAsync(
                definition.Id, _viewerUserId, "{}", System.Array.Empty<string>(), OutputFormat.Csv, isExport: false, estimatedRowCount: 6000, heavyRowThreshold: 5000);

            Assert.Equal(ReportExecutionStatus.Queued, execution.Status);
            Assert.Null(execution.RowCount);
            Assert.Null(execution.CompletedAtUtc);
        }

        [Fact]
        [BusinessRule("BR-RPT-005")]
        public async Task Completing_a_queued_run_stamps_the_result()
        {
            using var db = CreateContext();
            var admin = new ReportAdmin(db, _clock);
            var definition = await DefineReportAsync(admin);
            var queued = await admin.RunReportAsync(
                definition.Id, _viewerUserId, "{}", System.Array.Empty<string>(), OutputFormat.Csv, isExport: false, estimatedRowCount: 6000, heavyRowThreshold: 5000);

            var completed = await admin.CompleteQueuedRunAsync(queued.Id, rowCount: 5980, durationMs: 42000);

            Assert.Equal(ReportExecutionStatus.Completed, completed.Status);
            Assert.Equal(5980, completed.RowCount);
            Assert.Equal(42000, completed.DurationMs);
            Assert.NotNull(completed.CompletedAtUtc);
        }

        [Fact]
        [BusinessRule("BR-RPT-005")]
        public async Task Completing_a_run_that_is_not_queued_is_rejected()
        {
            using var db = CreateContext();
            var admin = new ReportAdmin(db, _clock);
            var definition = await DefineReportAsync(admin);
            var completed = await admin.RunReportAsync(
                definition.Id, _viewerUserId, "{}", System.Array.Empty<string>(), OutputFormat.Html, isExport: false, estimatedRowCount: 10);

            await Assert.ThrowsAsync<ReportExecutionNotQueuedException>(() =>
                admin.CompleteQueuedRunAsync(completed.Id, rowCount: 10, durationMs: 500));
        }

        // --- BR-RPT-006 subscription recipients ------------------------------------------

        [Fact]
        [BusinessRule("BR-RPT-006")]
        public async Task Subscribing_with_the_view_permission_succeeds()
        {
            using var db = CreateContext();
            var admin = new ReportAdmin(db, _clock);
            var definition = await DefineReportAsync(admin);

            var subscription = await admin.SubscribeAsync(
                definition.Id, _viewerUserId, SubscriptionFrequency.Weekly, "{}", OutputFormat.Pdf, DeliveryChannel.Email);

            Assert.True(subscription.IsActive);
        }

        [Fact]
        [BusinessRule("BR-RPT-006")]
        public async Task Subscribing_without_the_view_permission_is_rejected()
        {
            using var db = CreateContext();
            var admin = new ReportAdmin(db, _clock);
            var definition = await DefineReportAsync(admin);

            await Assert.ThrowsAsync<SubscriptionRecipientNotAuthorizedException>(() =>
                admin.SubscribeAsync(definition.Id, _strangerUserId, SubscriptionFrequency.Weekly, "{}", OutputFormat.Pdf, DeliveryChannel.Email));
        }

        [Fact]
        [BusinessRule("BR-RPT-003")]
        public async Task Subscribing_a_restricted_report_to_email_is_rejected()
        {
            using var db = CreateContext();
            var admin = new ReportAdmin(db, _clock);
            var definition = await DefineReportAsync(admin, ReportSensitivity.Restricted);

            await Assert.ThrowsAsync<RestrictedReportEmailDeliveryException>(() =>
                admin.SubscribeAsync(definition.Id, _viewerUserId, SubscriptionFrequency.Weekly, "{}", OutputFormat.Pdf, DeliveryChannel.Email));
        }

        [Fact]
        [BusinessRule("BR-RPT-003")]
        public async Task Subscribing_a_restricted_report_to_portal_succeeds()
        {
            using var db = CreateContext();
            var admin = new ReportAdmin(db, _clock);
            var definition = await DefineReportAsync(admin, ReportSensitivity.Restricted);

            var subscription = await admin.SubscribeAsync(
                definition.Id, _viewerUserId, SubscriptionFrequency.Weekly, "{}", OutputFormat.Pdf, DeliveryChannel.Portal);

            Assert.Equal(DeliveryChannel.Portal, subscription.DeliveryChannel);
        }

        [Fact]
        public async Task Cancelling_a_subscription_deactivates_it()
        {
            using var db = CreateContext();
            var admin = new ReportAdmin(db, _clock);
            var definition = await DefineReportAsync(admin);
            var subscription = await admin.SubscribeAsync(
                definition.Id, _viewerUserId, SubscriptionFrequency.Weekly, "{}", OutputFormat.Pdf, DeliveryChannel.Portal);

            await admin.CancelSubscriptionAsync(subscription.Id);

            Assert.False(db.ReportSubscriptions.Single(s => s.Id == subscription.Id).IsActive);
        }
    }
}
