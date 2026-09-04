using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Guards;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Grades;
using Sms.Domain.Grading;
using Sms.Domain.Learning;
using Sms.Domain.Schools;
using Sms.Domain.Security;
using Sms.Domain.Subjects;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Subjects;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// E-104 (slice: Subjects, doc/Modules/07, BR-SUB-001..004/009) over a
    /// real Sqlite-backed AppDbContext.
    /// </summary>
    public sealed class SubjectAdminTests : IDisposable
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

        public SubjectAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            var year = new AcademicYear
            {
                LabelAr = "٢٠٢٦-٢٠٢٧", LabelEn = "2026-2027", HijriLabel = "١٤٤٨هـ",
                StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 6, 30),
                Status = AcademicYearStatus.Active,
            };
            db.AcademicYears.Add(year);
            var stage = new Stage { Name = new Sms.Domain.Common.LocalizedName("الابتدائية", "Elementary"), SequenceOrder = 1, DefaultGenderPolicy = GenderPolicy.Mixed };
            db.Stages.Add(stage);
            db.SaveChanges();
            var grade = new GradeLevel { StageId = stage.Id, Code = "G3", Name = new Sms.Domain.Common.LocalizedName("ثالث", "Grade 3"), SequenceOrder = 3 };
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

        // --- BR-SUB-001 subject catalog -----------------------------------------

        [Fact]
        [BusinessRule("BR-SUB-001")]
        public async Task Defining_a_subject_links_it_to_its_department()
        {
            using var db = CreateContext();
            var admin = new SubjectAdmin(db, new CurriculumOfferingUsageInspector(db));
            var dept = await admin.DefineDepartmentAsync("العلوم", "Sciences");

            var subject = await admin.DefineSubjectAsync("SCI3", "علوم", "Science", "Core", dept.Id);

            Assert.Equal(dept.Id, db.Subjects.Single(s => s.Id == subject.Id).DepartmentId);
        }

        [Fact]
        [BusinessRule("BR-SUB-001")]
        public async Task A_duplicate_subject_code_is_rejected()
        {
            using var db = CreateContext();
            var admin = new SubjectAdmin(db, new CurriculumOfferingUsageInspector(db));
            await admin.DefineSubjectAsync("MATH3", "رياضيات", "Math", "Core");

            await Assert.ThrowsAsync<DuplicateSubjectCodeException>(() =>
                admin.DefineSubjectAsync("MATH3", "رياضيات ٢", "Math Again", "Core"));
        }

        // --- BR-SUB §9 offering weight + uniqueness -----------------------------

        [Fact]
        [BusinessRule("BR-SUB-009")]
        public async Task An_assessable_offering_without_a_positive_weight_is_rejected()
        {
            using var db = CreateContext();
            var admin = new SubjectAdmin(db, new CurriculumOfferingUsageInspector(db));
            var subject = await admin.DefineSubjectAsync("MATH3", "رياضيات", "Math", "Core");

            await Assert.ThrowsAsync<InvalidOfferingWeightException>(() =>
                admin.DefineOfferingAsync(_profileId, subject.Id, 5, isAssessable: true, gpaWeight: 0, false, null, new DateTime(2026, 9, 1)));
        }

        [Fact]
        [BusinessRule("BR-SUB-003")]
        public async Task A_non_assessable_offering_needs_no_weight()
        {
            using var db = CreateContext();
            var admin = new SubjectAdmin(db, new CurriculumOfferingUsageInspector(db));
            var subject = await admin.DefineSubjectAsync("ASSEMBLY", "الطابور", "Assembly", "Other");

            var offering = await admin.DefineOfferingAsync(
                _profileId, subject.Id, 1, isAssessable: false, gpaWeight: 0, false, null, new DateTime(2026, 9, 1));

            Assert.False(db.CurriculumOfferings.Single(o => o.Id == offering.Id).IsAssessable);
        }

        [Fact]
        public async Task A_duplicate_current_offering_for_the_same_grade_year_and_subject_is_rejected()
        {
            using var db = CreateContext();
            var admin = new SubjectAdmin(db, new CurriculumOfferingUsageInspector(db));
            var subject = await admin.DefineSubjectAsync("MATH3", "رياضيات", "Math", "Core");
            await admin.DefineOfferingAsync(_profileId, subject.Id, 5, true, 10, false, null, new DateTime(2026, 9, 1));

            await Assert.ThrowsAsync<DuplicateOfferingException>(() =>
                admin.DefineOfferingAsync(_profileId, subject.Id, 4, true, 8, false, null, new DateTime(2026, 9, 1)));
        }

        // --- BR-SUB-004 end-dating, not removal ---------------------------------

        [Fact]
        [BusinessRule("BR-SUB-004")]
        public async Task Ending_an_offering_sets_effective_to_rather_than_deleting_it()
        {
            using var db = CreateContext();
            var admin = new SubjectAdmin(db, new CurriculumOfferingUsageInspector(db));
            var subject = await admin.DefineSubjectAsync("MATH3", "رياضيات", "Math", "Core");
            var offering = await admin.DefineOfferingAsync(_profileId, subject.Id, 5, true, 10, false, null, new DateTime(2026, 9, 1));

            await admin.EndDateOfferingAsync(offering.Id, new DateTime(2027, 1, 1));

            var stored = db.CurriculumOfferings.Single(o => o.Id == offering.Id);
            Assert.Equal(new DateTime(2027, 1, 1), stored.EffectiveToUtc);
        }

        [Fact]
        [BusinessRule("BR-SUB-004")]
        public async Task After_end_dating_a_new_offering_can_be_defined_for_the_same_pair()
        {
            using var db = CreateContext();
            var admin = new SubjectAdmin(db, new CurriculumOfferingUsageInspector(db));
            var subject = await admin.DefineSubjectAsync("MATH3", "رياضيات", "Math", "Core");
            var offering = await admin.DefineOfferingAsync(_profileId, subject.Id, 5, true, 10, false, null, new DateTime(2026, 9, 1));
            await admin.EndDateOfferingAsync(offering.Id, new DateTime(2027, 1, 1));

            var replacement = await admin.DefineOfferingAsync(_profileId, subject.Id, 6, true, 12, false, null, new DateTime(2027, 1, 1));

            Assert.Equal(2, db.CurriculumOfferings.Count(o => o.SubjectId == subject.Id));
            Assert.Null(db.CurriculumOfferings.Single(o => o.Id == replacement.Id).EffectiveToUtc);
        }

        // --- BR-SUB-006 qualification matrix ------------------------------------

        [Fact]
        [BusinessRule("BR-SUB-006")]
        public async Task Defining_a_qualification_links_a_teacher_to_a_subject()
        {
            using var db = CreateContext();
            var admin = new SubjectAdmin(db, new CurriculumOfferingUsageInspector(db));
            var teacher = db.UserAccounts.Add(new UserAccount { UserName = "t.noor", AccountType = AccountType.Staff }).Entity;
            await db.SaveChangesAsync();
            var subject = await admin.DefineSubjectAsync("MATH3", "رياضيات", "Math", "Core");

            var qualification = await admin.DefineQualificationAsync(teacher.Id, subject.Id, stageId: null, QualificationSource.Qualification);

            Assert.Equal(teacher.Id, db.TeacherSubjectQualifications.Single(q => q.Id == qualification.Id).TeacherUserId);
        }

        // --- edit / soft-delete of subjects and departments -------------------------

        [Fact]
        [BusinessRule("BR-SUB-001")]
        public async Task Editing_a_subject_keeps_codes_unique()
        {
            using var db = CreateContext();
            var admin = new SubjectAdmin(db, new CurriculumOfferingUsageInspector(db));
            var math = await admin.DefineSubjectAsync("MATH3", "رياضيات", "Math", "core");
            var sci = await admin.DefineSubjectAsync("SCI3", "علوم", "Science", "core");

            await admin.UpdateSubjectAsync(math.Id, "MATH3", "الرياضيات", "Mathematics", "core");
            Assert.Equal("Mathematics", db.Subjects.Single(s => s.Id == math.Id).Name.NameEn);

            await Assert.ThrowsAsync<DuplicateSubjectCodeException>(() => admin.UpdateSubjectAsync(sci.Id, "MATH3", "ع", "S", "core"));
        }

        [Fact]
        [BusinessRule("BR-SUB-004")]
        public async Task A_subject_in_a_current_plan_cannot_be_removed_until_the_offering_is_end_dated()
        {
            using var db = CreateContext();
            var admin = new SubjectAdmin(db, new CurriculumOfferingUsageInspector(db));
            var subject = await admin.DefineSubjectAsync("MATH3", "رياضيات", "Math", "core");
            var offering = await admin.DefineOfferingAsync(_profileId, subject.Id, 5, true, 10, false, null, new DateTime(2026, 9, 1));

            await Assert.ThrowsAsync<SubjectInUseException>(() => admin.DeactivateSubjectAsync(subject.Id));

            await admin.EndDateOfferingAsync(offering.Id, new DateTime(2027, 1, 1));
            await admin.DeactivateSubjectAsync(subject.Id);
            Assert.Empty(db.Subjects.Where(s => s.Id == subject.Id)); // soft-active filter hides it
            Assert.False(db.Subjects.IgnoreQueryFilters().Single(s => s.Id == subject.Id).IsActive);
        }

        [Fact]
        public async Task A_department_with_subjects_cannot_be_removed()
        {
            using var db = CreateContext();
            var admin = new SubjectAdmin(db, new CurriculumOfferingUsageInspector(db));
            var dept = await admin.DefineDepartmentAsync("العلوم", "Sciences");
            var subject = await admin.DefineSubjectAsync("SCI3", "علوم", "Science", "core", dept.Id);

            await admin.UpdateDepartmentAsync(dept.Id, "قسم العلوم", "Science dept.");
            Assert.Equal("Science dept.", db.Departments.Single(d => d.Id == dept.Id).Name.NameEn);

            await Assert.ThrowsAsync<SubjectInUseException>(() => admin.DeactivateDepartmentAsync(dept.Id));
            await admin.UpdateSubjectAsync(subject.Id, "SCI3", "علوم", "Science", "core", null);
            await admin.DeactivateDepartmentAsync(dept.Id);
            Assert.Empty(db.Departments.Where(d => d.Id == dept.Id));
        }

        // --- editing and removing a plan line (doc/Modules/07 §8.2, §9) --------------

        [Fact]
        public async Task Editing_an_offering_corrects_the_same_line_rather_than_replacing_it()
        {
            using var db = CreateContext();
            var admin = new SubjectAdmin(db, new CurriculumOfferingUsageInspector(db));
            var subject = await admin.DefineSubjectAsync("MATH3", "رياضيات", "Math", "core");
            var offering = await admin.DefineOfferingAsync(_profileId, subject.Id, 5, true, 10, false, null, new DateTime(2026, 9, 1));

            await admin.UpdateOfferingAsync(offering.Id, 6, isAssessable: true, gpaWeight: 12, isElective: true, electiveGroupTag: "LANG");

            var stored = db.CurriculumOfferings.Single(o => o.Id == offering.Id);
            Assert.Equal(6, stored.WeeklyPeriods);
            Assert.Equal(12m, stored.GpaWeight);
            Assert.True(stored.IsElective);
            Assert.Equal("LANG", stored.ElectiveGroupTag);

            // The point of editing rather than end-dating-and-redefining: one row, still current, so
            // everything already pointing at it goes on pointing at it.
            Assert.Null(stored.EffectiveToUtc);
            Assert.Single(db.CurriculumOfferings.Where(o => o.SubjectId == subject.Id));
        }

        [Fact]
        public async Task An_offering_cannot_be_edited_into_being_assessed_for_nothing()
        {
            using var db = CreateContext();
            var admin = new SubjectAdmin(db, new CurriculumOfferingUsageInspector(db));
            var subject = await admin.DefineSubjectAsync("MATH3", "رياضيات", "Math", "core");
            var offering = await admin.DefineOfferingAsync(_profileId, subject.Id, 5, true, 10, false, null, new DateTime(2026, 9, 1));

            await Assert.ThrowsAsync<InvalidOfferingWeightException>(
                () => admin.UpdateOfferingAsync(offering.Id, 5, isAssessable: true, gpaWeight: 0, isElective: false, electiveGroupTag: null));

            Assert.Equal(10m, db.CurriculumOfferings.Single(o => o.Id == offering.Id).GpaWeight);
        }

        [Fact]
        public async Task An_offering_cannot_be_edited_down_to_no_periods()
        {
            using var db = CreateContext();
            var admin = new SubjectAdmin(db, new CurriculumOfferingUsageInspector(db));
            var subject = await admin.DefineSubjectAsync("MATH3", "رياضيات", "Math", "core");
            var offering = await admin.DefineOfferingAsync(_profileId, subject.Id, 5, true, 10, false, null, new DateTime(2026, 9, 1));

            await Assert.ThrowsAsync<InvalidOfferingPeriodsException>(
                () => admin.UpdateOfferingAsync(offering.Id, 0, isAssessable: true, gpaWeight: 10, isElective: false, electiveGroupTag: null));

            Assert.Equal(5, db.CurriculumOfferings.Single(o => o.Id == offering.Id).WeeklyPeriods);
        }

        [Fact]
        [BusinessRule("BR-SUB-004")]
        public async Task An_end_dated_offering_can_no_longer_be_edited()
        {
            using var db = CreateContext();
            var admin = new SubjectAdmin(db, new CurriculumOfferingUsageInspector(db));
            var subject = await admin.DefineSubjectAsync("MATH3", "رياضيات", "Math", "core");
            var offering = await admin.DefineOfferingAsync(_profileId, subject.Id, 5, true, 10, false, null, new DateTime(2026, 9, 1));
            await admin.EndDateOfferingAsync(offering.Id, new DateTime(2027, 1, 1));

            await Assert.ThrowsAsync<EndedOfferingNotEditableException>(
                () => admin.UpdateOfferingAsync(offering.Id, 6, isAssessable: true, gpaWeight: 12, isElective: false, electiveGroupTag: null));

            Assert.Equal(5, db.CurriculumOfferings.Single(o => o.Id == offering.Id).WeeklyPeriods);
        }

        [Fact]
        [BusinessRule("BR-SUB-004")]
        public async Task An_offering_nothing_points_at_is_removed_outright()
        {
            using var db = CreateContext();
            var admin = new SubjectAdmin(db, new CurriculumOfferingUsageInspector(db));
            var subject = await admin.DefineSubjectAsync("MATH3", "رياضيات", "Math", "core");
            var offering = await admin.DefineOfferingAsync(_profileId, subject.Id, 5, true, 10, false, null, new DateTime(2026, 9, 1));

            await admin.RemoveOfferingAsync(offering.Id);

            // Gone, not end-dated: this is the line added to the wrong grade and caught before use,
            // which end-dating would leave on the plan for ever as a row nobody can explain.
            Assert.Empty(db.CurriculumOfferings.IgnoreQueryFilters().Where(o => o.Id == offering.Id));
        }

        [Fact]
        [BusinessRule("BR-SUB-004")]
        public async Task An_offering_a_term_result_points_at_is_refused_and_end_dating_is_what_applies()
        {
            using var db = CreateContext();
            var admin = new SubjectAdmin(db, new CurriculumOfferingUsageInspector(db));
            var subject = await admin.DefineSubjectAsync("MATH3", "رياضيات", "Math", "core");
            var offering = await admin.DefineOfferingAsync(_profileId, subject.Id, 5, true, 10, false, null, new DateTime(2026, 9, 1));

            db.TermResults.Add(new TermResult
            {
                AcademicYearId = offering.AcademicYearId,
                EnrollmentId = 1,
                CurriculumOfferingId = offering.Id,
                TermId = 1,
                ScorePercent = 88m,
                CalculationSnapshotJson = "{}",
                PublishedAtUtc = new DateTime(2027, 1, 20),
            });
            await db.SaveChangesAsync();

            var refusal = await Assert.ThrowsAsync<RecordInUseException>(() => admin.RemoveOfferingAsync(offering.Id));

            // The refusal has to name what is in the way; "in use" on its own is a support call.
            Assert.Contains("term result", refusal.Usage.Describe(arabic: false));
            Assert.NotEmpty(refusal.Usage.Describe(arabic: true));
            Assert.Single(db.CurriculumOfferings.Where(o => o.Id == offering.Id));

            // And the operation the refusal points at still works, which is the whole of BR-SUB-004:
            // the line closes, and the result goes on pointing at it.
            await admin.EndDateOfferingAsync(offering.Id, new DateTime(2027, 6, 30));
            Assert.NotNull(db.CurriculumOfferings.Single(o => o.Id == offering.Id).EffectiveToUtc);
        }

        [Fact]
        [BusinessRule("BR-LRN-001")]
        public async Task An_offering_a_lesson_points_at_is_refused_before_the_foreign_key_can_fire()
        {
            using var db = CreateContext();
            var admin = new SubjectAdmin(db, new CurriculumOfferingUsageInspector(db));
            var subject = await admin.DefineSubjectAsync("MATH3", "رياضيات", "Math", "core");
            var offering = await admin.DefineOfferingAsync(_profileId, subject.Id, 5, true, 10, false, null, new DateTime(2026, 9, 1));

            db.Lessons.Add(new Lesson
            {
                AcademicYearId = offering.AcademicYearId,
                CurriculumOfferingId = offering.Id,
                TitleAr = "الكسور",
                TitleEn = "Fractions",
            });
            await db.SaveChangesAsync();

            // BR-SUB-004 does not list lessons; BR-LRN-001 anchors them on the offering and promises
            // the content survives a curriculum change. Without this count the removal reached a
            // Restrict foreign key and came back as a 500 rather than as an answer.
            var refusal = await Assert.ThrowsAsync<RecordInUseException>(() => admin.RemoveOfferingAsync(offering.Id));
            Assert.Contains("lesson", refusal.Usage.Describe(arabic: false));
        }
    }
}
