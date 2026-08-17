using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Attendance;
using Sms.Domain.Certificates;
using Sms.Domain.Common;
using Sms.Domain.Dashboards;
using Sms.Domain.Fees;
using Sms.Domain.Grades;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Security;
using Sms.Domain.Students;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Dashboards;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>S7/E-702 (Dashboards, doc/Modules/31, BR-DSH-001/002/003/006/007) over a real Sqlite-backed AppDbContext.</summary>
    public sealed class DashboardAdminTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2027, 3, 1, 8, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; }
        }

        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;

            public int AcademicYearId { get; set; } = 2027;
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();
        private int _permissionId;
        private int _grantedUserId;
        private int _ungrantedUserId;
        private int _sectionId;

        public DashboardAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            var year = new AcademicYear
            {
                LabelAr = "Year", LabelEn = "2026-2027", HijriLabel = "Hijri",
                StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 6, 30), Status = AcademicYearStatus.Active,
            };
            db.AcademicYears.Add(year);
            db.SaveChanges();
            _tenant.AcademicYearId = year.Id;

            var stage = new Stage { Name = new LocalizedName("Stage", "Elementary"), SequenceOrder = 1, DefaultGenderPolicy = GenderPolicy.Mixed };
            db.Stages.Add(stage);
            db.SaveChanges();
            var grade = new GradeLevel { StageId = stage.Id, Code = "G3", Name = new LocalizedName("Grade", "Grade 3"), SequenceOrder = 3 };
            db.GradeLevels.Add(grade);
            db.SaveChanges();
            var profile = new GradeYearProfile { GradeLevelId = grade.Id, AcademicYearId = year.Id, GenderPolicy = GenderPolicy.Mixed, TargetSections = 1, TargetSectionSize = 25 };
            db.GradeYearProfiles.Add(profile);
            db.SaveChanges();
            var section = new Section { AcademicYearId = year.Id, GradeYearProfileId = profile.Id, NameAr = "Section", NameEn = "3-A", Capacity = 25, GenderPolicy = GenderPolicy.Mixed };
            db.Sections.Add(section);
            db.SaveChanges();

            var permission = new Permission { ModuleCode = "DSH", ScreenCode = "WIDGET1", Action = ActionVerb.View };
            db.Permissions.Add(permission);
            db.SaveChanges();

            var role = new Role { Code = "DASH_VIEWER", Name = new LocalizedName("مشاهد", "Dashboard Viewer") };
            db.Roles.Add(role);
            db.SaveChanges();
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            db.SaveChanges();

            var grantedUser = new UserAccount { UserName = "granted", AccountType = AccountType.Staff };
            var ungrantedUser = new UserAccount { UserName = "ungranted", AccountType = AccountType.Staff };
            db.UserAccounts.Add(grantedUser);
            db.UserAccounts.Add(ungrantedUser);
            db.SaveChanges();

            db.RoleAssignments.Add(new RoleAssignment { UserAccountId = grantedUser.Id, RoleId = role.Id });
            db.SaveChanges();

            _permissionId = permission.Id;
            _grantedUserId = grantedUser.Id;
            _ungrantedUserId = ungrantedUser.Id;
            _sectionId = section.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private async Task<WidgetDefinition> DefineWidgetAsync(DashboardAdmin admin)
            => await admin.DefineWidgetAsync(
                "DSH-DSH-001", "DSH", "أداة", "Widget", _permissionId, WidgetRefreshClass.Live, "ReportsCenter", isPortalEligible: false);

        // --- doc §9 server-enforced permission check ------------------------------------

        [Fact]
        [BusinessRule("BR-DSH-001")]
        public async Task Personalizing_with_a_granted_permission_succeeds()
        {
            using var db = CreateContext();
            var admin = new DashboardAdmin(db);
            var widget = await DefineWidgetAsync(admin);

            var layout = await admin.PersonalizeAsync(_grantedUserId, widget.Id, sortOrder: 1, isVisible: true);

            Assert.Equal(1, layout.SortOrder);
        }

        [Fact]
        [BusinessRule("BR-DSH-001")]
        public async Task Personalizing_without_the_permission_is_rejected()
        {
            using var db = CreateContext();
            var admin = new DashboardAdmin(db);
            var widget = await DefineWidgetAsync(admin);

            await Assert.ThrowsAsync<WidgetNotPermittedException>(() =>
                admin.PersonalizeAsync(_ungrantedUserId, widget.Id, sortOrder: 1, isVisible: true));
        }

        [Fact]
        [BusinessRule("BR-DSH-003")]
        public async Task Resetting_removes_all_personalization_rows()
        {
            using var db = CreateContext();
            var admin = new DashboardAdmin(db);
            var widget = await DefineWidgetAsync(admin);
            await admin.PersonalizeAsync(_grantedUserId, widget.Id, sortOrder: 1, isVisible: true);

            await admin.ResetToDefaultAsync(_grantedUserId);

            Assert.Empty(db.UserLayouts.Where(l => l.UserAccountId == _grantedUserId));
        }

        [Fact]
        [BusinessRule("BR-DSH-003")]
        public async Task Layout_templates_hold_widgets_in_order()
        {
            using var db = CreateContext();
            var admin = new DashboardAdmin(db);
            var widget = await DefineWidgetAsync(admin);
            var role = db.Roles.Single();

            var template = await admin.DefineLayoutTemplateAsync(role.Id);
            await admin.AddWidgetToTemplateAsync(template.Id, widget.Id, sortOrder: 1);

            Assert.Single(db.LayoutTemplateWidgets.Where(w => w.LayoutTemplateId == template.Id));
        }

        // --- BR-DSH-002 real widget data, reusing each source module's own calculator ---

        [Fact]
        [BusinessRule("BR-DSH-002")]
        public async Task Section_attendance_today_reuses_the_central_percentage_computation()
        {
            using var db = CreateContext();
            var student = new Student
            {
                StudentNo = "STU-TEST-1",
                FirstNameAr = "S", FatherNameAr = "F", GrandfatherNameAr = "G", FamilyNameAr = "Fam",
                FirstNameEn = "S", FatherNameEn = "F", GrandfatherNameEn = "G", FamilyNameEn = "Fam",
                Gender = Gender.Male, DateOfBirth = new DateTime(2018, 1, 1), NationalityLookupId = 1,
            };
            db.Students.Add(student);
            await db.SaveChangesAsync();
            var enrollment = new Enrollment
            {
                AcademicYearId = _tenant.AcademicYearId, StudentId = student.Id, GradeYearProfileId = db.GradeYearProfiles.Single().Id,
                EnrollmentDate = new DateTime(2026, 9, 1), SourceType = EnrollmentSourceType.Admission,
            };
            db.Enrollments.Add(enrollment);
            await db.SaveChangesAsync();

            var date = new DateTime(2027, 3, 1);
            db.AttendanceDays.Add(new AttendanceDay
            {
                AcademicYearId = _tenant.AcademicYearId, EnrollmentId = enrollment.Id, SectionId = _sectionId,
                Date = date, Status = AttendanceStatus.AbsentUnexcused, CapturedByUserId = 1,
            });
            await db.SaveChangesAsync();

            var query = new DashboardQuery(db);
            var snapshot = await query.GetSectionAttendanceTodayAsync(_sectionId, date);

            Assert.Equal(1, snapshot.ScheduledCount);
            Assert.Equal(1, snapshot.AbsentCount);
            Assert.Equal(0m, snapshot.PresentPercent);
        }

        [Fact]
        [BusinessRule("BR-DSH-002")]
        public async Task School_receivables_total_reflects_posted_charges_minus_credit_notes()
        {
            using var db = CreateContext();
            var payer = new Payer { Type = PayerType.Parent };
            db.Payers.Add(payer);
            var student = new Student
            {
                StudentNo = "STU-TEST-1",
                FirstNameAr = "S", FatherNameAr = "F", GrandfatherNameAr = "G", FamilyNameAr = "Fam",
                FirstNameEn = "S", FatherNameEn = "F", GrandfatherNameEn = "G", FamilyNameEn = "Fam",
                Gender = Gender.Male, DateOfBirth = new DateTime(2018, 1, 1), NationalityLookupId = 1,
            };
            db.Students.Add(student);
            var category = new FeeCategory { NameAr = "رسوم", NameEn = "Fee", IsMandatory = true };
            db.FeeCategories.Add(category);
            await db.SaveChangesAsync();
            var charge = new Charge
            {
                AcademicYearId = _tenant.AcademicYearId, StudentId = student.Id, PayerId = payer.Id, FeeCategoryId = category.Id,
                SourceType = ChargeSourceType.Manual, ChargeNo = "INV-000001", NetAmount = 1000m, GrossAmount = 1000m,
                Status = ChargeStatus.Posted, PostedAtUtc = _clock.UtcNow, InvoiceUuid = Guid.NewGuid(),
            };
            db.Charges.Add(charge);
            await db.SaveChangesAsync();
            db.CreditNotes.Add(new CreditNote { ChargeId = charge.Id, CreditNoteNo = "CRN-000001", Amount = 200m, Reason = "adjustment", IssuedAtUtc = _clock.UtcNow });
            await db.SaveChangesAsync();

            var query = new DashboardQuery(db);
            var total = await query.GetSchoolReceivablesTotalAsync();

            Assert.Equal(800m, total);
        }

        [Fact]
        [BusinessRule("BR-CRT-003")]
        public async Task Pending_certificate_requests_excludes_issued_and_rejected()
        {
            using var db = CreateContext();
            var type = new CertificateType { NameAr = "شهادة", NameEn = "Certificate", NumberingSeriesCode = "CERT" };
            db.CertificateTypes.Add(type);
            await db.SaveChangesAsync();

            db.CertificateRequests.Add(new CertificateRequest { CertificateTypeId = type.Id, StudentId = 1, RequestedByUserId = 1, Status = CertificateRequestStatus.Requested, RequestedAtUtc = _clock.UtcNow });
            db.CertificateRequests.Add(new CertificateRequest { CertificateTypeId = type.Id, StudentId = 2, RequestedByUserId = 1, Status = CertificateRequestStatus.Approved, RequestedAtUtc = _clock.UtcNow });
            db.CertificateRequests.Add(new CertificateRequest { CertificateTypeId = type.Id, StudentId = 3, RequestedByUserId = 1, Status = CertificateRequestStatus.Issued, RequestedAtUtc = _clock.UtcNow });
            db.CertificateRequests.Add(new CertificateRequest { CertificateTypeId = type.Id, StudentId = 4, RequestedByUserId = 1, Status = CertificateRequestStatus.Rejected, RequestedAtUtc = _clock.UtcNow });
            await db.SaveChangesAsync();

            var query = new DashboardQuery(db);
            var count = await query.GetPendingCertificateRequestsCountAsync();

            Assert.Equal(2, count);
        }
    }
}
