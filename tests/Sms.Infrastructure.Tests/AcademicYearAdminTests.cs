using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Common;
using Sms.Domain.Grades;
using Sms.Domain.Schools;
using Sms.Domain.Students;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Schools;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// E-102 (remaining piece): Academic Years (BR-AYR-001/002/004/005/006)
    /// over a real Sqlite-backed AppDbContext — including the filtered
    /// unique indexes from DB doc A1.
    /// </summary>
    public sealed class AcademicYearAdminTests : IDisposable
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

        public AcademicYearAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private static Task<AcademicYear> DefineYear(AcademicYearAdmin admin, DateTime start, DateTime end, string label = "2026-2027")
            => admin.DefineYearAsync($"عام {label}", label, "١٤٤٨هـ", start, end);

        // --- BR-AYR-001 dates -----------------------------------------------------

        [Fact]
        [BusinessRule("BR-AYR-001")]
        public async Task Defining_a_year_with_an_invalid_span_is_rejected()
        {
            using var db = CreateContext();
            var admin = new AcademicYearAdmin(db);

            await Assert.ThrowsAsync<InvalidAcademicYearDatesException>(() =>
                DefineYear(admin, new DateTime(2026, 9, 1), new DateTime(2026, 10, 1)));
        }

        [Fact]
        [BusinessRule("BR-AYR-001")]
        public async Task Defining_a_year_that_overlaps_an_existing_one_is_rejected()
        {
            using var db = CreateContext();
            var admin = new AcademicYearAdmin(db);
            await DefineYear(admin, new DateTime(2026, 9, 1), new DateTime(2027, 6, 30));

            await Assert.ThrowsAsync<InvalidAcademicYearDatesException>(() =>
                DefineYear(admin, new DateTime(2027, 1, 1), new DateTime(2027, 12, 1), "overlap"));
        }

        // --- BR-AYR-002 one Active / one Preparation, enforced two ways ------------

        [Fact]
        [BusinessRule("BR-AYR-002")]
        public async Task A_second_Preparation_year_is_rejected_by_the_proactive_check()
        {
            using var db = CreateContext();
            var admin = new AcademicYearAdmin(db);
            await DefineYear(admin, new DateTime(2026, 9, 1), new DateTime(2027, 6, 30));

            await Assert.ThrowsAsync<DuplicatePreparationYearException>(() =>
                DefineYear(admin, new DateTime(2028, 9, 1), new DateTime(2029, 6, 30), "2028-2029"));
        }

        [Fact]
        [BusinessRule("BR-AYR-002")]
        public async Task The_filtered_unique_index_is_the_concurrency_backstop_for_one_Active_year()
        {
            using var db = CreateContext();
            // Bypass the admin service's proactive check to prove the DB constraint itself holds.
            db.AcademicYears.Add(new AcademicYear
            {
                LabelAr = "أ", LabelEn = "A", HijriLabel = "h", StartDate = new DateTime(2025, 9, 1), EndDate = new DateTime(2026, 6, 30),
                Status = AcademicYearStatus.Active,
            });
            db.AcademicYears.Add(new AcademicYear
            {
                LabelAr = "ب", LabelEn = "B", HijriLabel = "h", StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 6, 30),
                Status = AcademicYearStatus.Active,
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        // --- BR-AYR-004/005/006 lifecycle ------------------------------------------

        [Fact]
        [BusinessRule("BR-AYR-004")]
        public async Task Activating_a_year_moves_the_prior_Active_year_to_Closing()
        {
            using var db = CreateContext();
            var admin = new AcademicYearAdmin(db);
            var first = await DefineYear(admin, new DateTime(2025, 9, 1), new DateTime(2026, 6, 30), "2025-2026");
            await admin.ActivateAsync(first.Id);

            var second = await DefineYear(admin, new DateTime(2026, 9, 1), new DateTime(2027, 6, 30), "2026-2027");
            await admin.ActivateAsync(second.Id);

            Assert.Equal(AcademicYearStatus.Closing, db.AcademicYears.Single(y => y.Id == first.Id).Status);
            Assert.Equal(AcademicYearStatus.Active, db.AcademicYears.Single(y => y.Id == second.Id).Status);
        }

        [Fact]
        [BusinessRule("BR-AYR-002")]
        public async Task An_illegal_status_move_is_rejected()
        {
            using var db = CreateContext();
            var admin = new AcademicYearAdmin(db);
            var year = await DefineYear(admin, new DateTime(2026, 9, 1), new DateTime(2027, 6, 30));

            await Assert.ThrowsAsync<InvalidAcademicYearStatusTransitionException>(() => admin.CloseAsync(year.Id)); // Preparation -> Closed skips Active/Closing
        }

        [Fact]
        [BusinessRule("BR-AYR-006")]
        public async Task The_full_lifecycle_runs_end_to_end()
        {
            using var db = CreateContext();
            var admin = new AcademicYearAdmin(db);
            var year = await DefineYear(admin, new DateTime(2026, 9, 1), new DateTime(2027, 6, 30));

            await admin.ActivateAsync(year.Id);
            Assert.Equal(AcademicYearStatus.Active, db.AcademicYears.Single(y => y.Id == year.Id).Status);

            // Direct Active -> Closing is a legal state-machine move even without
            // another year's activation triggering it (e.g. manual admin action).
            var reActivated = await DefineYear(admin, new DateTime(2027, 9, 1), new DateTime(2028, 6, 30), "2027-2028");
            await admin.ActivateAsync(reActivated.Id); // this pushes `year` into Closing

            await admin.CloseAsync(year.Id);
            await admin.ArchiveAsync(year.Id);

            Assert.Equal(AcademicYearStatus.Archived, db.AcademicYears.Single(y => y.Id == year.Id).Status);
        }

        // --- BR-AYR-007 semester/term builder -----------------------------------------

        [Fact]
        [BusinessRule("BR-AYR-007")]
        public async Task Semesters_and_terms_nest_inside_their_parent_and_never_overlap_siblings()
        {
            using var db = CreateContext();
            var admin = new AcademicYearAdmin(db);
            var year = await DefineYear(admin, new DateTime(2026, 9, 1), new DateTime(2027, 6, 30));

            var s1 = await admin.DefineSemesterAsync(year.Id, 1, "الفصل الأول", "Semester 1", new DateTime(2026, 9, 1), new DateTime(2027, 1, 31));
            await admin.DefineSemesterAsync(year.Id, 2, "الفصل الثاني", "Semester 2", new DateTime(2027, 2, 1), new DateTime(2027, 6, 30));

            // outside the year
            await Assert.ThrowsAsync<InvalidPeriodDatesException>(() =>
                admin.DefineSemesterAsync(year.Id, 3, "س", "S3", new DateTime(2027, 6, 1), new DateTime(2027, 8, 1)));
            // overlapping a sibling
            await Assert.ThrowsAsync<InvalidPeriodDatesException>(() =>
                admin.DefineSemesterAsync(year.Id, 3, "س", "S3", new DateTime(2027, 1, 15), new DateTime(2027, 3, 1)));

            var t1 = await admin.DefineTermAsync(s1.Id, 1, "الفترة الأولى", "Term 1", new DateTime(2026, 9, 1), new DateTime(2026, 11, 15));
            await admin.DefineTermAsync(s1.Id, 2, "الفترة الثانية", "Term 2", new DateTime(2026, 11, 16), new DateTime(2027, 1, 31));
            Assert.Equal(year.Id, db.Terms.Single(t => t.Id == t1.Id).AcademicYearId);

            // term outside its semester
            await Assert.ThrowsAsync<InvalidPeriodDatesException>(() =>
                admin.DefineTermAsync(s1.Id, 3, "ف", "T3", new DateTime(2027, 1, 20), new DateTime(2027, 2, 20)));

            // re-defining by sequence updates in place; shrinking below its terms is refused
            await Assert.ThrowsAsync<InvalidPeriodDatesException>(() =>
                admin.DefineSemesterAsync(year.Id, 1, "الفصل الأول", "Semester 1", new DateTime(2026, 9, 1), new DateTime(2026, 12, 31)));
            var updated = await admin.DefineSemesterAsync(year.Id, 1, "الفصل الأول (محدث)", "Semester 1 (updated)", new DateTime(2026, 9, 1), new DateTime(2027, 1, 31));
            Assert.Equal(s1.Id, updated.Id);
            Assert.Equal(2, db.Semesters.Count(s => s.AcademicYearId == year.Id));
        }

        // --- Edit / delete: only while no student is enrolled -----------------------

        private static async Task EnrollOneStudent(AppDbContext db, int yearId)
        {
            var stage = new Stage { Name = new LocalizedName("مرحلة", "Stage"), SequenceOrder = 1, DefaultGenderPolicy = GenderPolicy.Mixed };
            db.Stages.Add(stage);
            await db.SaveChangesAsync();
            var grade = new GradeLevel { StageId = stage.Id, Code = "G1", Name = new LocalizedName("صف", "Grade 1"), SequenceOrder = 1 };
            db.GradeLevels.Add(grade);
            await db.SaveChangesAsync();
            var profile = new GradeYearProfile { GradeLevelId = grade.Id, AcademicYearId = yearId, GenderPolicy = GenderPolicy.Mixed, TargetSections = 1, TargetSectionSize = 25 };
            db.GradeYearProfiles.Add(profile);
            var student = new Student
            {
                StudentNo = "STU-1", FirstNameAr = "س", FatherNameAr = "أ", GrandfatherNameAr = "ج", FamilyNameAr = "ع",
                FirstNameEn = "S", FatherNameEn = "F", GrandfatherNameEn = "G", FamilyNameEn = "Fam", Gender = Gender.Male, DateOfBirth = new DateTime(2018, 1, 1), NationalityLookupId = 1,
            };
            db.Students.Add(student);
            await db.SaveChangesAsync();
            db.Enrollments.Add(new Enrollment { AcademicYearId = yearId, StudentId = student.Id, GradeYearProfileId = profile.Id, EnrollmentDate = new DateTime(2026, 9, 1), SourceType = EnrollmentSourceType.Admission });
            await db.SaveChangesAsync();
        }

        [Fact]
        [BusinessRule("BR-AYR-001")]
        public async Task A_year_without_enrollments_can_be_edited_under_the_same_date_rules()
        {
            using var db = CreateContext();
            var admin = new AcademicYearAdmin(db);
            var year = await DefineYear(admin, new DateTime(2026, 9, 1), new DateTime(2027, 6, 30));
            await admin.DefineSemesterAsync(year.Id, 1, "الفصل الأول", "Semester 1", new DateTime(2026, 9, 1), new DateTime(2027, 1, 31));

            var updated = await admin.UpdateYearAsync(year.Id, "عام محدث", "2026-2027 (v2)", "١٤٤٨هـ", new DateTime(2026, 8, 25), new DateTime(2027, 7, 15));
            Assert.Equal("2026-2027 (v2)", db.AcademicYears.Single(y => y.Id == year.Id).LabelEn);
            Assert.Equal(new DateTime(2026, 8, 25), updated.StartDate);

            // invalid span
            await Assert.ThrowsAsync<InvalidAcademicYearDatesException>(() =>
                admin.UpdateYearAsync(year.Id, "أ", "A", "h", new DateTime(2026, 9, 1), new DateTime(2026, 11, 1)));
            // shrinking below an existing semester
            await Assert.ThrowsAsync<InvalidPeriodDatesException>(() =>
                admin.UpdateYearAsync(year.Id, "أ", "A", "h", new DateTime(2026, 10, 1), new DateTime(2027, 6, 30)));
        }

        [Fact]
        public async Task A_year_with_enrolled_students_can_be_neither_edited_nor_deleted()
        {
            using var db = CreateContext();
            var admin = new AcademicYearAdmin(db);
            var year = await DefineYear(admin, new DateTime(2026, 9, 1), new DateTime(2027, 6, 30));
            await EnrollOneStudent(db, year.Id);

            await Assert.ThrowsAsync<AcademicYearInUseException>(() =>
                admin.UpdateYearAsync(year.Id, "أ", "A", "h", new DateTime(2026, 9, 1), new DateTime(2027, 6, 30)));
            await Assert.ThrowsAsync<AcademicYearInUseException>(() => admin.DeleteYearAsync(year.Id));
            Assert.Single(db.AcademicYears.Where(y => y.Id == year.Id));
        }

        [Fact]
        public async Task Deleting_a_year_without_enrollments_removes_it_with_its_semesters_and_terms()
        {
            using var db = CreateContext();
            var admin = new AcademicYearAdmin(db);
            var year = await DefineYear(admin, new DateTime(2026, 9, 1), new DateTime(2027, 6, 30));
            var s1 = await admin.DefineSemesterAsync(year.Id, 1, "الفصل الأول", "Semester 1", new DateTime(2026, 9, 1), new DateTime(2027, 1, 31));
            await admin.DefineTermAsync(s1.Id, 1, "الفترة الأولى", "Term 1", new DateTime(2026, 9, 1), new DateTime(2026, 11, 15));

            await admin.DeleteYearAsync(year.Id);

            Assert.Empty(db.AcademicYears.Where(y => y.Id == year.Id));
            Assert.Empty(db.Semesters.Where(s => s.AcademicYearId == year.Id));
            Assert.Empty(db.Terms.Where(t => t.AcademicYearId == year.Id));
        }
    }
}
