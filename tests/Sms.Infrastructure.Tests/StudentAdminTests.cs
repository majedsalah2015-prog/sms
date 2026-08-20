using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Common;
using Sms.Domain.Grades;
using Sms.Domain.Numbering;
using Sms.Domain.Schools;
using Sms.Domain.Students;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Students;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// E-202 (slice: Students, doc/Modules/10, BR-STU-001..003) over a real
    /// Sqlite-backed AppDbContext, including E-006's real INumberIssuer
    /// (the "STU" series) — the first real consumer of that framework.
    /// </summary>
    public sealed class StudentAdminTests : IDisposable
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
        private int _profileId;

        public StudentAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            db.NumberingSeries.Add(new NumberingSeries
            {
                Code = "STU", EntityName = "Student", FormatTemplate = "STU-{SEQ:6}",
                ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Normal, EffectiveFromUtc = _clock.UtcNow, IsActive = true,
            });

            var year = new AcademicYear
            {
                LabelAr = "٢٠٢٦-٢٠٢٧", LabelEn = "2026-2027", HijriLabel = "١٤٤٨هـ",
                StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 6, 30), Status = AcademicYearStatus.Active,
            };
            db.AcademicYears.Add(year);
            var stage = new Stage { Name = new LocalizedName("الابتدائية", "Elementary"), SequenceOrder = 1, DefaultGenderPolicy = GenderPolicy.Mixed };
            db.Stages.Add(stage);
            db.SaveChanges();
            var grade = new GradeLevel { StageId = stage.Id, Code = "G3", Name = new LocalizedName("ثالث", "Grade 3"), SequenceOrder = 3 };
            db.GradeLevels.Add(grade);
            db.SaveChanges();
            var profile = new GradeYearProfile { GradeLevelId = grade.Id, AcademicYearId = year.Id, GenderPolicy = GenderPolicy.Mixed, TargetSections = 2, TargetSectionSize = 25 };
            db.GradeYearProfiles.Add(profile);
            db.SaveChanges();
            _profileId = profile.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private static Task<Student> Register(StudentAdmin admin, string suffix = "1")
            => admin.RegisterStudentAsync(
                "طالب" + suffix, "أب", "جد", "عائلة", "Student" + suffix, "Father", "Grandfather", "Family",
                Gender.Male, new DateTime(2018, 1, 1), nationalityLookupId: 1);

        // --- BR-STU-001 registration + real numbering integration --------------

        [Fact]
        [BusinessRule("BR-STU-001")]
        public async Task Registering_a_student_issues_a_real_permanent_number_via_the_STU_series()
        {
            using var db = CreateContext();
            var admin = new StudentAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));

            var student = await Register(admin);

            Assert.Equal("STU-000001", student.StudentNo);
        }

        [Fact]
        [BusinessRule("BR-STU-001")]
        public async Task Successive_registrations_get_distinct_sequential_numbers()
        {
            using var db = CreateContext();
            var admin = new StudentAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));

            var first = await Register(admin, "1");
            var second = await Register(admin, "2");

            Assert.Equal("STU-000001", first.StudentNo);
            Assert.Equal("STU-000002", second.StudentNo);
        }

        [Fact]
        [BusinessRule("BR-STU-001")]
        public async Task Editing_identity_fields_without_a_reason_is_rejected()
        {
            using var db = CreateContext();
            var admin = new StudentAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var student = await Register(admin);

            _audit.Reason = null;
            var tracked = await db.Students.SingleAsync(s => s.Id == student.Id);
            tracked.FirstNameEn = "Changed";
            await Assert.ThrowsAsync<MissingAuditReasonException>(() => db.SaveChangesAsync());
        }

        // --- BR-STU-002 status lifecycle ----------------------------------------

        [Fact]
        [BusinessRule("BR-STU-002")]
        public async Task An_illegal_status_transition_is_rejected()
        {
            using var db = CreateContext();
            var admin = new StudentAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var student = await Register(admin);

            await Assert.ThrowsAsync<InvalidStudentStatusTransitionException>(() =>
                admin.ChangeStatusAsync(student.Id, StudentStatus.Alumni)); // Enrolled -> Alumni skips Graduated
        }

        // --- BR-STU-003 guardian links + financial-responsibility guard --------

        [Fact]
        [BusinessRule("BR-STU-003")]
        public async Task Unlinking_the_last_financially_responsible_guardian_is_rejected()
        {
            using var db = CreateContext();
            var admin = new StudentAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var student = await Register(admin);
            var link = await admin.LinkGuardianAsync(
                student.Id, parentId: 1, relationshipLookupId: 1, isPrimaryContact: true, isFinanciallyResponsible: true,
                isPickupAuthorized: true, isPortalVisible: true, new DateTime(2026, 9, 1));

            await Assert.ThrowsAsync<LastFinanciallyResponsibleGuardianException>(() =>
                admin.UnlinkGuardianAsync(link.Id, new DateTime(2026, 10, 1)));
        }

        [Fact]
        [BusinessRule("BR-STU-003")]
        public async Task Unlinking_one_of_two_financially_responsible_guardians_succeeds()
        {
            using var db = CreateContext();
            var admin = new StudentAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var student = await Register(admin);
            var first = await admin.LinkGuardianAsync(student.Id, 1, 1, true, true, true, true, new DateTime(2026, 9, 1));
            await admin.LinkGuardianAsync(student.Id, 2, 1, false, true, false, true, new DateTime(2026, 9, 1));

            await admin.UnlinkGuardianAsync(first.Id, new DateTime(2026, 10, 1));

            Assert.Equal(new DateTime(2026, 10, 1), db.StudentGuardianLinks.Single(l => l.Id == first.Id).EffectiveToUtc);
        }

        // --- Enrollment (BR-GLB-024) --------------------------------------------

        [Fact]
        public async Task Enrolling_a_student_creates_an_active_enrollment_for_the_grade_year()
        {
            using var db = CreateContext();
            var admin = new StudentAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var student = await Register(admin);

            var enrollment = await admin.EnrollAsync(student.Id, _profileId, new DateTime(2026, 9, 1), EnrollmentSourceType.Admission);

            Assert.Equal(EnrollmentStatus.Active, db.Enrollments.Single(e => e.Id == enrollment.Id).Status);
        }

        [Fact]
        [BusinessRule("BR-GLB-024")]
        public async Task A_second_active_enrollment_for_the_same_student_and_year_is_rejected()
        {
            using var db = CreateContext();
            var admin = new StudentAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var student = await Register(admin);
            await admin.EnrollAsync(student.Id, _profileId, new DateTime(2026, 9, 1), EnrollmentSourceType.Admission);

            await Assert.ThrowsAsync<DuplicateEnrollmentException>(() =>
                admin.EnrollAsync(student.Id, _profileId, new DateTime(2026, 9, 2), EnrollmentSourceType.Admission));
        }

        [Fact]
        [BusinessRule("BR-STU-002")]
        public async Task Renaming_a_student_requires_an_audit_reason_because_identity_is_T1()
        {
            using var db = CreateContext();
            var admin = new StudentAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var student = await Register(admin);

            _audit.Reason = null;
            await Assert.ThrowsAsync<Sms.Application.Common.Exceptions.MissingAuditReasonException>(() => admin.UpdateStudentAsync(
                student.Id, "جديد", "أب", "جد", "عائلة", "Renamed", "Father", "Grandfather", "Family", Gender.Male, new DateTime(2018, 1, 1), 1));

            _audit.Reason = "birth certificate";
            var updated = await admin.UpdateStudentAsync(
                student.Id, "جديد", "أب", "جد", "عائلة", "Renamed", "Father", "Grandfather", "Family", Gender.Male, new DateTime(2018, 1, 1), 1, primaryIdNo: "1098765432");
            Assert.Equal("Renamed", updated.FirstNameEn);
            Assert.Equal("1098765432", db.Students.Single(s => s.Id == student.Id).PrimaryIdNo);
            _audit.Reason = null;
        }
    }
}
