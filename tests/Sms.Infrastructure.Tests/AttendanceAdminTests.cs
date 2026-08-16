using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Attendance;
using Sms.Domain.Common;
using Sms.Domain.Grades;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Students;
using Sms.Infrastructure.Attendance;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// S3/E-301 (Attendance, doc/Modules/14, BR-ATD-002/003/005/006/007)
    /// over a real Sqlite-backed AppDbContext — Daily mode only.
    /// </summary>
    public sealed class AttendanceAdminTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 9, 15, 8, 0, 0, DateTimeKind.Utc);
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
        private int _yearId;
        private int _sectionId;
        private int _enrollmentId;

        public AttendanceAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

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

            var profile = new GradeYearProfile { GradeLevelId = grade.Id, AcademicYearId = year.Id, GenderPolicy = GenderPolicy.Mixed, TargetSections = 1, TargetSectionSize = 25 };
            db.GradeYearProfiles.Add(profile);
            db.SaveChanges();

            var student = new Student
            {
                StudentNo = "STU-TEST-1",
                FirstNameAr = "طالب", FatherNameAr = "أب", GrandfatherNameAr = "جد", FamilyNameAr = "عائلة",
                FirstNameEn = "Student", FatherNameEn = "Father", GrandfatherNameEn = "Grandfather", FamilyNameEn = "Family",
                Gender = Gender.Male, DateOfBirth = new DateTime(2018, 1, 1), NationalityLookupId = 1,
            };
            db.Students.Add(student);
            db.SaveChanges();

            var enrollment = new Enrollment
            {
                AcademicYearId = year.Id, StudentId = student.Id, GradeYearProfileId = profile.Id,
                EnrollmentDate = new DateTime(2026, 9, 1), SourceType = EnrollmentSourceType.Admission,
            };
            db.Enrollments.Add(enrollment);
            db.SaveChanges();

            var section = new Section
            {
                SchoolId = 1, AcademicYearId = year.Id, GradeYearProfileId = profile.Id,
                NameAr = "ثالث-أ", NameEn = "3-A", Capacity = 25, GenderPolicy = GenderPolicy.Mixed,
            };
            db.Sections.Add(section);
            db.SaveChanges();

            var membership = new SectionMembership
            {
                AcademicYearId = year.Id, SectionId = section.Id, EnrollmentId = enrollment.Id,
                EffectiveFromUtc = new DateTime(2026, 9, 1),
            };
            db.SectionMemberships.Add(membership);
            db.SaveChanges();

            _yearId = year.Id;
            _sectionId = section.Id;
            _enrollmentId = enrollment.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        // --- BR-ATD-002/003 capture ------------------------------------------------

        [Fact]
        [BusinessRule("BR-ATD-003")]
        public async Task Capturing_attendance_stamps_the_enrollments_current_section()
        {
            using var db = CreateContext();
            var admin = new AttendanceAdmin(db);

            var day = await admin.CaptureAsync(_enrollmentId, new DateTime(2026, 9, 15), AttendanceStatus.Present, capturedByUserId: 1);

            Assert.Equal(_sectionId, day.SectionId);
            Assert.Equal(_yearId, day.AcademicYearId);
        }

        [Fact]
        [BusinessRule("BR-ATD-003")]
        public async Task A_second_capture_for_the_same_enrollment_and_day_is_rejected()
        {
            using var db = CreateContext();
            var admin = new AttendanceAdmin(db);
            await admin.CaptureAsync(_enrollmentId, new DateTime(2026, 9, 15), AttendanceStatus.Present, capturedByUserId: 1);

            await Assert.ThrowsAsync<DuplicateAttendanceRecordException>(() =>
                admin.CaptureAsync(_enrollmentId, new DateTime(2026, 9, 15), AttendanceStatus.AbsentUnexcused, capturedByUserId: 1));
        }

        [Fact]
        [BusinessRule("BR-ATD-003")]
        public async Task Capturing_before_any_section_membership_is_rejected()
        {
            using var db = CreateContext();
            var admin = new AttendanceAdmin(db);

            await Assert.ThrowsAsync<NoSectionMembershipOnDateException>(() =>
                admin.CaptureAsync(_enrollmentId, new DateTime(2026, 1, 1), AttendanceStatus.Present, capturedByUserId: 1));
        }

        // --- BR-ATD-007 correction + closure ----------------------------------------

        [Fact]
        [BusinessRule("BR-ATD-007")]
        public async Task Correcting_status_requires_the_ambient_audit_reason()
        {
            using var db = CreateContext();
            var admin = new AttendanceAdmin(db);
            var day = await admin.CaptureAsync(_enrollmentId, new DateTime(2026, 9, 15), AttendanceStatus.Present, capturedByUserId: 1);

            _audit.Reason = null;
            await Assert.ThrowsAsync<MissingAuditReasonException>(() =>
                admin.CorrectAsync(day.Id, AttendanceStatus.AbsentUnexcused));
        }

        [Fact]
        [BusinessRule("BR-ATD-007")]
        public async Task Correcting_status_with_a_reason_succeeds()
        {
            using var db = CreateContext();
            var admin = new AttendanceAdmin(db);
            var day = await admin.CaptureAsync(_enrollmentId, new DateTime(2026, 9, 15), AttendanceStatus.Present, capturedByUserId: 1);

            _audit.Reason = "Teacher mis-marked, corrected same day";
            await admin.CorrectAsync(day.Id, AttendanceStatus.Late);

            Assert.Equal(AttendanceStatus.Late, db.AttendanceDays.Single(a => a.Id == day.Id).Status);
        }

        [Fact]
        [BusinessRule("BR-ATD-007")]
        public async Task Closing_a_day_locks_every_captured_row_for_that_date()
        {
            using var db = CreateContext();
            var admin = new AttendanceAdmin(db);
            var day = await admin.CaptureAsync(_enrollmentId, new DateTime(2026, 9, 15), AttendanceStatus.Present, capturedByUserId: 1);

            var lockedCount = await admin.CloseDayAsync(new DateTime(2026, 9, 15));

            Assert.Equal(1, lockedCount);
            Assert.True(db.AttendanceDays.Single(a => a.Id == day.Id).IsLocked);
        }

        // --- BR-ATD-005 justification ------------------------------------------------

        [Fact]
        [BusinessRule("BR-ATD-005")]
        public async Task Accepting_a_medical_justification_flips_the_day_to_medical_leave()
        {
            using var db = CreateContext();
            var admin = new AttendanceAdmin(db);
            var day = await admin.CaptureAsync(_enrollmentId, new DateTime(2026, 9, 15), AttendanceStatus.AbsentUnexcused, capturedByUserId: 1);
            var justification = await admin.SubmitJustificationAsync(day.Id, JustificationType.Medical, new DateTime(2026, 9, 16));

            _audit.Reason = "Accepted with document";
            await admin.ReviewJustificationAsync(justification.Id, accept: true, reviewedByUserId: 2, reviewedAtUtc: new DateTime(2026, 9, 16));

            Assert.Equal(AttendanceStatus.MedicalLeave, db.AttendanceDays.Single(a => a.Id == day.Id).Status);
            Assert.Equal(JustificationReviewState.Accepted, db.Justifications.Single(j => j.Id == justification.Id).ReviewState);
        }

        [Fact]
        [BusinessRule("BR-ATD-005")]
        public async Task Accepting_a_plain_excuse_flips_the_day_to_absent_excused()
        {
            using var db = CreateContext();
            var admin = new AttendanceAdmin(db);
            var day = await admin.CaptureAsync(_enrollmentId, new DateTime(2026, 9, 15), AttendanceStatus.AbsentUnexcused, capturedByUserId: 1);
            var justification = await admin.SubmitJustificationAsync(day.Id, JustificationType.Excuse, new DateTime(2026, 9, 16));

            _audit.Reason = "Accepted";
            await admin.ReviewJustificationAsync(justification.Id, accept: true, reviewedByUserId: 2, reviewedAtUtc: new DateTime(2026, 9, 16));

            Assert.Equal(AttendanceStatus.AbsentExcused, db.AttendanceDays.Single(a => a.Id == day.Id).Status);
        }

        [Fact]
        [BusinessRule("BR-ATD-005")]
        public async Task Rejecting_a_justification_leaves_the_day_unexcused_and_records_the_reason()
        {
            using var db = CreateContext();
            var admin = new AttendanceAdmin(db);
            var day = await admin.CaptureAsync(_enrollmentId, new DateTime(2026, 9, 15), AttendanceStatus.AbsentUnexcused, capturedByUserId: 1);
            var justification = await admin.SubmitJustificationAsync(day.Id, JustificationType.Excuse, new DateTime(2026, 9, 16));

            await admin.ReviewJustificationAsync(justification.Id, accept: false, reviewedByUserId: 2, reviewedAtUtc: new DateTime(2026, 9, 16), rejectionReason: "No document provided");

            Assert.Equal(AttendanceStatus.AbsentUnexcused, db.AttendanceDays.Single(a => a.Id == day.Id).Status);
            Assert.Equal("No document provided", db.Justifications.Single(j => j.Id == justification.Id).RejectionReason);
        }

        [Fact]
        [BusinessRule("BR-ATD-005")]
        public async Task Reviewing_an_already_reviewed_justification_is_rejected()
        {
            using var db = CreateContext();
            var admin = new AttendanceAdmin(db);
            var day = await admin.CaptureAsync(_enrollmentId, new DateTime(2026, 9, 15), AttendanceStatus.AbsentUnexcused, capturedByUserId: 1);
            var justification = await admin.SubmitJustificationAsync(day.Id, JustificationType.Excuse, new DateTime(2026, 9, 16));
            await admin.ReviewJustificationAsync(justification.Id, accept: false, reviewedByUserId: 2, reviewedAtUtc: new DateTime(2026, 9, 16));

            await Assert.ThrowsAsync<InvalidJustificationReviewException>(() =>
                admin.ReviewJustificationAsync(justification.Id, accept: true, reviewedByUserId: 2, reviewedAtUtc: new DateTime(2026, 9, 17)));
        }

        // --- BR-ATD-006 leave pass -----------------------------------------------------

        [Fact]
        [BusinessRule("BR-ATD-006")]
        public async Task A_leave_pass_moves_through_its_full_lifecycle()
        {
            using var db = CreateContext();
            var admin = new AttendanceAdmin(db);
            var pass = await admin.RequestLeavePassAsync(_enrollmentId, "Dentist appointment", new DateTime(2026, 9, 15, 9, 0, 0));

            await admin.ChangeLeavePassStatusAsync(pass.Id, LeavePassStatus.Approved, new DateTime(2026, 9, 15, 9, 5, 0));
            await admin.ChangeLeavePassStatusAsync(pass.Id, LeavePassStatus.Released, new DateTime(2026, 9, 15, 10, 0, 0));
            await admin.ChangeLeavePassStatusAsync(pass.Id, LeavePassStatus.Returned, new DateTime(2026, 9, 15, 12, 0, 0));

            var stored = db.LeavePasses.Single(l => l.Id == pass.Id);
            Assert.Equal(LeavePassStatus.Returned, stored.Status);
            Assert.Equal(new DateTime(2026, 9, 15, 10, 0, 0), stored.ReleasedAtUtc);
            Assert.Equal(new DateTime(2026, 9, 15, 12, 0, 0), stored.ReturnedAtUtc);
        }

        [Fact]
        [BusinessRule("BR-ATD-006")]
        public async Task Releasing_a_leave_pass_that_was_never_approved_is_rejected()
        {
            using var db = CreateContext();
            var admin = new AttendanceAdmin(db);
            var pass = await admin.RequestLeavePassAsync(_enrollmentId, "Dentist appointment", new DateTime(2026, 9, 15, 9, 0, 0));

            await Assert.ThrowsAsync<InvalidLeavePassTransitionException>(() =>
                admin.ChangeLeavePassStatusAsync(pass.Id, LeavePassStatus.Released, new DateTime(2026, 9, 15, 10, 0, 0)));
        }

        // --- BR-ATD-004 gate events --------------------------------------------------

        [Fact]
        [BusinessRule("BR-ATD-004")]
        public async Task Recording_a_gate_event_logs_it_against_the_enrollment()
        {
            using var db = CreateContext();
            var admin = new AttendanceAdmin(db);

            var gateEvent = await admin.RecordGateEventAsync(
                _enrollmentId, GateEventType.EarlyLeaveRelease, new DateTime(2026, 9, 15, 11, 0, 0),
                pickupPersonName: "Uncle Ahmad", isAuthorizedPickupOverride: true, releasedByUserId: 3);

            var stored = db.GateEvents.Single(g => g.Id == gateEvent.Id);
            Assert.Equal(_enrollmentId, stored.EnrollmentId);
            Assert.True(stored.IsAuthorizedPickupOverride);
        }
    }
}
