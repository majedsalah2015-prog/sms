using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Attachments;
using Sms.Domain.Common;
using Sms.Domain.Employees;
using Sms.Domain.Grades;
using Sms.Domain.Learning;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Subjects;
using Sms.Domain.Teachers;
using Sms.Domain.Timetable;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Learning;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// Module 37 slice 1 (doc/Modules/37 §8.1-2, BR-LRN-001/002/003/006/016)
    /// over a real Sqlite-backed AppDbContext.
    /// </summary>
    public sealed class LessonAdminTests : IDisposable
    {
        private const int TeacherUserId = 500;
        private const int HeadOfDepartmentUserId = 600;
        private const int StrangerUserId = 700;

        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2027, 1, 20, 8, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; } = TeacherUserId;
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

        private readonly int _yearId;
        private readonly int _mathOfferingId;
        private readonly int _artOfferingId;
        private readonly int _sectionId;
        private readonly int _mathSessionId;
        private readonly int _artSessionId;
        private readonly int _attachmentId;

        public LessonAdminTests()
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

            // The art department is headed by a different user — the HoD reach test rides this.
            var artDepartment = new Department { SchoolId = 1, Name = new LocalizedName("الفنون", "Arts"), HeadTeacherUserId = HeadOfDepartmentUserId };
            db.Departments.Add(artDepartment);
            db.SaveChanges();

            var math = new Subject { SchoolId = 1, Code = "MATH", Name = new LocalizedName("رياضيات", "Math"), Category = "core" };
            var art = new Subject { SchoolId = 1, Code = "ART", Name = new LocalizedName("فنون", "Art"), Category = "core", DepartmentId = artDepartment.Id };
            db.Subjects.AddRange(math, art);
            db.SaveChanges();

            var mathOffering = NewOffering(year.Id, profile.Id, math.Id);
            var artOffering = NewOffering(year.Id, profile.Id, art.Id);
            db.CurriculumOfferings.AddRange(mathOffering, artOffering);

            var section = new Section { SchoolId = 1, AcademicYearId = year.Id, GradeYearProfileId = profile.Id, NameAr = "ثالث-أ", NameEn = "3-A", Capacity = 25, GenderPolicy = GenderPolicy.Mixed };
            db.Sections.Add(section);
            db.SaveChanges();

            var employee = new Employee
            {
                SchoolId = 1, EmployeeNo = "EMP-1", UserAccountId = TeacherUserId,
                FirstNameAr = "معلم", FatherNameAr = "أب", GrandfatherNameAr = "جد", FamilyNameAr = "عائلة",
                FirstNameEn = "Teacher", FatherNameEn = "Father", GrandfatherNameEn = "Grandfather", FamilyNameEn = "Family",
                Gender = Gender.Male, DateOfBirth = new DateTime(1990, 1, 1), NationalityLookupId = 1,
            };
            db.Employees.Add(employee);
            db.SaveChanges();

            var teacher = new TeacherProfile { SchoolId = 1, EmployeeId = employee.Id, MaxWeeklyPeriods = 24 };
            db.TeacherProfiles.Add(teacher);

            var shape = new TimetableShape { SchoolId = 1, AcademicYearId = year.Id, StageId = stage.Id };
            db.TimetableShapes.Add(shape);
            db.SaveChanges();

            var slot = new PeriodSlot
            {
                SchoolId = 1, TimetableShapeId = shape.Id, DayOfWeek = DayOfWeek.Sunday, SequenceNumber = 1,
                StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(8, 45, 0),
            };
            db.PeriodSlots.Add(slot);

            var published = new TimetableVersion
            {
                SchoolId = 1, AcademicYearId = year.Id, Status = TimetableVersionStatus.Published,
                PublishedAtUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            };
            db.TimetableVersions.Add(published);
            db.SaveChanges();

            // The teacher holds Math in 3-A. They hold no placement on Art.
            var mathPlacement = new Placement
            {
                SchoolId = 1, TimetableVersionId = published.Id, SectionId = section.Id,
                PeriodSlotId = slot.Id, CurriculumOfferingId = mathOffering.Id, TeacherProfileId = teacher.Id,
            };

            // An Art placement exists but belongs to nobody this test signs in as,
            // so the Art session below is a real session that does not teach Math.
            var artPlacement = new Placement
            {
                SchoolId = 1, TimetableVersionId = published.Id, SectionId = section.Id,
                PeriodSlotId = slot.Id, CurriculumOfferingId = artOffering.Id, TeacherProfileId = teacher.Id + 99,
            };
            db.Placements.AddRange(mathPlacement, artPlacement);
            db.SaveChanges();

            var mathSession = new Session { SchoolId = 1, AcademicYearId = year.Id, PlacementId = mathPlacement.Id, Date = new DateTime(2026, 9, 6) };
            var artSession = new Session { SchoolId = 1, AcademicYearId = year.Id, PlacementId = artPlacement.Id, Date = new DateTime(2026, 9, 6) };
            db.Sessions.AddRange(mathSession, artSession);

            var documentType = new DocumentType { SchoolId = 1, Code = "LESSON-RES", ModuleCode = "LRN", Name = new LocalizedName("مورد درس", "Lesson resource") };
            db.DocumentTypes.Add(documentType);
            db.SaveChanges();

            var attachment = new Attachment
            {
                SchoolId = 1, DocumentTypeId = documentType.Id, OwningEntityType = "Lesson", OwningEntityId = 0,
                Status = AttachmentStatus.PendingScan, CurrentVersionNumber = 1,
            };
            db.Attachments.Add(attachment);
            db.SaveChanges();

            _yearId = year.Id;
            _mathOfferingId = mathOffering.Id;
            _artOfferingId = artOffering.Id;
            _sectionId = section.Id;
            _mathSessionId = mathSession.Id;
            _artSessionId = artSession.Id;
            _attachmentId = attachment.Id;
        }

        public void Dispose() => _connection.Dispose();

        private static CurriculumOffering NewOffering(int yearId, int profileId, int subjectId) => new()
        {
            SchoolId = 1, AcademicYearId = yearId, GradeYearProfileId = profileId, SubjectId = subjectId,
            WeeklyPeriods = 5, IsAssessable = true, GpaWeight = 1m, EffectiveFromUtc = new DateTime(2026, 9, 1),
        };

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private LessonAdmin CreateAdmin(AppDbContext db) => new(db, _clock, _user);

        private async Task<Lesson> CreateMathLesson(LessonAdmin admin, int? sessionId = null) =>
            await admin.CreateAsync(_mathOfferingId, weekNumber: 1, "الكسور", "Fractions", sessionId: sessionId);

        // ---------------------------------------------------------------- BR-LRN-001 anchor

        [Fact]
        [BusinessRule("BR-LRN-001")]
        public async Task A_lesson_inherits_the_academic_year_of_the_offering_it_teaches()
        {
            using var db = CreateContext();
            var lesson = await CreateMathLesson(CreateAdmin(db));

            Assert.Equal(_mathOfferingId, lesson.CurriculumOfferingId);
            Assert.Equal(_yearId, lesson.AcademicYearId);
        }

        [Fact]
        [BusinessRule("BR-LRN-001")]
        public async Task An_unbound_lesson_is_a_syllabus_entry()
        {
            using var db = CreateContext();
            var lesson = await CreateMathLesson(CreateAdmin(db));

            Assert.Null(lesson.SessionId);
        }

        [Fact]
        [BusinessRule("BR-LRN-001")]
        public async Task A_lesson_may_bind_to_a_session_that_teaches_its_offering()
        {
            using var db = CreateContext();
            var lesson = await CreateMathLesson(CreateAdmin(db), _mathSessionId);

            Assert.Equal(_mathSessionId, lesson.SessionId);
        }

        [Fact]
        [BusinessRule("BR-LRN-001")]
        public async Task Binding_to_a_session_that_teaches_a_different_offering_is_refused()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            await Assert.ThrowsAsync<LessonSessionMismatchException>(
                () => CreateMathLesson(admin, _artSessionId));
        }

        // ---------------------------------------------------------------- BR-LRN-002 reach

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public async Task A_teacher_may_author_content_for_an_offering_they_are_placed_on()
        {
            using var db = CreateContext();
            var lesson = await CreateMathLesson(CreateAdmin(db));

            Assert.True(lesson.Id > 0);
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public async Task A_teacher_may_not_author_content_for_an_offering_they_do_not_teach()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            await Assert.ThrowsAsync<TeachingReachException>(
                () => admin.CreateAsync(_artOfferingId, 1, "منظور", "Perspective"));
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public async Task A_head_of_department_reaches_their_departments_offerings()
        {
            _user.UserId = HeadOfDepartmentUserId;
            using var db = CreateContext();
            var lesson = await CreateAdmin(db).CreateAsync(_artOfferingId, 1, "منظور", "Perspective");

            Assert.Equal(_artOfferingId, lesson.CurriculumOfferingId);
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public async Task A_user_with_no_placement_and_no_department_reaches_nothing()
        {
            _user.UserId = StrangerUserId;
            using var db = CreateContext();

            await Assert.ThrowsAsync<TeachingReachException>(
                () => CreateMathLesson(CreateAdmin(db)));
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public async Task A_draft_timetable_version_grants_no_reach()
        {
            // BR-LRN-002 measures reach in the published version only. Demoting the
            // published version must take the teacher's authoring rights with it.
            using (var setup = CreateContext())
            {
                var version = await setup.TimetableVersions.SingleAsync(v => v.Status == TimetableVersionStatus.Published);
                version.Status = TimetableVersionStatus.Draft;
                await setup.SaveChangesAsync();
            }

            using var db = CreateContext();
            await Assert.ThrowsAsync<TeachingReachException>(() => CreateMathLesson(CreateAdmin(db)));
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public async Task Reach_is_enforced_when_publishing_not_only_when_creating()
        {
            int lessonId;
            using (var authoring = CreateContext())
            {
                lessonId = (await CreateMathLesson(CreateAdmin(authoring))).Id;
            }

            _user.UserId = StrangerUserId;
            using var db = CreateContext();
            await Assert.ThrowsAsync<TeachingReachException>(() => CreateAdmin(db).PublishAsync(lessonId));
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public async Task Reach_is_enforced_when_attaching_a_resource()
        {
            int lessonId;
            using (var authoring = CreateContext())
            {
                lessonId = (await CreateMathLesson(CreateAdmin(authoring))).Id;
            }

            _user.UserId = StrangerUserId;
            using var db = CreateContext();
            await Assert.ThrowsAsync<TeachingReachException>(
                () => CreateAdmin(db).AttachResourceAsync(lessonId, _attachmentId, "ورقة عمل", "Worksheet"));
        }

        // ---------------------------------------------------------------- BR-LRN-003 publication gate

        [Fact]
        [BusinessRule("BR-LRN-003")]
        public async Task A_new_lesson_starts_as_a_draft_and_is_invisible_to_the_portal()
        {
            using var db = CreateContext();
            var lesson = await CreateMathLesson(CreateAdmin(db));

            Assert.Equal(LessonStatus.Draft, lesson.Status);
            Assert.Null(lesson.PublishedAtUtc);
        }

        [Fact]
        [BusinessRule("BR-LRN-003")]
        public async Task Publishing_stamps_the_moment_families_can_see_it()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var lesson = await CreateMathLesson(admin);

            await admin.PublishAsync(lesson.Id);

            var stored = await db.Lessons.SingleAsync(l => l.Id == lesson.Id);
            Assert.Equal(LessonStatus.Published, stored.Status);
            Assert.Equal(_clock.UtcNow, stored.PublishedAtUtc);
        }

        [Fact]
        [BusinessRule("BR-LRN-003")]
        public async Task Publishing_twice_is_refused()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var lesson = await CreateMathLesson(admin);
            await admin.PublishAsync(lesson.Id);

            await Assert.ThrowsAsync<LessonTransitionException>(() => admin.PublishAsync(lesson.Id));
        }

        // ---------------------------------------------------------------- BR-LRN-016 no delete

        [Fact]
        [BusinessRule("BR-LRN-016")]
        public async Task Retiring_records_why_because_a_student_who_read_it_will_ask()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var lesson = await CreateMathLesson(admin);
            await admin.PublishAsync(lesson.Id);

            await admin.RetireAsync(lesson.Id, "استُبدلت بنسخة مصححة");

            var stored = await db.Lessons.SingleAsync(l => l.Id == lesson.Id);
            Assert.Equal(LessonStatus.Retired, stored.Status);
            Assert.Equal("استُبدلت بنسخة مصححة", stored.RetiredReason);
            Assert.Equal(_clock.UtcNow, stored.RetiredAtUtc);
        }

        [Theory]
        [BusinessRule("BR-LRN-016")]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Retiring_without_a_reason_is_refused(string reason)
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var lesson = await CreateMathLesson(admin);

            await Assert.ThrowsAsync<ArgumentException>(() => admin.RetireAsync(lesson.Id, reason));
        }

        [Fact]
        [BusinessRule("BR-LRN-016")]
        public async Task A_retired_lesson_can_no_longer_be_edited()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var lesson = await CreateMathLesson(admin);
            await admin.RetireAsync(lesson.Id, "ألغيت الوحدة");

            await Assert.ThrowsAsync<LessonRetiredException>(
                () => admin.UpdateAsync(lesson.Id, 2, "الكسور", "Fractions"));
        }

        [Fact]
        [BusinessRule("BR-LRN-016")]
        public async Task A_withdrawn_resource_is_deactivated_never_deleted()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var lesson = await CreateMathLesson(admin);
            var resource = await admin.AttachResourceAsync(lesson.Id, _attachmentId, "ورقة عمل", "Worksheet");

            await admin.WithdrawResourceAsync(resource.Id);

            // The row survives; only the soft-active filter hides it.
            var stored = await db.LessonResources.IgnoreQueryFilters().SingleAsync(r => r.Id == resource.Id);
            Assert.False(stored.IsActive);
            Assert.Empty(await db.LessonResources.Where(r => r.Id == resource.Id).ToListAsync());
        }

        [Fact]
        [BusinessRule("BR-GLB-005")]
        public async Task Hard_deleting_a_lesson_resource_throws_bypassing_the_service()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var lesson = await CreateMathLesson(admin);
            var resource = await admin.AttachResourceAsync(lesson.Id, _attachmentId, "ورقة عمل", "Worksheet");

            db.LessonResources.Remove(resource);

            await Assert.ThrowsAsync<HardDeleteForbiddenException>(() => db.SaveChangesAsync());
        }

        // ---------------------------------------------------------------- resources

        [Fact]
        [BusinessRule("BR-LRN-006")]
        public async Task A_resource_links_an_attachment_and_owns_no_bytes_of_its_own()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var lesson = await CreateMathLesson(admin);

            var resource = await admin.AttachResourceAsync(lesson.Id, _attachmentId, "ورقة عمل", "Worksheet", displayOrder: 2);

            Assert.Equal(_attachmentId, resource.AttachmentId);
            Assert.Equal(lesson.Id, resource.LessonId);
            Assert.Equal(2, resource.DisplayOrder);
            Assert.Equal(_yearId, resource.AcademicYearId);
            Assert.Equal(1, resource.SchoolId);
        }

        [Fact]
        [BusinessRule("BR-LRN-016")]
        public async Task A_retired_lesson_accepts_no_new_material()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var lesson = await CreateMathLesson(admin);
            await admin.RetireAsync(lesson.Id, "ألغيت الوحدة");

            await Assert.ThrowsAsync<LessonRetiredException>(
                () => admin.AttachResourceAsync(lesson.Id, _attachmentId, "ورقة عمل", "Worksheet"));
        }
    }
}
