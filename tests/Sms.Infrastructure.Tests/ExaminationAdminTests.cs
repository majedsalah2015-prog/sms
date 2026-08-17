using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Attendance;
using Sms.Domain.Classrooms;
using Sms.Domain.Common;
using Sms.Domain.Examinations;
using Sms.Domain.Grades;
using Sms.Domain.Grading;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Students;
using Sms.Domain.Subjects;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Examinations;
using Sms.Infrastructure.Grading;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// S4/E-402 (Examinations, doc/Modules/16, BR-EXM-002/003/004/006/008)
    /// over a real Sqlite-backed AppDbContext. Marks capture reuses
    /// E-302's GradingAdmin directly (the "single marks store" mandate).
    /// </summary>
    public sealed class ExaminationAdminTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2027, 1, 20, 8, 0, 0, DateTimeKind.Utc);
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
        private int _termId;
        private int _yearId;
        private int _gradeYearProfileId;
        private int _sectionId;
        private int _offeringId;
        private int _roomId;
        private int _enrollmentId;
        private int _blueprintComponentId;
        private int _marksheetId;
        private int _examTypeId;

        public ExaminationAdminTests()
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
            var stage = new Stage { Name = new LocalizedName("Stage", "Elementary"), SequenceOrder = 1, DefaultGenderPolicy = GenderPolicy.Mixed };
            db.Stages.Add(stage);
            db.SaveChanges();

            var grade = new GradeLevel { StageId = stage.Id, Code = "G3", Name = new LocalizedName("Grade", "Grade 3"), SequenceOrder = 3 };
            db.GradeLevels.Add(grade);
            db.SaveChanges();

            var profile = new GradeYearProfile { GradeLevelId = grade.Id, AcademicYearId = year.Id, GenderPolicy = GenderPolicy.Mixed, TargetSections = 1, TargetSectionSize = 25 };
            db.GradeYearProfiles.Add(profile);
            db.SaveChanges();

            var subject = new Subject { SchoolId = 1, Code = "MATH", Name = new LocalizedName("Subject", "Math"), Category = "core" };
            db.Subjects.Add(subject);
            db.SaveChanges();

            var offering = new CurriculumOffering
            {
                SchoolId = 1, AcademicYearId = year.Id, GradeYearProfileId = profile.Id, SubjectId = subject.Id,
                WeeklyPeriods = 5, IsAssessable = true, GpaWeight = 1m, EffectiveFromUtc = new DateTime(2026, 9, 1),
            };
            db.CurriculumOfferings.Add(offering);

            var section = new Section { SchoolId = 1, AcademicYearId = year.Id, GradeYearProfileId = profile.Id, NameAr = "Section", NameEn = "3-A", Capacity = 25, GenderPolicy = GenderPolicy.Mixed };
            db.Sections.Add(section);
            db.SaveChanges();

            var semester = new Semester { AcademicYearId = year.Id, SequenceNumber = 1, NameAr = "S1", NameEn = "Semester 1", StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 1, 31) };
            db.Semesters.Add(semester);
            db.SaveChanges();

            var term = new Term { AcademicYearId = year.Id, SemesterId = semester.Id, SequenceNumber = 1, NameAr = "T1", NameEn = "Term 1", StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2026, 11, 30) };
            db.Terms.Add(term);
            db.SaveChanges();

            var building = new Building { Name = new LocalizedName("Building", "Building A") };
            db.Buildings.Add(building);
            db.SaveChanges();
            var floor = new Floor { BuildingId = building.Id, Name = new LocalizedName("Floor", "Ground"), SequenceOrder = 1 };
            db.Floors.Add(floor);
            db.SaveChanges();
            var room = new Room
            {
                FloorId = floor.Id, Code = "R101", Name = new LocalizedName("Room", "Room 101"),
                RoomTypeLookupId = 1, StandardCapacity = 30, ExamCapacity = 2, WingTag = GenderPolicy.Mixed,
            };
            db.Rooms.Add(room);
            db.SaveChanges();

            var student = new Student
            {
                StudentNo = "STU-TEST-1",
                FirstNameAr = "Student", FatherNameAr = "Father", GrandfatherNameAr = "Grandfather", FamilyNameAr = "Family",
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

            db.SectionMemberships.Add(new SectionMembership
            {
                AcademicYearId = year.Id, SectionId = section.Id, EnrollmentId = enrollment.Id, EffectiveFromUtc = new DateTime(2026, 9, 1),
            });
            db.SaveChanges();

            // A finalized Blueprint + a Draft Marksheet, so the exam has something real to link/write into.
            var gradingAdmin = new GradingAdmin(db, _clock, _audit);
            var scale = gradingAdmin.DefineScaleAsync(stage.Id, "Scale", "Percentage").GetAwaiter().GetResult();
            gradingAdmin.AddScaleBandAsync(scale.Id, 0m, 100m, "P", "Pass", "Pass", isPassing: true, sortOrder: 1).GetAwaiter().GetResult();
            var blueprint = gradingAdmin.DefineBlueprintAsync(offering.Id, term.Id, scale.Id).GetAwaiter().GetResult();
            var component = gradingAdmin.AddBlueprintComponentAsync(blueprint.Id, "Final", "Final", weight: 100m, maxScore: 100m).GetAwaiter().GetResult();
            gradingAdmin.LockBlueprintAsync(blueprint.Id).GetAwaiter().GetResult();
            var marksheet = gradingAdmin.CreateMarksheetAsync(blueprint.Id, section.Id).GetAwaiter().GetResult();

            var examAdmin = new ExaminationAdmin(db, _clock);
            var examType = examAdmin.DefineExamTypeAsync("Final", "Final", isScheduled: true, isMakeupEligible: true).GetAwaiter().GetResult();

            _termId = term.Id;
            _yearId = year.Id;
            _gradeYearProfileId = profile.Id;
            _sectionId = section.Id;
            _offeringId = offering.Id;
            _roomId = room.Id;
            _enrollmentId = enrollment.Id;
            _blueprintComponentId = component.Id;
            _marksheetId = marksheet.Id;
            _examTypeId = examType.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private async Task<Exam> ScheduleExamAsync(ExaminationAdmin admin, int examRoundId)
            => await admin.ScheduleExamAsync(
                examRoundId, _examTypeId, _offeringId, _gradeYearProfileId, _blueprintComponentId,
                new DateTime(2026, 11, 15), new TimeSpan(9, 0, 0), durationMinutes: 90);

        // --- BR-EXM-002 blueprint linkage --------------------------------------------

        [Fact]
        [BusinessRule("BR-EXM-002")]
        public async Task Scheduling_against_a_mismatched_offering_is_rejected()
        {
            using var db = CreateContext();
            var admin = new ExaminationAdmin(db, _clock);
            var round = await admin.DefineRoundAsync(_yearId, _termId, "Round", "Round");

            await Assert.ThrowsAsync<ExamBlueprintMismatchException>(() =>
                admin.ScheduleExamAsync(round.Id, _examTypeId, curriculumOfferingId: 9999, _gradeYearProfileId, _blueprintComponentId,
                    new DateTime(2026, 11, 15), new TimeSpan(9, 0, 0), durationMinutes: 90));
        }

        // --- BR-EXM-003 schedule clash -------------------------------------------------

        [Fact]
        [BusinessRule("BR-EXM-003")]
        public async Task A_second_exam_for_the_same_grade_year_on_the_same_day_clashes_by_default()
        {
            using var db = CreateContext();
            var admin = new ExaminationAdmin(db, _clock);
            var round = await admin.DefineRoundAsync(_yearId, _termId, "Round", "Round");
            await ScheduleExamAsync(admin, round.Id);

            await Assert.ThrowsAsync<ExamScheduleClashException>(() => admin.ScheduleExamAsync(
                round.Id, _examTypeId, _offeringId, _gradeYearProfileId, _blueprintComponentId,
                new DateTime(2026, 11, 15), new TimeSpan(11, 0, 0), durationMinutes: 90));
        }

        // --- BR-EXM §4 round WF -------------------------------------------------------

        [Fact]
        [BusinessRule("BR-EXM-003")]
        public async Task Publishing_a_draft_round_directly_is_rejected()
        {
            using var db = CreateContext();
            var admin = new ExaminationAdmin(db, _clock);
            var round = await admin.DefineRoundAsync(_yearId, _termId, "Round", "Round");

            await Assert.ThrowsAsync<InvalidExamRoundStatusTransitionException>(() =>
                admin.PublishRoundAsync(round.Id, publishedByUserId: 1));
        }

        [Fact]
        [BusinessRule("BR-EXM-003")]
        public async Task Validate_then_publish_succeeds()
        {
            using var db = CreateContext();
            var admin = new ExaminationAdmin(db, _clock);
            var round = await admin.DefineRoundAsync(_yearId, _termId, "Round", "Round");

            await admin.ValidateRoundAsync(round.Id);
            await admin.PublishRoundAsync(round.Id, publishedByUserId: 1);

            Assert.Equal(ExamRoundStatus.Published, db.ExamRounds.Single(r => r.Id == round.Id).Status);
        }

        // --- BR-EXM-004 seating capacity -----------------------------------------------

        [Fact]
        [BusinessRule("BR-EXM-004")]
        public async Task Seating_beyond_room_exam_capacity_is_rejected()
        {
            using var db = CreateContext();
            var admin = new ExaminationAdmin(db, _clock);
            var round = await admin.DefineRoundAsync(_yearId, _termId, "Round", "Round");
            var exam = await ScheduleExamAsync(admin, round.Id);
            var sitting = await admin.CreateSittingAsync(exam.Id, _roomId);
            await admin.SeatStudentAsync(sitting.Id, _enrollmentId); // room ExamCapacity = 2

            var studentAdmin2Enrollment = await SeedSecondStudentEnrollmentAsync(db);
            await admin.SeatStudentAsync(sitting.Id, studentAdmin2Enrollment); // fills capacity (2/2)

            var thirdEnrollment = await SeedSecondStudentEnrollmentAsync(db, "3");
            await Assert.ThrowsAsync<SittingFullException>(() => admin.SeatStudentAsync(sitting.Id, thirdEnrollment));
        }

        private async Task<int> SeedSecondStudentEnrollmentAsync(AppDbContext db, string suffix = "2")
        {
            var student = new Student
            {
                StudentNo = "STU-TEST-" + suffix,
                FirstNameAr = "S", FatherNameAr = "F", GrandfatherNameAr = "G", FamilyNameAr = "Fam",
                FirstNameEn = "S", FatherNameEn = "F", GrandfatherNameEn = "G", FamilyNameEn = "Fam",
                Gender = Gender.Male, DateOfBirth = new DateTime(2018, 1, 1), NationalityLookupId = 1,
            };
            db.Students.Add(student);
            await db.SaveChangesAsync();
            var enrollment = new Enrollment
            {
                AcademicYearId = _yearId, StudentId = student.Id, GradeYearProfileId = _gradeYearProfileId,
                EnrollmentDate = new DateTime(2026, 9, 1), SourceType = EnrollmentSourceType.Admission,
            };
            db.Enrollments.Add(enrollment);
            await db.SaveChangesAsync();
            return enrollment.Id;
        }

        // --- BR-EXM-006/008 attendance -> makeup eligibility / mark zeroing ------------

        [Fact]
        [BusinessRule("BR-EXM-008")]
        public async Task Excused_absence_creates_system_derived_makeup_eligibility()
        {
            using var db = CreateContext();
            var admin = new ExaminationAdmin(db, _clock);
            var round = await admin.DefineRoundAsync(_yearId, _termId, "Round", "Round");
            var exam = await ScheduleExamAsync(admin, round.Id);
            var sitting = await admin.CreateSittingAsync(exam.Id, _roomId);
            await admin.SeatStudentAsync(sitting.Id, _enrollmentId);

            await admin.RecordExamAttendanceAsync(sitting.Id, _enrollmentId, AttendanceStatus.AbsentExcused);

            var eligibility = db.MakeupEligibilities.Single(m => m.ExamId == exam.Id && m.EnrollmentId == _enrollmentId);
            Assert.True(eligibility.IsSystemDerived);
        }

        [Fact]
        [BusinessRule("BR-EXM-006")]
        public async Task Unexcused_absence_zeroes_the_marksheet_entry()
        {
            using var db = CreateContext();
            var admin = new ExaminationAdmin(db, _clock);
            var round = await admin.DefineRoundAsync(_yearId, _termId, "Round", "Round");
            var exam = await ScheduleExamAsync(admin, round.Id);
            var sitting = await admin.CreateSittingAsync(exam.Id, _roomId);
            await admin.SeatStudentAsync(sitting.Id, _enrollmentId);

            await admin.RecordExamAttendanceAsync(sitting.Id, _enrollmentId, AttendanceStatus.AbsentUnexcused);

            var markEntry = db.MarkEntries.Single(e => e.MarksheetId == _marksheetId && e.EnrollmentId == _enrollmentId);
            Assert.Equal(0m, markEntry.Score);
            Assert.True(markEntry.IsAbsent);
        }

        [Fact]
        [BusinessRule("BR-EXM-006")]
        public async Task Recording_attendance_for_an_unseated_student_is_rejected()
        {
            using var db = CreateContext();
            var admin = new ExaminationAdmin(db, _clock);
            var round = await admin.DefineRoundAsync(_yearId, _termId, "Round", "Round");
            var exam = await ScheduleExamAsync(admin, round.Id);
            var sitting = await admin.CreateSittingAsync(exam.Id, _roomId);

            await Assert.ThrowsAsync<StudentNotSeatedException>(() =>
                admin.RecordExamAttendanceAsync(sitting.Id, _enrollmentId, AttendanceStatus.Present));
        }

        // --- BR-EXM-007 incidents -------------------------------------------------------

        [Fact]
        [BusinessRule("BR-EXM-007")]
        public async Task Recording_an_incident_persists_the_narrative()
        {
            using var db = CreateContext();
            var admin = new ExaminationAdmin(db, _clock);
            var round = await admin.DefineRoundAsync(_yearId, _termId, "Round", "Round");
            var exam = await ScheduleExamAsync(admin, round.Id);
            var sitting = await admin.CreateSittingAsync(exam.Id, _roomId);

            var incident = await admin.RecordIncidentAsync(sitting.Id, _enrollmentId, "cheating", "found notes in pocket", recordedByUserId: 1);

            Assert.Equal("found notes in pocket", db.ExamIncidents.Single(i => i.Id == incident.Id).Narrative);
        }
    }
}
