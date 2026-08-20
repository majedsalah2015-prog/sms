using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Grades;
using Sms.Domain.Schools;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Grades;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// E-103 (slice: Grades, doc/Modules/05, BR-GRD-001/002/004/007/009)
    /// over a real Sqlite-backed AppDbContext.
    /// </summary>
    public sealed class GradeStructureAdminTests : IDisposable
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
        private int _yearId;

        public GradeStructureAdminTests()
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
            db.SaveChanges();
            _yearId = year.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        // --- stage/grade definition ------------------------------------------------

        [Fact]
        [BusinessRule("BR-GRD-001")]
        public async Task Defining_a_stage_and_grade_links_them()
        {
            using var db = CreateContext();
            var admin = new GradeStructureAdmin(db);
            var stage = await admin.DefineStageAsync("الابتدائية", "Elementary", 1, GenderPolicy.Mixed);

            var grade = await admin.DefineGradeLevelAsync(
                stage.Id, "G5", "الصف الخامس", "Grade 5", 5, promotionTargetGradeLevelId: null, isGraduating: false);

            Assert.Equal(stage.Id, db.GradeLevels.Single(g => g.Id == grade.Id).StageId);
        }

        [Fact]
        [BusinessRule("BR-GRD-009")]
        public async Task A_duplicate_grade_code_is_rejected()
        {
            using var db = CreateContext();
            var admin = new GradeStructureAdmin(db);
            var stage = await admin.DefineStageAsync("الابتدائية", "Elementary", 1, GenderPolicy.Mixed);
            await admin.DefineGradeLevelAsync(stage.Id, "G5", "الصف الخامس", "Grade 5", 5, null, false);

            await Assert.ThrowsAsync<DuplicateGradeCodeException>(() =>
                admin.DefineGradeLevelAsync(stage.Id, "G5", "خامس آخر", "Another G5", 6, null, false));
        }

        // --- BR-GRD-004 gender-policy narrowing at the year-profile level ----------

        [Fact]
        [BusinessRule("BR-GRD-004")]
        public async Task A_grade_year_profile_can_narrow_a_mixed_stage_to_a_single_gender()
        {
            using var db = CreateContext();
            var admin = new GradeStructureAdmin(db);
            var stage = await admin.DefineStageAsync("الثانوية", "Secondary", 1, GenderPolicy.Mixed);
            var grade = await admin.DefineGradeLevelAsync(stage.Id, "G10", "عاشر", "Grade 10", 10, null, false);

            var profile = await admin.DefineGradeYearProfileAsync(
                grade.Id, _yearId, GenderPolicy.Boys, targetSections: 3, targetSectionSize: 25);

            Assert.Equal(GenderPolicy.Boys, db.GradeYearProfiles.Single(p => p.Id == profile.Id).GenderPolicy);
        }

        [Fact]
        [BusinessRule("BR-GRD-004")]
        public async Task A_grade_year_profile_cannot_widen_a_single_gender_stage()
        {
            using var db = CreateContext();
            var admin = new GradeStructureAdmin(db);
            var stage = await admin.DefineStageAsync("بنين فقط", "Boys Only", 1, GenderPolicy.Boys);
            var grade = await admin.DefineGradeLevelAsync(stage.Id, "G1", "أول", "Grade 1", 1, null, false);

            await Assert.ThrowsAsync<InvalidGenderPolicyNarrowingException>(() =>
                admin.DefineGradeYearProfileAsync(grade.Id, _yearId, GenderPolicy.Mixed, 2, 20));
        }

        // --- BR-GRD-006 capacity + BR-GRD-008 year-versioning upsert ----------------

        [Fact]
        [BusinessRule("BR-GRD-006")]
        public async Task Redefining_the_same_grade_years_profile_upserts_rather_than_duplicating()
        {
            using var db = CreateContext();
            var admin = new GradeStructureAdmin(db);
            var stage = await admin.DefineStageAsync("الابتدائية", "Elementary", 1, GenderPolicy.Mixed);
            var grade = await admin.DefineGradeLevelAsync(stage.Id, "G3", "ثالث", "Grade 3", 3, null, false);

            await admin.DefineGradeYearProfileAsync(grade.Id, _yearId, GenderPolicy.Mixed, 2, 20);
            await admin.DefineGradeYearProfileAsync(grade.Id, _yearId, GenderPolicy.Mixed, 3, 25);

            var stored = Assert.Single(db.GradeYearProfiles.Where(p => p.GradeLevelId == grade.Id && p.AcademicYearId == _yearId));
            Assert.Equal(3, stored.TargetSections);
            Assert.Equal(25, stored.TargetSectionSize);
        }

        // --- BR-GRD-007 deactivate-only -----------------------------------------

        [Fact]
        [BusinessRule("BR-GRD-007")]
        public async Task A_grade_is_deactivatable_not_hard_deletable()
        {
            using var db = CreateContext();
            var admin = new GradeStructureAdmin(db);
            var stage = await admin.DefineStageAsync("الابتدائية", "Elementary", 1, GenderPolicy.Mixed);
            var grade = await admin.DefineGradeLevelAsync(stage.Id, "G4", "رابع", "Grade 4", 4, null, false);

            var tracked = await db.GradeLevels.SingleAsync(g => g.Id == grade.Id);
            tracked.IsActive = false;
            await db.SaveChangesAsync();
            Assert.False(db.GradeLevels.IgnoreQueryFilters().Single(g => g.Id == grade.Id).IsActive);

            db.GradeLevels.Remove(tracked);
            await Assert.ThrowsAsync<HardDeleteForbiddenException>(() => db.SaveChangesAsync());
        }

        [Fact]
        [BusinessRule("BR-GRD-002")]
        public async Task Promotion_path_can_be_set_later_but_never_form_a_cycle()
        {
            using var db = CreateContext();
            var admin = new GradeStructureAdmin(db);
            var stage = await admin.DefineStageAsync("الابتدائية", "Elementary", 1, GenderPolicy.Mixed);
            var g1 = await admin.DefineGradeLevelAsync(stage.Id, "G1", "أول", "Grade 1", 1, null, false);
            var g2 = await admin.DefineGradeLevelAsync(stage.Id, "G2", "ثاني", "Grade 2", 2, null, false);

            await admin.SetPromotionPathAsync(g1.Id, g2.Id, false);
            Assert.Equal(g2.Id, db.GradeLevels.Single(g => g.Id == g1.Id).PromotionTargetGradeLevelId);

            await Assert.ThrowsAsync<PromotionPathCycleException>(() => admin.SetPromotionPathAsync(g2.Id, g1.Id, false));
            await Assert.ThrowsAsync<PromotionPathCycleException>(() => admin.SetPromotionPathAsync(g2.Id, g2.Id, false));

            await admin.SetPromotionPathAsync(g2.Id, null, true);
            Assert.True(db.GradeLevels.Single(g => g.Id == g2.Id).IsGraduating);
        }

        // --- edit / soft-delete of stages, grades and profiles ----------------------

        [Fact]
        [BusinessRule("BR-GRD-004")]
        public async Task A_stage_can_be_edited_but_not_narrowed_below_an_existing_profile()
        {
            using var db = CreateContext();
            var admin = new GradeStructureAdmin(db);
            var stage = await admin.DefineStageAsync("الابتدائية", "Elementary", 1, GenderPolicy.Mixed);
            var grade = await admin.DefineGradeLevelAsync(stage.Id, "G1", "أول", "Grade 1", 1, null, false);
            await admin.DefineGradeYearProfileAsync(grade.Id, _yearId, GenderPolicy.Boys, 2, 25);

            await admin.UpdateStageAsync(stage.Id, "المرحلة الابتدائية", "Primary", 2, GenderPolicy.Boys); // Boys profile still fits
            var saved = db.Stages.Single(s => s.Id == stage.Id);
            Assert.Equal("Primary", saved.Name.NameEn);
            Assert.Equal(2, saved.SequenceOrder);

            await Assert.ThrowsAsync<InvalidGenderPolicyNarrowingException>(() =>
                admin.UpdateStageAsync(stage.Id, "أ", "A", 2, GenderPolicy.Girls)); // Boys profile would no longer narrow Girls
        }

        [Fact]
        [BusinessRule("BR-GRD-007")]
        public async Task A_stage_with_grades_cannot_be_removed_but_an_empty_one_is_deactivated()
        {
            using var db = CreateContext();
            var admin = new GradeStructureAdmin(db);
            var stage = await admin.DefineStageAsync("الابتدائية", "Elementary", 1, GenderPolicy.Mixed);
            var grade = await admin.DefineGradeLevelAsync(stage.Id, "G1", "أول", "Grade 1", 1, null, true);

            await Assert.ThrowsAsync<GradeStructureInUseException>(() => admin.DeactivateStageAsync(stage.Id));

            await admin.DeactivateGradeLevelAsync(grade.Id);
            await admin.DeactivateStageAsync(stage.Id);
            Assert.Empty(db.Stages.Where(s => s.Id == stage.Id)); // hidden by the soft-active filter
            Assert.False(db.Stages.IgnoreQueryFilters().Single(s => s.Id == stage.Id).IsActive);
        }

        [Fact]
        [BusinessRule("BR-GRD-009")]
        public async Task Editing_a_grade_keeps_codes_unique()
        {
            using var db = CreateContext();
            var admin = new GradeStructureAdmin(db);
            var stage = await admin.DefineStageAsync("الابتدائية", "Elementary", 1, GenderPolicy.Mixed);
            var g1 = await admin.DefineGradeLevelAsync(stage.Id, "G1", "أول", "Grade 1", 1, null, false);
            var g2 = await admin.DefineGradeLevelAsync(stage.Id, "G2", "ثاني", "Grade 2", 2, null, true);

            await admin.UpdateGradeLevelAsync(g1.Id, stage.Id, "G1", "الصف الأول", "First grade", 1); // same code on itself is fine
            Assert.Equal("First grade", db.GradeLevels.Single(g => g.Id == g1.Id).Name.NameEn);

            await Assert.ThrowsAsync<DuplicateGradeCodeException>(() => admin.UpdateGradeLevelAsync(g2.Id, stage.Id, "G1", "ث", "Second", 2));
        }

        [Fact]
        [BusinessRule("BR-GRD-007")]
        public async Task A_grade_with_a_feeder_or_sections_cannot_be_removed()
        {
            using var db = CreateContext();
            var admin = new GradeStructureAdmin(db);
            var stage = await admin.DefineStageAsync("الابتدائية", "Elementary", 1, GenderPolicy.Mixed);
            var g1 = await admin.DefineGradeLevelAsync(stage.Id, "G1", "أول", "Grade 1", 1, null, false);
            var g2 = await admin.DefineGradeLevelAsync(stage.Id, "G2", "ثاني", "Grade 2", 2, null, true);
            await admin.SetPromotionPathAsync(g1.Id, g2.Id, false);

            // g1 promotes into g2 → g2 blocked until the path is changed
            await Assert.ThrowsAsync<GradeStructureInUseException>(() => admin.DeactivateGradeLevelAsync(g2.Id));

            var profile = await admin.DefineGradeYearProfileAsync(g1.Id, _yearId, GenderPolicy.Mixed, 1, 25);
            db.Sections.Add(new Sms.Domain.Sections.Section { AcademicYearId = _yearId, GradeYearProfileId = profile.Id, NameAr = "أول-أ", NameEn = "1-A", Capacity = 25 });
            await db.SaveChangesAsync();
            await Assert.ThrowsAsync<GradeStructureInUseException>(() => admin.DeactivateGradeLevelAsync(g1.Id));
            await Assert.ThrowsAsync<GradeStructureInUseException>(() => admin.RemoveGradeYearProfileAsync(profile.Id));
        }

        [Fact]
        public async Task Removing_an_unused_profile_deactivates_it_and_redefining_reactivates_it()
        {
            using var db = CreateContext();
            var admin = new GradeStructureAdmin(db);
            var stage = await admin.DefineStageAsync("الابتدائية", "Elementary", 1, GenderPolicy.Mixed);
            var g1 = await admin.DefineGradeLevelAsync(stage.Id, "G1", "أول", "Grade 1", 1, null, true);
            var profile = await admin.DefineGradeYearProfileAsync(g1.Id, _yearId, GenderPolicy.Mixed, 1, 25);

            await admin.RemoveGradeYearProfileAsync(profile.Id);
            Assert.False(db.GradeYearProfiles.Single(p => p.Id == profile.Id).IsActive);

            var again = await admin.DefineGradeYearProfileAsync(g1.Id, _yearId, GenderPolicy.Mixed, 2, 20);
            Assert.Equal(profile.Id, again.Id);
            Assert.True(again.IsActive);
        }
    }
}
