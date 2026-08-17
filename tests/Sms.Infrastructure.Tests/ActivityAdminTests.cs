using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Activities;
using Sms.Domain.Attendance;
using Sms.Domain.Common;
using Sms.Domain.Fees;
using Sms.Domain.Numbering;
using Sms.Domain.Schools;
using Sms.Domain.Students;
using Sms.Infrastructure.Activities;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Fees;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// S6/E-607 (Activities, doc/Modules/29, BR-ACT-001..008) over a real
    /// Sqlite-backed AppDbContext. Costed enrollment activation posts a
    /// real charge via E-303's IFeeAdmin.
    /// </summary>
    public sealed class ActivityAdminTests : IDisposable
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
        private int _termId;
        private int _feeCategoryId;
        private int _payerId;
        private int _gradeYearProfileId;
        private int _nextStudentSeq = 1;

        public ActivityAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            db.NumberingSeries.Add(new NumberingSeries
            {
                Code = "INV", EntityName = "Charge", FormatTemplate = "INV-{SEQ:6}",
                ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Normal, EffectiveFromUtc = _clock.UtcNow, IsActive = true,
            });

            var year = new AcademicYear
            {
                LabelAr = "Year", LabelEn = "2026-2027", HijriLabel = "Hijri",
                StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 6, 30), Status = AcademicYearStatus.Active,
            };
            db.AcademicYears.Add(year);
            db.SaveChanges();
            _tenant.AcademicYearId = year.Id;

            var semester = new Semester { AcademicYearId = year.Id, SequenceNumber = 1, NameAr = "S1", NameEn = "Semester 1", StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 1, 31) };
            db.Semesters.Add(semester);
            db.SaveChanges();
            var term = new Term { AcademicYearId = year.Id, SemesterId = semester.Id, SequenceNumber = 1, NameAr = "T1", NameEn = "Term 1", StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2026, 11, 30) };
            db.Terms.Add(term);
            db.SaveChanges();

            var payer = new Payer { Type = PayerType.Parent };
            db.Payers.Add(payer);
            db.SaveChanges();

            var stage = new Domain.Grades.Stage { Name = new LocalizedName("Stage", "Elementary"), SequenceOrder = 1, DefaultGenderPolicy = Domain.Grades.GenderPolicy.Mixed };
            db.Stages.Add(stage);
            db.SaveChanges();
            var grade = new Domain.Grades.GradeLevel { StageId = stage.Id, Code = "G3", Name = new LocalizedName("Grade", "Grade 3"), SequenceOrder = 3 };
            db.GradeLevels.Add(grade);
            db.SaveChanges();
            var profile = new Domain.Grades.GradeYearProfile { GradeLevelId = grade.Id, AcademicYearId = year.Id, GenderPolicy = Domain.Grades.GenderPolicy.Mixed, TargetSections = 1, TargetSectionSize = 25 };
            db.GradeYearProfiles.Add(profile);
            db.SaveChanges();

            var numberIssuer = new NumberIssuer(db, _tenant, _tenant, _clock);
            var feeAdmin = new FeeAdmin(db, numberIssuer, _clock);
            var category = feeAdmin.DefineCategoryAsync("Activity Fee", "Activity Fee", vatRate: null, isMandatory: false, isRefundable: false, isServiceLinked: false)
                .GetAwaiter().GetResult();

            _termId = term.Id;
            _feeCategoryId = category.Id;
            _payerId = payer.Id;
            _gradeYearProfileId = profile.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private ActivityAdmin CreateAdmin(AppDbContext db)
        {
            var numberIssuer = new NumberIssuer(db, _tenant, _tenant, _clock);
            var feeAdmin = new FeeAdmin(db, numberIssuer, _clock);
            return new ActivityAdmin(db, _clock, feeAdmin);
        }

        private async Task<int> SeedStudentAsync(AppDbContext db)
        {
            var seq = _nextStudentSeq++;
            var student = new Student
            {
                StudentNo = $"STU-TEST-{seq}",
                FirstNameAr = "S", FatherNameAr = "F", GrandfatherNameAr = "G", FamilyNameAr = "Fam",
                FirstNameEn = "S", FatherNameEn = "F", GrandfatherNameEn = "G", FamilyNameEn = "Fam",
                Gender = Gender.Male, DateOfBirth = new DateTime(2018, 1, 1), NationalityLookupId = 1,
            };
            db.Students.Add(student);
            await db.SaveChangesAsync();

            db.Enrollments.Add(new Enrollment
            {
                AcademicYearId = _tenant.AcademicYearId, StudentId = student.Id, GradeYearProfileId = _gradeYearProfileId,
                EnrollmentDate = new DateTime(2026, 9, 1), SourceType = EnrollmentSourceType.Admission,
            });
            await db.SaveChangesAsync();

            return student.Id;
        }

        private async Task<ActivityProgram> DefineFreeProgramAsync(ActivityAdmin admin, int capacity = 20, bool requiresConsent = false)
        {
            var type = await admin.DefineActivityTypeAsync("Chess Club", "Chess Club", ActivityCategory.Club);
            return await admin.DefineProgramAsync(type.Id, _termId, "شطرنج", "Chess Club", supervisorEmployeeId: 1, capacity: capacity, requiresConsent: requiresConsent);
        }

        // --- BR-ACT-002 capacity/waitlist ---------------------------------------------

        [Fact]
        [BusinessRule("BR-ACT-002")]
        public async Task Enrolling_with_capacity_and_no_consent_activates_immediately()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var program = await DefineFreeProgramAsync(admin);
            var studentId = await SeedStudentAsync(db);

            var enrollment = await admin.RequestEnrollmentAsync(program.Id, studentId);

            Assert.Equal(ProgramEnrollmentStatus.Active, enrollment.Status);
        }

        [Fact]
        [BusinessRule("BR-ACT-002")]
        public async Task Enrolling_past_capacity_waitlists()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var program = await DefineFreeProgramAsync(admin, capacity: 1);
            var first = await SeedStudentAsync(db);
            var second = await SeedStudentAsync(db);
            await admin.RequestEnrollmentAsync(program.Id, first);

            var enrollment = await admin.RequestEnrollmentAsync(program.Id, second);

            Assert.Equal(ProgramEnrollmentStatus.Waitlisted, enrollment.Status);
        }

        // --- BR-ACT-005 consent gate ----------------------------------------------------

        [Fact]
        [BusinessRule("BR-ACT-005")]
        public async Task A_consent_required_program_holds_the_enrollment_pending()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var program = await DefineFreeProgramAsync(admin, requiresConsent: true);
            var studentId = await SeedStudentAsync(db);

            var enrollment = await admin.RequestEnrollmentAsync(program.Id, studentId);

            Assert.Equal(ProgramEnrollmentStatus.ConsentPending, enrollment.Status);
        }

        [Fact]
        [BusinessRule("BR-ACT-005")]
        public async Task Granting_consent_activates_a_pending_enrollment()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var program = await DefineFreeProgramAsync(admin, requiresConsent: true);
            var studentId = await SeedStudentAsync(db);
            var enrollment = await admin.RequestEnrollmentAsync(program.Id, studentId);

            await admin.GrantConsentAsync(enrollment.Id, "I consent to my child joining Chess Club", grantedByUserId: 1);

            Assert.Equal(ProgramEnrollmentStatus.Active, db.ProgramEnrollments.Single(e => e.Id == enrollment.Id).Status);
        }

        // --- BR-ACT-002 withdrawal ------------------------------------------------------

        [Fact]
        [BusinessRule("BR-ACT-002")]
        public async Task Withdrawing_an_active_enrollment_records_the_reason()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var program = await DefineFreeProgramAsync(admin);
            var studentId = await SeedStudentAsync(db);
            var enrollment = await admin.RequestEnrollmentAsync(program.Id, studentId);

            await admin.WithdrawEnrollmentAsync(enrollment.Id, "moved schools");

            var updated = db.ProgramEnrollments.Single(e => e.Id == enrollment.Id);
            Assert.Equal(ProgramEnrollmentStatus.Withdrawn, updated.Status);
            Assert.Equal("moved schools", updated.WithdrawalReason);
        }

        // --- BR-ACT-007 costed programs --------------------------------------------------

        [Fact]
        [BusinessRule("BR-ACT-007")]
        public async Task A_costed_program_posts_a_real_charge_on_activation()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var type = await admin.DefineActivityTypeAsync("Swim Team", "Swim Team", ActivityCategory.Sport);
            var program = await admin.DefineProgramAsync(
                type.Id, _termId, "سباحة", "Swim Team", supervisorEmployeeId: 1, capacity: 20, requiresConsent: false,
                costAmount: 300m, feeCategoryId: _feeCategoryId);
            var studentId = await SeedStudentAsync(db);

            var enrollment = await admin.RequestEnrollmentAsync(program.Id, studentId, _payerId);

            Assert.Equal(ProgramEnrollmentStatus.Active, enrollment.Status);
            Assert.NotNull(enrollment.ChargeId);
            var charge = db.Charges.Single(c => c.Id == enrollment.ChargeId);
            Assert.Equal(300m, charge.GrossAmount);
        }

        [Fact]
        [BusinessRule("BR-ACT-007")]
        public async Task A_costed_program_without_a_payer_is_rejected()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var type = await admin.DefineActivityTypeAsync("Swim Team", "Swim Team", ActivityCategory.Sport);
            var program = await admin.DefineProgramAsync(
                type.Id, _termId, "سباحة", "Swim Team", supervisorEmployeeId: 1, capacity: 20, requiresConsent: false,
                costAmount: 300m, feeCategoryId: _feeCategoryId);
            var studentId = await SeedStudentAsync(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => admin.RequestEnrollmentAsync(program.Id, studentId));
        }

        // --- BR-ACT-004 trip departure/return --------------------------------------------

        private async Task<(ActivityProgram program, ActivityTrip trip, int enrollmentId)> SeedReadyTripAsync(ActivityAdmin admin, AppDbContext db)
        {
            var program = await DefineFreeProgramAsync(admin, requiresConsent: true);
            var studentId = await SeedStudentAsync(db);
            var enrollment = await admin.RequestEnrollmentAsync(program.Id, studentId);
            await admin.GrantConsentAsync(enrollment.Id, "trip consent", grantedByUserId: 1);

            var trip = await admin.DefineTripAsync(program.Id, "Museum visit", staffRatioRequired: 10);
            await admin.ConfirmTransportAsync(trip.Id);
            await admin.AssignTripStaffAsync(trip.Id, assignedStaffCount: 1);

            return (program, trip, enrollment.Id);
        }

        [Fact]
        [BusinessRule("BR-ACT-004")]
        public async Task Departure_is_blocked_until_the_checklist_is_satisfied()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var program = await DefineFreeProgramAsync(admin, requiresConsent: true);
            var studentId = await SeedStudentAsync(db);
            await admin.RequestEnrollmentAsync(program.Id, studentId); // stays ConsentPending - no consent granted

            var trip = await admin.DefineTripAsync(program.Id, "Museum visit", staffRatioRequired: 10);
            await admin.AssignTripStaffAsync(trip.Id, assignedStaffCount: 1);
            // transport not confirmed either

            await Assert.ThrowsAsync<TripNotReadyForDepartureException>(() => admin.ConfirmDepartureAsync(trip.Id));
        }

        [Fact]
        [BusinessRule("BR-ACT-004")]
        public async Task Departure_succeeds_once_ratio_consent_and_transport_are_all_satisfied()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (_, trip, _) = await SeedReadyTripAsync(admin, db);

            await admin.ConfirmDepartureAsync(trip.Id);

            Assert.True(db.ActivityTrips.Single(t => t.Id == trip.Id).DepartureChecklistComplete);
        }

        [Fact]
        [BusinessRule("BR-ACT-004")]
        public async Task Return_headcount_mismatch_is_rejected()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (_, trip, _) = await SeedReadyTripAsync(admin, db);
            await admin.ConfirmDepartureAsync(trip.Id);

            await Assert.ThrowsAsync<TripHeadcountMismatchException>(() => admin.ConfirmReturnAsync(trip.Id, returnedHeadcount: 0));
        }

        [Fact]
        [BusinessRule("BR-ACT-004")]
        public async Task Return_with_matching_headcount_confirms()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (_, trip, _) = await SeedReadyTripAsync(admin, db);
            await admin.ConfirmDepartureAsync(trip.Id);

            await admin.ConfirmReturnAsync(trip.Id, returnedHeadcount: 1);

            Assert.True(db.ActivityTrips.Single(t => t.Id == trip.Id).ReturnHeadcountConfirmed);
        }

        // --- BR-ACT-003 attendance + BR-ACT-006 achievements -----------------------------

        [Fact]
        [BusinessRule("BR-ACT-003")]
        public async Task Capturing_attendance_creates_a_record()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var program = await DefineFreeProgramAsync(admin);
            var studentId = await SeedStudentAsync(db);
            var enrollment = await admin.RequestEnrollmentAsync(program.Id, studentId);
            var session = await admin.CreateSessionAsync(program.Id, new DateTime(2026, 10, 1));

            await admin.CaptureAttendanceAsync(session.Id, enrollment.Id, AttendanceStatus.Present);

            var attendance = db.ActivityAttendances.Single(a => a.ActivitySessionId == session.Id);
            Assert.Equal(AttendanceStatus.Present, attendance.Status);
        }

        [Fact]
        [BusinessRule("BR-ACT-006")]
        public async Task Recording_an_achievement_persists_the_title()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var studentId = await SeedStudentAsync(db);

            var achievement = await admin.RecordAchievementAsync(studentId, "First Place - Regional Chess Tournament", _clock.UtcNow);

            Assert.Equal("First Place - Regional Chess Tournament", db.Achievements.Single(a => a.Id == achievement.Id).Title);
        }
    }
}
