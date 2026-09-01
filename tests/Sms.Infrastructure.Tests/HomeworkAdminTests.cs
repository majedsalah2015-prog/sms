using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Learning;
using Sms.Application.Setup;
using Sms.Domain.Calendar;
using Sms.Domain.Common;
using Sms.Domain.Employees;
using Sms.Domain.Grades;
using Sms.Domain.Grading;
using Sms.Domain.Learning;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Setup;
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
    /// Module 37 slice 3 (doc/Modules/37 §8.3, BR-LRN-002/003/004/016) over a
    /// real Sqlite-backed AppDbContext.
    /// </summary>
    public sealed class HomeworkAdminTests : IDisposable
    {
        private const int TeacherUserId = 500;
        private const int HeadOfDepartmentUserId = 600;
        private const int StrangerUserId = 700;

        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
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

        /// <summary>
        /// Answers the one setting BR-GLB-052 needs and refuses the rest, so a
        /// method quietly starting to depend on setup fails loudly here rather
        /// than passing against a stub that invented an answer.
        /// </summary>
        private sealed class FixedSetup : ISystemSetupAdmin
        {
            public string? WorkingDays { get; set; } = "Sunday,Monday,Tuesday,Wednesday,Thursday";

            public Task<string?> GetSettingAsync(string key, int? academicYearId = null, CancellationToken cancellationToken = default)
                => Task.FromResult(key == SettingKeys.WorkingDays ? WorkingDays : null);

            public Task<CountryPack> DefineCountryPackAsync(CountryPackDefinition definition, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task BindCountryPackAsync(string packCode, string? reason = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<CountryPack?> GetBoundCountryPackAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<SchoolSetting> SetSettingAsync(string key, string value, int? academicYearId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<IReadOnlyList<SchoolSetting>> ListSettingsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task SetFeatureAsync(string featureCode, bool enabled, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<IReadOnlyDictionary<string, bool>> GetFeatureStatesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<IReadOnlyList<StepState>> GetChecklistAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task CompleteStepAsync(string stepCode, string? notes = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task DeclareSetupCompleteAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<bool> IsSetupCompleteAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();
        private readonly FixedSetup _setup = new();

        private readonly int _yearId;
        private readonly int _mathOfferingId;
        private readonly int _artOfferingId;
        private readonly int _sectionAId;
        private readonly int _sectionBId;
        private readonly int _componentId;

        // 2026-10-05 is a Monday, inside the year, and a working day.
        private static readonly DateTime GoodDueDate = new(2026, 10, 5);

        public HomeworkAdminTests()
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

            var profile = new GradeYearProfile { GradeLevelId = grade.Id, AcademicYearId = year.Id, GenderPolicy = GenderPolicy.Mixed, TargetSections = 2, TargetSectionSize = 25 };
            db.GradeYearProfiles.Add(profile);
            db.SaveChanges();

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

            // Two sections of the same grade: the teacher holds Math in 3-A only,
            // which is what makes BR-LRN-002's pair rule testable.
            var sectionA = NewSection(year.Id, profile.Id, "ثالث-أ", "3-A");
            var sectionB = NewSection(year.Id, profile.Id, "ثالث-ب", "3-B");
            db.Sections.AddRange(sectionA, sectionB);
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

            // The teacher holds Math in 3-A. Art in 3-A belongs to someone else,
            // and nobody in this fixture holds Math in 3-B.
            db.Placements.AddRange(
                new Placement { SchoolId = 1, TimetableVersionId = published.Id, SectionId = sectionA.Id, PeriodSlotId = slot.Id, CurriculumOfferingId = mathOffering.Id, TeacherProfileId = teacher.Id },
                new Placement { SchoolId = 1, TimetableVersionId = published.Id, SectionId = sectionA.Id, PeriodSlotId = slot.Id, CurriculumOfferingId = artOffering.Id, TeacherProfileId = teacher.Id + 99 },
                new Placement { SchoolId = 1, TimetableVersionId = published.Id, SectionId = sectionB.Id, PeriodSlotId = slot.Id, CurriculumOfferingId = artOffering.Id, TeacherProfileId = teacher.Id + 99 });
            db.SaveChanges();

            // A Module 17 component for the graded-homework path.
            var scale = new GradingScale { SchoolId = 1, AcademicYearId = year.Id, StageId = stage.Id, NameAr = "مئوي", NameEn = "Percentage" };
            db.GradingScales.Add(scale);
            db.SaveChanges();

            var blueprint = new Blueprint
            {
                SchoolId = 1, AcademicYearId = year.Id, CurriculumOfferingId = mathOffering.Id,
                TermId = 1, GradingScaleId = scale.Id,
            };
            db.Blueprints.Add(blueprint);
            db.SaveChanges();

            var component = new BlueprintComponent
            {
                SchoolId = 1, BlueprintId = blueprint.Id, NameAr = "واجبات", NameEn = "Homework",
                Weight = 20m, MaxScore = 20m,
            };
            db.BlueprintComponents.Add(component);
            db.SaveChanges();

            _yearId = year.Id;
            _mathOfferingId = mathOffering.Id;
            _artOfferingId = artOffering.Id;
            _sectionAId = sectionA.Id;
            _sectionBId = sectionB.Id;
            _componentId = component.Id;
        }

        public void Dispose() => _connection.Dispose();

        private static CurriculumOffering NewOffering(int yearId, int profileId, int subjectId) => new()
        {
            SchoolId = 1, AcademicYearId = yearId, GradeYearProfileId = profileId, SubjectId = subjectId,
            WeeklyPeriods = 5, IsAssessable = true, GpaWeight = 1m, EffectiveFromUtc = new DateTime(2026, 9, 1),
        };

        private static Section NewSection(int yearId, int profileId, string nameAr, string nameEn) => new()
        {
            SchoolId = 1, AcademicYearId = yearId, GradeYearProfileId = profileId,
            NameAr = nameAr, NameEn = nameEn, Capacity = 25, GenderPolicy = GenderPolicy.Mixed,
        };

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private HomeworkAdmin CreateAdmin(AppDbContext db) => new(db, _clock, _user, _setup);

        private Task<Homework> CreateMathHomework(
            HomeworkAdmin admin,
            int? sectionId = null,
            DateTime? dueDate = null,
            decimal? maxMarks = null,
            int? componentId = null)
            => admin.CreateAsync(
                _mathOfferingId, sectionId ?? _sectionAId, "واجب الكسور", "Fractions homework",
                dueDate ?? GoodDueDate, maxMarks: maxMarks, blueprintComponentId: componentId);

        // ---------------------------------------------------------------- BR-LRN-001 anchor

        [Fact]
        [BusinessRule("BR-LRN-001")]
        public async Task Homework_inherits_the_academic_year_of_the_offering_it_is_set_against()
        {
            using var db = CreateContext();
            var homework = await CreateMathHomework(CreateAdmin(db));

            Assert.Equal(_mathOfferingId, homework.CurriculumOfferingId);
            Assert.Equal(_yearId, homework.AcademicYearId);
            Assert.Equal(HomeworkStatus.Draft, homework.Status);
        }

        // ---------------------------------------------------------------- BR-LRN-002 reach

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public async Task A_teacher_may_set_work_for_the_section_they_are_placed_on()
        {
            using var db = CreateContext();
            var homework = await CreateMathHomework(CreateAdmin(db));

            Assert.Equal(_sectionAId, homework.SectionId);
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public async Task Reach_for_homework_is_the_offering_and_section_pair_not_the_offering_alone()
        {
            // The teacher teaches Math - but in 3-A. Setting Math homework for
            // 3-B is setting work for a class they do not stand in front of.
            using var db = CreateContext();

            await Assert.ThrowsAsync<TeachingReachException>(
                () => CreateMathHomework(CreateAdmin(db), sectionId: _sectionBId));
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public async Task A_teacher_may_not_set_work_for_an_offering_they_do_not_hold()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            await Assert.ThrowsAsync<TeachingReachException>(() => admin.CreateAsync(
                _artOfferingId, _sectionAId, "واجب", "Homework", GoodDueDate));
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public async Task A_head_of_department_reaches_every_section_of_their_departments_offerings()
        {
            _user.UserId = HeadOfDepartmentUserId;
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            var homework = await admin.CreateAsync(_artOfferingId, _sectionBId, "واجب فني", "Art homework", GoodDueDate);

            Assert.Equal(_artOfferingId, homework.CurriculumOfferingId);
            Assert.Equal(_sectionBId, homework.SectionId);
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public async Task A_user_with_neither_placement_nor_department_reaches_nothing()
        {
            _user.UserId = StrangerUserId;
            using var db = CreateContext();

            await Assert.ThrowsAsync<TeachingReachException>(() => CreateMathHomework(CreateAdmin(db)));

            var reachable = await CreateAdmin(db).ReachableSectionsAsync();
            Assert.Empty(reachable);
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public async Task The_picker_offers_exactly_the_pairs_the_guard_will_accept()
        {
            using var db = CreateContext();
            var reachable = await CreateAdmin(db).ReachableSectionsAsync();

            var pair = Assert.Single(reachable);
            Assert.Equal(_mathOfferingId, pair.CurriculumOfferingId);
            Assert.Equal(_sectionAId, pair.SectionId);
        }

        // ---------------------------------------------------------------- BR-LRN-004 issue gate

        [Fact]
        [BusinessRule("BR-GLB-031")]
        public async Task A_draft_may_be_incomplete_because_it_affects_nothing()
        {
            // Graded, but no component named yet. Saving must not refuse - the
            // teacher is allowed to start on Tuesday and finish on Wednesday.
            using var db = CreateContext();
            var homework = await CreateMathHomework(CreateAdmin(db), maxMarks: 10m);

            Assert.Equal(HomeworkStatus.Draft, homework.Status);
            Assert.Null(homework.BlueprintComponentId);
        }

        [Fact]
        [BusinessRule("BR-LRN-004")]
        public async Task Issuing_graded_homework_with_no_component_is_refused()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var homework = await CreateMathHomework(admin, maxMarks: 10m);

            var ex = await Assert.ThrowsAsync<HomeworkIssueRefusedException>(() => admin.IssueAsync(homework.Id));
            Assert.Equal(HomeworkIssueRefusal.GradedWithoutBlueprintComponent, ex.Reason);

            // And the refusal left the row alone.
            var reloaded = await db.Homeworks.AsNoTracking().SingleAsync(h => h.Id == homework.Id);
            Assert.Equal(HomeworkStatus.Draft, reloaded.Status);
            Assert.Null(reloaded.IssuedAtUtc);
        }

        [Fact]
        [BusinessRule("BR-LRN-004")]
        public async Task Ungraded_practice_issues_without_a_component()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var homework = await CreateMathHomework(admin);

            await admin.IssueAsync(homework.Id);

            var reloaded = await db.Homeworks.AsNoTracking().SingleAsync(h => h.Id == homework.Id);
            Assert.Equal(HomeworkStatus.Issued, reloaded.Status);
            Assert.Equal(_clock.UtcNow, reloaded.IssuedAtUtc);
        }

        [Fact]
        [BusinessRule("BR-LRN-004")]
        public async Task Graded_homework_naming_its_component_issues()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var homework = await CreateMathHomework(admin, maxMarks: 10m, componentId: _componentId);

            await admin.IssueAsync(homework.Id);

            var reloaded = await db.Homeworks.AsNoTracking().SingleAsync(h => h.Id == homework.Id);
            Assert.Equal(HomeworkStatus.Issued, reloaded.Status);
            Assert.Equal(_componentId, reloaded.BlueprintComponentId);
        }

        [Fact]
        [BusinessRule("BR-GLB-051")]
        public async Task A_due_date_outside_the_academic_year_is_refused_at_issue()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var homework = await CreateMathHomework(admin, dueDate: new DateTime(2027, 8, 1));

            var ex = await Assert.ThrowsAsync<HomeworkIssueRefusedException>(() => admin.IssueAsync(homework.Id));
            Assert.Equal(HomeworkIssueRefusal.DueDateOutsideAcademicYear, ex.Reason);
        }

        [Fact]
        [BusinessRule("BR-GLB-052")]
        public async Task A_due_date_on_a_weekend_is_refused_at_issue()
        {
            // 2026-10-10 is a Saturday, which the working week excludes.
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var homework = await CreateMathHomework(admin, dueDate: new DateTime(2026, 10, 10));

            var ex = await Assert.ThrowsAsync<HomeworkIssueRefusedException>(() => admin.IssueAsync(homework.Id));
            Assert.Equal(HomeworkIssueRefusal.DueDateNotAWorkingDay, ex.Reason);
        }

        [Fact]
        [BusinessRule("BR-GLB-052")]
        public async Task A_due_date_on_a_calendar_holiday_is_refused_at_issue()
        {
            // The school calendar overrides an ordinary working Monday.
            using (var seed = CreateContext())
            {
                seed.CalendarDays.Add(new CalendarDay
                {
                    SchoolId = 1, AcademicYearId = _yearId, Date = GoodDueDate, DayType = DayType.Holiday,
                });
                await seed.SaveChangesAsync();
            }

            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var homework = await CreateMathHomework(admin);

            var ex = await Assert.ThrowsAsync<HomeworkIssueRefusedException>(() => admin.IssueAsync(homework.Id));
            Assert.Equal(HomeworkIssueRefusal.DueDateNotAWorkingDay, ex.Reason);
        }

        // ---------------------------------------------------------------- BR-LRN-003 lifecycle

        [Fact]
        [BusinessRule("BR-LRN-003")]
        public async Task Issuing_twice_is_refused()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var homework = await CreateMathHomework(admin);
            await admin.IssueAsync(homework.Id);

            var ex = await Assert.ThrowsAsync<HomeworkTransitionException>(() => admin.IssueAsync(homework.Id));
            Assert.Equal(HomeworkStatus.Issued, ex.From);
        }

        [Fact]
        [BusinessRule("BR-LRN-016")]
        public async Task Withdrawing_states_a_reason_and_keeps_the_row()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var homework = await CreateMathHomework(admin);
            await admin.IssueAsync(homework.Id);

            await admin.WithdrawAsync(homework.Id, "أُلغيت الحصة");

            var reloaded = await db.Homeworks.AsNoTracking().SingleAsync(h => h.Id == homework.Id);
            Assert.Equal(HomeworkStatus.Withdrawn, reloaded.Status);
            Assert.Equal("أُلغيت الحصة", reloaded.WithdrawnReason);
            Assert.Equal(_clock.UtcNow, reloaded.WithdrawnAtUtc);
        }

        [Fact]
        [BusinessRule("BR-LRN-016")]
        public async Task Withdrawing_without_a_reason_is_refused()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var homework = await CreateMathHomework(admin);

            await Assert.ThrowsAsync<ArgumentException>(() => admin.WithdrawAsync(homework.Id, "   "));
        }

        [Fact]
        [BusinessRule("BR-LRN-016")]
        public async Task Withdrawn_homework_can_no_longer_be_edited()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var homework = await CreateMathHomework(admin);
            await admin.WithdrawAsync(homework.Id, "خطأ في الإدخال");

            await Assert.ThrowsAsync<HomeworkTransitionException>(() => admin.UpdateAsync(
                homework.Id, "عنوان جديد", "New title", GoodDueDate));
        }

        [Fact]
        [BusinessRule("BR-LRN-016")]
        public async Task Withdrawing_twice_is_refused()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var homework = await CreateMathHomework(admin);
            await admin.WithdrawAsync(homework.Id, "سبب");

            await Assert.ThrowsAsync<HomeworkTransitionException>(() => admin.WithdrawAsync(homework.Id, "سبب آخر"));
        }

        // ---------------------------------------------------------------- BR-LRN-005 lateness

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public async Task A_penalty_percentage_is_kept_only_while_the_policy_uses_one()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            var penalised = await admin.CreateAsync(
                _mathOfferingId, _sectionAId, "واجب", "Homework", GoodDueDate,
                latenessPolicy: LatenessPolicy.AcceptWithPenalty, latePenaltyPercent: 25m);
            Assert.Equal(25m, penalised.LatePenaltyPercent);

            // Switching the policy must not leave a stale penalty behind that
            // nothing displays but marking would later read.
            var updated = await admin.UpdateAsync(
                penalised.Id, "واجب", "Homework", GoodDueDate,
                latenessPolicy: LatenessPolicy.AcceptWithoutPenalty, latePenaltyPercent: 25m);
            Assert.Null(updated.LatePenaltyPercent);
        }

        // ---------------------------------------------------------------- tenancy

        [Fact]
        [BusinessRule("BR-GLB-010")]
        public async Task Homework_is_stamped_with_the_acting_tenant()
        {
            using var db = CreateContext();
            var homework = await CreateMathHomework(CreateAdmin(db));

            Assert.Equal(_tenant.SchoolId, homework.SchoolId);
        }
    }
}
