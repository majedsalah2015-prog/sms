using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Common;
using Sms.Domain.Grades;
using Sms.Domain.Grading;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Students;
using Sms.Domain.Subjects;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Grading;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// S3/E-302 (Grading - basic subset, doc/Modules/17, BR-GRA-001/003/005)
    /// over a real Sqlite-backed AppDbContext.
    /// </summary>
    public sealed class GradingAdminTests : IDisposable
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
        private int _stageId;
        private int _offeringId;
        private int _sectionId;
        private int _termId;
        private int _profileId;
        private int _yearId;

        public GradingAdminTests()
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

            var subject = new Subject { SchoolId = 1, Code = "MATH", Name = new LocalizedName("رياضيات", "Math"), Category = "core" };
            db.Subjects.Add(subject);
            db.SaveChanges();

            var offering = new CurriculumOffering
            {
                SchoolId = 1, AcademicYearId = year.Id, GradeYearProfileId = profile.Id, SubjectId = subject.Id,
                WeeklyPeriods = 5, IsAssessable = true, GpaWeight = 1m, EffectiveFromUtc = new DateTime(2026, 9, 1),
            };
            db.CurriculumOfferings.Add(offering);

            var section = new Section { SchoolId = 1, AcademicYearId = year.Id, GradeYearProfileId = profile.Id, NameAr = "ثالث-أ", NameEn = "3-A", Capacity = 25, GenderPolicy = GenderPolicy.Mixed };
            db.Sections.Add(section);
            db.SaveChanges();

            var semester = new Semester { AcademicYearId = year.Id, SequenceNumber = 1, NameAr = "الفصل الأول", NameEn = "Semester 1", StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 1, 31) };
            db.Semesters.Add(semester);
            db.SaveChanges();

            var term = new Term { AcademicYearId = year.Id, SemesterId = semester.Id, SequenceNumber = 1, NameAr = "الفترة الأولى", NameEn = "Term 1", StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2026, 11, 30) };
            db.Terms.Add(term);
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

            db.SectionMemberships.Add(new SectionMembership
            {
                AcademicYearId = year.Id, SectionId = section.Id, EnrollmentId = enrollment.Id, EffectiveFromUtc = new DateTime(2026, 9, 1),
            });
            db.SaveChanges();

            _stageId = stage.Id;
            _offeringId = offering.Id;
            _sectionId = section.Id;
            _termId = term.Id;
            _profileId = profile.Id;
            _yearId = year.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private async Task<int> DefineStandardScale(GradingAdmin admin)
        {
            var scale = await admin.DefineScaleAsync(_stageId, "نسبة مئوية", "Percentage");
            await admin.AddScaleBandAsync(scale.Id, 90m, 100m, "A", "ممتاز", "Excellent", isPassing: true, sortOrder: 1);
            await admin.AddScaleBandAsync(scale.Id, 50m, 89.99m, "P", "ناجح", "Pass", isPassing: true, sortOrder: 2);
            await admin.AddScaleBandAsync(scale.Id, 0m, 49.99m, "F", "راسب", "Fail", isPassing: false, sortOrder: 3);
            return scale.Id;
        }

        private async Task<int> DefineFinalizedBlueprint(GradingAdmin admin, int scaleId)
        {
            var blueprint = await admin.DefineBlueprintAsync(_offeringId, _termId, scaleId);
            await admin.AddBlueprintComponentAsync(blueprint.Id, "اختبار قصير", "Quiz", weight: 30m, maxScore: 20m);
            await admin.AddBlueprintComponentAsync(blueprint.Id, "نصف الفصل", "Midterm", weight: 70m, maxScore: 100m);
            await admin.LockBlueprintAsync(blueprint.Id);
            return blueprint.Id;
        }

        // --- BR-GRA-001 scales ----------------------------------------------------

        [Fact]
        [BusinessRule("BR-GRA-001")]
        public async Task Adding_a_band_to_a_locked_scale_is_rejected()
        {
            using var db = CreateContext();
            var admin = new GradingAdmin(db, _clock, _audit);
            var scale = await admin.DefineScaleAsync(_stageId, "نسبة مئوية", "Percentage");
            await admin.LockScaleAsync(scale.Id);

            await Assert.ThrowsAsync<GradingScaleLockedException>(() =>
                admin.AddScaleBandAsync(scale.Id, 0m, 100m, "X", "س", "X", isPassing: true, sortOrder: 1));
        }

        // --- BR-GRA-003 blueprint weights ------------------------------------------

        [Fact]
        [BusinessRule("BR-GRA-003")]
        public async Task Finalizing_a_blueprint_whose_weights_dont_sum_to_100_is_rejected()
        {
            using var db = CreateContext();
            var admin = new GradingAdmin(db, _clock, _audit);
            var scaleId = await DefineStandardScale(admin);
            var blueprint = await admin.DefineBlueprintAsync(_offeringId, _termId, scaleId);
            await admin.AddBlueprintComponentAsync(blueprint.Id, "اختبار", "Quiz", weight: 30m, maxScore: 20m);

            await Assert.ThrowsAsync<BlueprintWeightMismatchException>(() => admin.LockBlueprintAsync(blueprint.Id));
        }

        [Fact]
        [BusinessRule("BR-GRA-003")]
        public async Task Adding_a_component_to_a_finalized_blueprint_is_rejected()
        {
            using var db = CreateContext();
            var admin = new GradingAdmin(db, _clock, _audit);
            var scaleId = await DefineStandardScale(admin);
            var blueprintId = await DefineFinalizedBlueprint(admin, scaleId);

            await Assert.ThrowsAsync<BlueprintLockedException>(() =>
                admin.AddBlueprintComponentAsync(blueprintId, "إضافي", "Extra", weight: 10m, maxScore: 10m));
        }

        // --- Marksheet creation + BR-GRA-005 status flow ---------------------------

        [Fact]
        [BusinessRule("BR-GRA-003")]
        public async Task Creating_a_marksheet_from_an_unfinalized_blueprint_is_rejected()
        {
            using var db = CreateContext();
            var admin = new GradingAdmin(db, _clock, _audit);
            var scaleId = await DefineStandardScale(admin);
            var blueprint = await admin.DefineBlueprintAsync(_offeringId, _termId, scaleId);

            await Assert.ThrowsAsync<BlueprintNotFinalizedException>(() => admin.CreateMarksheetAsync(blueprint.Id, _sectionId));
        }

        [Fact]
        [BusinessRule("BR-GRA-005")]
        public async Task Creating_a_marksheet_seeds_one_stub_entry_per_member_per_component()
        {
            using var db = CreateContext();
            var admin = new GradingAdmin(db, _clock, _audit);
            var scaleId = await DefineStandardScale(admin);
            var blueprintId = await DefineFinalizedBlueprint(admin, scaleId);

            var marksheet = await admin.CreateMarksheetAsync(blueprintId, _sectionId);

            Assert.Equal(2, db.MarkEntries.Count(e => e.MarksheetId == marksheet.Id)); // 1 student x 2 components
        }

        [Fact]
        [BusinessRule("BR-GRA-005")]
        public async Task Publishing_with_unresolved_entries_is_rejected()
        {
            using var db = CreateContext();
            var admin = new GradingAdmin(db, _clock, _audit);
            var scaleId = await DefineStandardScale(admin);
            var blueprintId = await DefineFinalizedBlueprint(admin, scaleId);
            var marksheet = await admin.CreateMarksheetAsync(blueprintId, _sectionId);
            await admin.ChangeMarksheetStatusAsync(marksheet.Id, MarksheetStatus.Submitted);
            await admin.ChangeMarksheetStatusAsync(marksheet.Id, MarksheetStatus.HoDReviewed);
            await admin.ChangeMarksheetStatusAsync(marksheet.Id, MarksheetStatus.Approved);

            await Assert.ThrowsAsync<UnresolvedMarkEntriesException>(() =>
                admin.ChangeMarksheetStatusAsync(marksheet.Id, MarksheetStatus.Published));
        }

        [Fact]
        [BusinessRule("BR-GRA-003")]
        public async Task Publishing_a_fully_resolved_marksheet_computes_a_term_result_with_a_snapshot()
        {
            using var db = CreateContext();
            var admin = new GradingAdmin(db, _clock, _audit);
            var scaleId = await DefineStandardScale(admin);
            var blueprintId = await DefineFinalizedBlueprint(admin, scaleId);
            var marksheet = await admin.CreateMarksheetAsync(blueprintId, _sectionId);

            var components = db.BlueprintComponents.Where(c => c.BlueprintId == blueprintId).OrderBy(c => c.NameEn).ToList();
            var enrollmentId = db.MarkEntries.First(e => e.MarksheetId == marksheet.Id).EnrollmentId;
            // Midterm (weight 70, max 100): 80 -> 80%; Quiz (weight 30, max 20): 18 -> 90%
            foreach (var component in components)
            {
                var score = component.NameEn == "Midterm" ? 80m : 18m;
                await admin.EnterMarkAsync(marksheet.Id, component.Id, enrollmentId, score, isAbsent: false, isExempt: false);
            }

            await admin.ChangeMarksheetStatusAsync(marksheet.Id, MarksheetStatus.Submitted);
            await admin.ChangeMarksheetStatusAsync(marksheet.Id, MarksheetStatus.HoDReviewed);
            await admin.ChangeMarksheetStatusAsync(marksheet.Id, MarksheetStatus.Approved);
            await admin.ChangeMarksheetStatusAsync(marksheet.Id, MarksheetStatus.Published);

            // weighted = 90*30 + 80*70 = 2700 + 5600 = 8300 / 100 = 83.00
            var result = db.TermResults.Single(r => r.EnrollmentId == enrollmentId);
            Assert.Equal(83.00m, result.ScorePercent);
            Assert.NotEmpty(result.CalculationSnapshotJson);
            var band = db.ScaleBands.Single(b => b.Id == result.ScaleBandId);
            Assert.Equal("P", band.BandCode);
        }

        [Fact]
        [BusinessRule("BR-GRA-005")]
        public async Task Changing_status_along_an_illegal_path_is_rejected()
        {
            using var db = CreateContext();
            var admin = new GradingAdmin(db, _clock, _audit);
            var scaleId = await DefineStandardScale(admin);
            var blueprintId = await DefineFinalizedBlueprint(admin, scaleId);
            var marksheet = await admin.CreateMarksheetAsync(blueprintId, _sectionId);

            await Assert.ThrowsAsync<InvalidMarksheetStatusTransitionException>(() =>
                admin.ChangeMarksheetStatusAsync(marksheet.Id, MarksheetStatus.Approved));
        }

        // --- S4/E-402: BR-GRA-005 WF-08 correction ----------------------------------

        private async Task<(int marksheetId, int enrollmentId)> PublishScoredMarksheetAsync(
            AppDbContext db, GradingAdmin admin, int blueprintId, decimal midterm, decimal quiz)
        {
            var marksheet = await admin.CreateMarksheetAsync(blueprintId, _sectionId);
            var components = db.BlueprintComponents.Where(c => c.BlueprintId == blueprintId).OrderBy(c => c.NameEn).ToList();
            var enrollmentId = db.MarkEntries.First(e => e.MarksheetId == marksheet.Id).EnrollmentId;
            foreach (var component in components)
            {
                var score = component.NameEn == "Midterm" ? midterm : quiz;
                await admin.EnterMarkAsync(marksheet.Id, component.Id, enrollmentId, score, isAbsent: false, isExempt: false);
            }

            await admin.ChangeMarksheetStatusAsync(marksheet.Id, MarksheetStatus.Submitted);
            await admin.ChangeMarksheetStatusAsync(marksheet.Id, MarksheetStatus.HoDReviewed);
            await admin.ChangeMarksheetStatusAsync(marksheet.Id, MarksheetStatus.Approved);
            await admin.ChangeMarksheetStatusAsync(marksheet.Id, MarksheetStatus.Published);
            return (marksheet.Id, enrollmentId);
        }

        [Fact]
        [BusinessRule("BR-GRA-005")]
        public async Task Correcting_a_published_marksheet_and_republishing_updates_the_same_term_result()
        {
            using var db = CreateContext();
            var admin = new GradingAdmin(db, _clock, _audit);
            var scaleId = await DefineStandardScale(admin);
            var blueprintId = await DefineFinalizedBlueprint(admin, scaleId);
            var (marksheetId, enrollmentId) = await PublishScoredMarksheetAsync(db, admin, blueprintId, midterm: 80m, quiz: 18m);
            Assert.Equal(83.00m, db.TermResults.Single(r => r.EnrollmentId == enrollmentId).ScorePercent);

            await admin.CorrectPublishedMarksheetAsync(marksheetId, "typo in midterm score");
            var midtermComponent = db.BlueprintComponents.Single(c => c.BlueprintId == blueprintId && c.NameEn == "Midterm");
            await admin.EnterMarkAsync(marksheetId, midtermComponent.Id, enrollmentId, 100m, isAbsent: false, isExempt: false);
            await admin.ChangeMarksheetStatusAsync(marksheetId, MarksheetStatus.Submitted);
            await admin.ChangeMarksheetStatusAsync(marksheetId, MarksheetStatus.HoDReviewed);
            await admin.ChangeMarksheetStatusAsync(marksheetId, MarksheetStatus.Approved);
            await admin.ChangeMarksheetStatusAsync(marksheetId, MarksheetStatus.Published);

            // weighted = 90*30 + 100*70 = 9700 / 100 = 97.00 - and still exactly one TermResult row (upsert, not a duplicate).
            var results = db.TermResults.Where(r => r.EnrollmentId == enrollmentId).ToList();
            Assert.Single(results);
            Assert.Equal(97.00m, results[0].ScorePercent);
        }

        [Fact]
        [BusinessRule("BR-GRA-005")]
        public async Task Correcting_a_marksheet_that_isnt_published_is_rejected()
        {
            using var db = CreateContext();
            var admin = new GradingAdmin(db, _clock, _audit);
            var scaleId = await DefineStandardScale(admin);
            var blueprintId = await DefineFinalizedBlueprint(admin, scaleId);
            var marksheet = await admin.CreateMarksheetAsync(blueprintId, _sectionId); // still Draft

            await Assert.ThrowsAsync<InvalidMarksheetStatusTransitionException>(() =>
                admin.CorrectPublishedMarksheetAsync(marksheet.Id, "oops"));
        }

        // --- S4/E-402: BR-GRA-006/007 year result + promotion -----------------------

        [Fact]
        [BusinessRule("BR-GRA-006")]
        public async Task A_passing_student_with_no_failed_subjects_is_promoted()
        {
            using var db = CreateContext();
            var admin = new GradingAdmin(db, _clock, _audit);
            var scaleId = await DefineStandardScale(admin);
            var blueprintId = await DefineFinalizedBlueprint(admin, scaleId);
            var (_, enrollmentId) = await PublishScoredMarksheetAsync(db, admin, blueprintId, midterm: 80m, quiz: 18m); // 83.00%, band P
            await admin.DefinePromotionCriteriaAsync(_profileId, overallPassMark: 50m, maxFailedSubjectsForPromotion: 1);

            var yearResult = await admin.ComputeYearResultAsync(enrollmentId, _yearId, _profileId);

            Assert.Equal(0, yearResult.FailedSubjectCount);
            Assert.Equal(PromotionOutcome.Promote, yearResult.PromotionOutcome);
        }

        [Fact]
        [BusinessRule("BR-GRA-006")]
        public async Task A_student_below_the_overall_pass_mark_is_retained()
        {
            using var db = CreateContext();
            var admin = new GradingAdmin(db, _clock, _audit);
            var scaleId = await DefineStandardScale(admin);
            var blueprintId = await DefineFinalizedBlueprint(admin, scaleId);
            var (_, enrollmentId) = await PublishScoredMarksheetAsync(db, admin, blueprintId, midterm: 30m, quiz: 10m); // 36.00%, band F
            await admin.DefinePromotionCriteriaAsync(_profileId, overallPassMark: 50m, maxFailedSubjectsForPromotion: 1);

            var yearResult = await admin.ComputeYearResultAsync(enrollmentId, _yearId, _profileId);

            Assert.Equal(1, yearResult.FailedSubjectCount);
            Assert.Equal(PromotionOutcome.Retain, yearResult.PromotionOutcome);
        }
    }
}
