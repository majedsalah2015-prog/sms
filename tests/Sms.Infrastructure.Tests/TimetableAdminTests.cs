using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Calendar;
using Sms.Domain.Classrooms;
using Sms.Domain.Common;
using Sms.Domain.Employees;
using Sms.Domain.Grades;
using Sms.Domain.Numbering;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Subjects;
using Sms.Domain.Teachers;
using Sms.Domain.Timetable;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Employees;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Teachers;
using Sms.Infrastructure.Timetable;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// S4/E-401 (Timetable, doc/Modules/15, BR-TTB-001..009) over a real
    /// Sqlite-backed AppDbContext. Assisted-manual v1 (no solver) —
    /// covers placement conflicts, completeness/WF-12, session
    /// generation (reusing E-103's CalendarDayResolver), and daily cover.
    /// </summary>
    public sealed class TimetableAdminTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2027, 8, 15, 8, 0, 0, DateTimeKind.Utc);
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
        private int _stageId;
        private int _sectionId;
        private int _sectionBId;
        private int _offeringId;
        private int _shapeId;
        private int _slotAId;
        private int _slotBId;

        public TimetableAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            db.NumberingSeries.Add(new NumberingSeries
            {
                Code = "EMP", EntityName = "Employee", FormatTemplate = "EMP-{SEQ:5}",
                ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Normal, EffectiveFromUtc = _clock.UtcNow, IsActive = true,
            });

            var year = new AcademicYear
            {
                LabelAr = "٢٠٢٧-٢٠٢٨", LabelEn = "2027-2028", HijriLabel = "١٤٤٩هـ",
                StartDate = new DateTime(2027, 9, 1), EndDate = new DateTime(2028, 6, 30), Status = AcademicYearStatus.Active,
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

            var subject = new Subject { SchoolId = 1, Code = "MATH", Name = new LocalizedName("رياضيات", "Math"), Category = "core" };
            db.Subjects.Add(subject);
            db.SaveChanges();

            var offering = new CurriculumOffering
            {
                SchoolId = 1, AcademicYearId = year.Id, GradeYearProfileId = profile.Id, SubjectId = subject.Id,
                WeeklyPeriods = 2, IsAssessable = true, GpaWeight = 1m, EffectiveFromUtc = year.StartDate,
            };
            db.CurriculumOfferings.Add(offering);

            var section = new Section { SchoolId = 1, AcademicYearId = year.Id, GradeYearProfileId = profile.Id, NameAr = "ثالث-أ", NameEn = "3-A", Capacity = 25, GenderPolicy = GenderPolicy.Mixed };
            var sectionB = new Section { SchoolId = 1, AcademicYearId = year.Id, GradeYearProfileId = profile.Id, NameAr = "ثالث-ب", NameEn = "3-B", Capacity = 25, GenderPolicy = GenderPolicy.Mixed };
            db.Sections.Add(section);
            db.Sections.Add(sectionB);
            db.SaveChanges();

            var shape = new TimetableShape { StageId = stage.Id, AcademicYearId = year.Id };
            db.TimetableShapes.Add(shape);
            db.SaveChanges();

            var dayA = year.StartDate.DayOfWeek;
            var dayB = (DayOfWeek)(((int)dayA + 1) % 7);
            var slotA = new PeriodSlot { TimetableShapeId = shape.Id, DayOfWeek = dayA, SequenceNumber = 1, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(8, 45, 0) };
            var slotB = new PeriodSlot { TimetableShapeId = shape.Id, DayOfWeek = dayB, SequenceNumber = 1, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(8, 45, 0) };
            db.PeriodSlots.Add(slotA);
            db.PeriodSlots.Add(slotB);
            db.SaveChanges();

            _yearId = year.Id;
            _stageId = stage.Id;
            _sectionId = section.Id;
            _sectionBId = sectionB.Id;
            _offeringId = offering.Id;
            _shapeId = shape.Id;
            _slotAId = slotA.Id;
            _slotBId = slotB.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private async Task<int> RegisterTeacherAsync(AppDbContext db, string suffix)
        {
            var employeeAdmin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var employee = await employeeAdmin.RegisterEmployeeAsync(
                "معلم" + suffix, "أب", "جد", "عائلة", "Teacher" + suffix, "Father", "Grandfather", "Family",
                Gender.Male, new DateTime(1985, 1, 1), nationalityLookupId: 1);
            var contract = await employeeAdmin.DefineContractAsync(
                employee.Id, ContractType.FullTime, _clock.UtcNow.Date, new DateTime(2028, 6, 30), salaryBasic: 9000m);
            await employeeAdmin.ChangeContractStatusAsync(contract.Id, ContractStatus.Active);

            var teacherAdmin = new TeacherAdmin(db, _clock);
            var profile = await teacherAdmin.DesignateTeacherAsync(employee.Id, maxWeeklyPeriods: 24);
            return profile.Id;
        }

        private async Task AssignAsync(AppDbContext db, int teacherProfileId, int sectionId)
        {
            var teacherAdmin = new TeacherAdmin(db, _clock);
            await teacherAdmin.AssignAsync(teacherProfileId, _offeringId, sectionId, TeacherRole.Primary, _clock.UtcNow.Date);
        }

        // --- BR-TCH-002 / BR-TTB-004 placement gates --------------------------------

        [Fact]
        [BusinessRule("BR-TCH-002")]
        public async Task Placing_without_a_matching_teacher_assignment_is_rejected()
        {
            using var db = CreateContext();
            var teacherId = await RegisterTeacherAsync(db, "1"); // not assigned to the offering
            var timetableAdmin = new TimetableAdmin(db, _clock);
            var version = await timetableAdmin.DefineVersionAsync(_yearId);

            await Assert.ThrowsAsync<TeacherNotAssignedException>(() =>
                timetableAdmin.PlaceAsync(version.Id, _sectionId, _slotAId, _offeringId, teacherId));
        }

        [Fact]
        [BusinessRule("BR-TTB-003")]
        public async Task Placing_with_a_matching_assignment_succeeds()
        {
            using var db = CreateContext();
            var teacherId = await RegisterTeacherAsync(db, "1");
            await AssignAsync(db, teacherId, _sectionId);
            var timetableAdmin = new TimetableAdmin(db, _clock);
            var version = await timetableAdmin.DefineVersionAsync(_yearId);

            var placement = await timetableAdmin.PlaceAsync(version.Id, _sectionId, _slotAId, _offeringId, teacherId);

            Assert.Equal(_sectionId, placement.SectionId);
        }

        [Fact]
        [BusinessRule("BR-TTB-004")]
        public async Task A_teacher_cannot_be_placed_twice_at_the_same_slot()
        {
            using var db = CreateContext();
            var teacherId = await RegisterTeacherAsync(db, "1");
            await AssignAsync(db, teacherId, _sectionId);
            await AssignAsync(db, teacherId, _sectionBId);
            var timetableAdmin = new TimetableAdmin(db, _clock);
            var version = await timetableAdmin.DefineVersionAsync(_yearId);
            await timetableAdmin.PlaceAsync(version.Id, _sectionId, _slotAId, _offeringId, teacherId);

            await Assert.ThrowsAsync<PlacementConflictException>(() =>
                timetableAdmin.PlaceAsync(version.Id, _sectionBId, _slotAId, _offeringId, teacherId));
        }

        // --- BR-TTB-002/003 validate + publish ---------------------------------------

        [Fact]
        [BusinessRule("BR-TTB-003")]
        public async Task Validating_an_incomplete_version_is_rejected()
        {
            using var db = CreateContext();
            var teacherId = await RegisterTeacherAsync(db, "1");
            await AssignAsync(db, teacherId, _sectionId);
            var timetableAdmin = new TimetableAdmin(db, _clock);
            var version = await timetableAdmin.DefineVersionAsync(_yearId);
            await timetableAdmin.PlaceAsync(version.Id, _sectionId, _slotAId, _offeringId, teacherId); // 1 of 2 weekly periods

            await Assert.ThrowsAsync<IncompletePlacementException>(() => timetableAdmin.ValidateVersionAsync(version.Id));
        }

        [Fact]
        [BusinessRule("BR-TTB-002")]
        public async Task Publishing_a_complete_version_generates_sessions_on_working_days_only()
        {
            using var db = CreateContext();
            var teacherId = await RegisterTeacherAsync(db, "1");
            await AssignAsync(db, teacherId, _sectionId);
            var timetableAdmin = new TimetableAdmin(db, _clock);
            var version = await timetableAdmin.DefineVersionAsync(_yearId);
            await timetableAdmin.PlaceAsync(version.Id, _sectionId, _slotAId, _offeringId, teacherId);
            await timetableAdmin.PlaceAsync(version.Id, _sectionId, _slotBId, _offeringId, teacherId);
            await timetableAdmin.ValidateVersionAsync(version.Id);

            var rangeStart = db.AcademicYears.Single().StartDate;
            var rangeEnd = rangeStart.AddDays(13); // two weeks -> 2 occurrences per weekday, minus 1 holiday below

            // Mark the first occurrence of slotA's weekday as a holiday - it should be skipped.
            db.CalendarDays.Add(new CalendarDay { AcademicYearId = _yearId, Date = rangeStart, DayType = DayType.Holiday, Source = CalendarDaySource.Manual });
            db.SaveChanges();

            await timetableAdmin.PublishAsync(version.Id, publishedByUserId: 1, rangeStart, rangeEnd, weekendDays: new HashSet<DayOfWeek>());

            Assert.Equal(TimetableVersionStatus.Published, db.TimetableVersions.Single(v => v.Id == version.Id).Status);
            // slotA: 2 occurrences - 1 holiday = 1 session; slotB: 2 occurrences = 2 sessions.
            Assert.Equal(3, db.Sessions.Count());
            Assert.DoesNotContain(db.Sessions, s => s.Date == rangeStart);
        }

        // --- BR-TTB-007 daily cover --------------------------------------------------

        private async Task<(TimetableAdmin admin, TimetableVersion version, int primaryTeacherId)> PublishedVersionAsync(AppDbContext db)
        {
            var teacherId = await RegisterTeacherAsync(db, "1");
            await AssignAsync(db, teacherId, _sectionId);
            var timetableAdmin = new TimetableAdmin(db, _clock);
            var version = await timetableAdmin.DefineVersionAsync(_yearId);
            await timetableAdmin.PlaceAsync(version.Id, _sectionId, _slotAId, _offeringId, teacherId);
            await timetableAdmin.PlaceAsync(version.Id, _sectionId, _slotBId, _offeringId, teacherId);
            await timetableAdmin.ValidateVersionAsync(version.Id);

            var rangeStart = db.AcademicYears.Single().StartDate;
            await timetableAdmin.PublishAsync(version.Id, publishedByUserId: 1, rangeStart, rangeStart.AddDays(6), weekendDays: new HashSet<DayOfWeek>());

            return (timetableAdmin, version, teacherId);
        }

        [Fact]
        [BusinessRule("BR-TTB-007")]
        public async Task An_unqualified_substitute_without_supervise_only_is_rejected()
        {
            using var db = CreateContext();
            var (timetableAdmin, _, _) = await PublishedVersionAsync(db);
            var substituteId = await RegisterTeacherAsync(db, "2"); // never assigned to the offering
            var session = db.Sessions.First();

            await Assert.ThrowsAsync<SubstituteNotEligibleException>(() =>
                timetableAdmin.AssignSubstituteAsync(session.Id, substituteId, "sick leave"));
        }

        [Fact]
        [BusinessRule("BR-TTB-007")]
        public async Task An_unqualified_substitute_with_supervise_only_is_accepted()
        {
            using var db = CreateContext();
            var (timetableAdmin, _, _) = await PublishedVersionAsync(db);
            var substituteId = await RegisterTeacherAsync(db, "2");
            var session = db.Sessions.First();

            await timetableAdmin.AssignSubstituteAsync(session.Id, substituteId, "sick leave", allowSuperviseOnly: true);

            Assert.Equal(SessionStatus.Substituted, db.Sessions.Single(s => s.Id == session.Id).Status);
        }

        [Fact]
        [BusinessRule("BR-TTB-007")]
        public async Task A_qualified_and_free_substitute_is_accepted_without_supervise_only()
        {
            using var db = CreateContext();
            var (timetableAdmin, _, _) = await PublishedVersionAsync(db);
            var substituteId = await RegisterTeacherAsync(db, "2");
            await AssignAsync(db, substituteId, _sectionBId); // qualified: assigned to the same offering elsewhere, never placed
            var session = db.Sessions.First();

            var substitution = await timetableAdmin.AssignSubstituteAsync(session.Id, substituteId, "sick leave");

            Assert.Equal(substituteId, substitution.SubstituteTeacherProfileId);
        }

        // --- BR-TTB-008/009 room change + cancellation --------------------------------

        [Fact]
        [BusinessRule("BR-TTB-008")]
        public async Task Changing_a_sessions_room_flips_status_and_records_the_override()
        {
            using var db = CreateContext();
            var (timetableAdmin, _, _) = await PublishedVersionAsync(db);
            var building = new Building { Name = new LocalizedName("مبنى أ", "Building A") };
            db.Buildings.Add(building);
            db.SaveChanges();
            var floor = new Floor { BuildingId = building.Id, Name = new LocalizedName("الأرضي", "Ground"), SequenceOrder = 1 };
            db.Floors.Add(floor);
            db.SaveChanges();
            var room = new Room
            {
                FloorId = floor.Id, Code = "R101", Name = new LocalizedName("قاعة ١٠١", "Room 101"),
                RoomTypeLookupId = 1, StandardCapacity = 30, ExamCapacity = 20, WingTag = GenderPolicy.Mixed,
            };
            db.Rooms.Add(room);
            db.SaveChanges();
            var session = db.Sessions.First();

            await timetableAdmin.ChangeSessionRoomAsync(session.Id, room.Id, "AC maintenance");

            var updated = db.Sessions.Single(s => s.Id == session.Id);
            Assert.Equal(SessionStatus.RoomChanged, updated.Status);
            Assert.Equal(room.Id, updated.OverrideRoomId);
        }

        [Fact]
        [BusinessRule("BR-TTB-009")]
        public async Task Cancelling_a_session_records_the_reason()
        {
            using var db = CreateContext();
            var (timetableAdmin, _, _) = await PublishedVersionAsync(db);
            var session = db.Sessions.First();

            await timetableAdmin.CancelSessionAsync(session.Id, "school trip");

            var updated = db.Sessions.Single(s => s.Id == session.Id);
            Assert.Equal(SessionStatus.Cancelled, updated.Status);
            Assert.Equal("school trip", updated.ChangeReason);
        }
    }
}
