using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Grades;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Security;
using Sms.Domain.Students;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Sections;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// E-103 (slice: Sections, doc/Modules/06, BR-SCN-001..007) over a real
    /// Sqlite-backed AppDbContext. SectionMembership.EnrollmentId carries a
    /// real FK to ppl.Enrollment as of E-202 — tests seed real Student +
    /// Enrollment rows via <see cref="CreateEnrollment"/> rather than the
    /// arbitrary placeholder ints used before Enrollment existed.
    /// </summary>
    public sealed class SectionAdminTests : IDisposable
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
        private int _profileId;
        private int _teacherId;

        public SectionAdminTests()
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
            var teacher = new UserAccount { UserName = "t.sara", AccountType = AccountType.Staff };
            db.UserAccounts.Add(teacher);
            db.SaveChanges();

            var profile = new GradeYearProfile
            {
                GradeLevelId = grade.Id, AcademicYearId = year.Id,
                GenderPolicy = GenderPolicy.Mixed, TargetSections = 2, TargetSectionSize = 3,
            };
            db.GradeYearProfiles.Add(profile);
            db.SaveChanges();

            _yearId = year.Id;
            _profileId = profile.Id;
            _teacherId = teacher.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private int _nextStudentSeq = 1;

        /// <summary>Seeds a Student + an Active Enrollment for the fixture's year, returning the Enrollment id (real FK target since E-202).</summary>
        private async Task<int> CreateEnrollment(AppDbContext db, int gradeYearProfileId = -1)
        {
            var profileId = gradeYearProfileId == -1 ? _profileId : gradeYearProfileId;
            var seq = _nextStudentSeq++;
            var student = new Student
            {
                StudentNo = $"STU-TEST-{seq}",
                FirstNameAr = "طالب", FatherNameAr = "أب", GrandfatherNameAr = "جد", FamilyNameAr = "عائلة",
                FirstNameEn = "Student", FatherNameEn = "Father", GrandfatherNameEn = "Grandfather", FamilyNameEn = "Family",
                Gender = Sms.Domain.Common.Gender.Male,
                DateOfBirth = new DateTime(2018, 1, 1),
                NationalityLookupId = 1,
            };
            db.Students.Add(student);
            await db.SaveChangesAsync();

            var enrollment = new Enrollment
            {
                AcademicYearId = _yearId,
                StudentId = student.Id,
                GradeYearProfileId = profileId,
                EnrollmentDate = new DateTime(2026, 9, 1),
                SourceType = EnrollmentSourceType.Admission,
            };
            db.Enrollments.Add(enrollment);
            await db.SaveChangesAsync();
            return enrollment.Id;
        }

        // --- BR-SCN-001/002/003 section definition ----------------------------

        [Fact]
        [BusinessRule("BR-SCN-001")]
        public async Task Defining_a_section_links_it_to_its_grade_year_profile()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);

            var section = await admin.DefineSectionAsync(_profileId, "ثالث-أ", "3-A", capacity: 3, GenderPolicy.Mixed);

            Assert.Equal(_profileId, db.Sections.Single(s => s.Id == section.Id).GradeYearProfileId);
        }

        [Fact]
        [BusinessRule("BR-SCN-001")]
        public async Task A_duplicate_section_name_within_the_same_grade_year_is_rejected()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);
            await admin.DefineSectionAsync(_profileId, "ثالث-أ", "3-A", 3, GenderPolicy.Mixed);

            await Assert.ThrowsAsync<DuplicateSectionNameException>(() =>
                admin.DefineSectionAsync(_profileId, "ثالث-أ2", "3-A", 3, GenderPolicy.Mixed));
        }

        [Fact]
        [BusinessRule("BR-SCN-002")]
        public async Task A_section_capacity_over_the_grade_plan_is_rejected()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);

            await Assert.ThrowsAsync<SectionCapacityPlanExceededException>(() =>
                admin.DefineSectionAsync(_profileId, "ثالث-أ", "3-A", capacity: 4, GenderPolicy.Mixed)); // plan is 3
        }

        [Fact]
        [BusinessRule("BR-SCN-003")]
        public async Task A_section_cannot_widen_its_grades_gender_policy()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);
            // Grade profile is Mixed already covers everything; redefine with a single-gender grade instead.
            var singleGenderProfile = await SeedSingleGenderProfile(db, _yearId);

            await Assert.ThrowsAsync<InvalidSectionGenderPolicyException>(() =>
                admin.DefineSectionAsync(singleGenderProfile, "أ", "A", 3, GenderPolicy.Mixed));
        }

        private async Task<int> SeedSingleGenderProfile(AppDbContext db, int yearId)
        {
            var stage = new Stage { Name = new Sms.Domain.Common.LocalizedName("بنين", "Boys"), SequenceOrder = 2, DefaultGenderPolicy = GenderPolicy.Boys };
            db.Stages.Add(stage);
            await db.SaveChangesAsync();
            var grade = new GradeLevel { StageId = stage.Id, Code = "G1B", Name = new Sms.Domain.Common.LocalizedName("أول", "Grade 1"), SequenceOrder = 1 };
            db.GradeLevels.Add(grade);
            await db.SaveChangesAsync();
            var profile = new GradeYearProfile { GradeLevelId = grade.Id, AcademicYearId = yearId, GenderPolicy = GenderPolicy.Boys, TargetSections = 1, TargetSectionSize = 3 };
            db.GradeYearProfiles.Add(profile);
            await db.SaveChangesAsync();
            return profile.Id;
        }

        // --- BR-SCN-004 homeroom effective-dating -----------------------------

        [Fact]
        [BusinessRule("BR-SCN-004")]
        public async Task Reassigning_the_homeroom_teacher_closes_out_the_previous_one()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);
            var section = await admin.DefineSectionAsync(_profileId, "ثالث-أ", "3-A", 3, GenderPolicy.Mixed);
            var otherTeacher = db.UserAccounts.Add(new UserAccount { UserName = "t.ali", AccountType = AccountType.Staff }).Entity;
            await db.SaveChangesAsync();

            var first = await admin.AssignHomeroomTeacherAsync(section.Id, _teacherId, new DateTime(2026, 9, 1));
            var second = await admin.AssignHomeroomTeacherAsync(section.Id, otherTeacher.Id, new DateTime(2027, 1, 1));

            Assert.Equal(new DateTime(2027, 1, 1), db.HomeroomAssignments.Single(h => h.Id == first.Id).EffectiveToUtc);
            Assert.Null(db.HomeroomAssignments.Single(h => h.Id == second.Id).EffectiveToUtc);
        }

        // --- BR-SCN-005/002 membership + capacity -------------------------------

        [Fact]
        [BusinessRule("BR-SCN-002")]
        public async Task Assigning_beyond_capacity_is_rejected()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);
            var section = await admin.DefineSectionAsync(_profileId, "ثالث-أ", "3-A", capacity: 2, GenderPolicy.Mixed);
            await admin.AssignMembershipAsync(section.Id, await CreateEnrollment(db), new DateTime(2026, 9, 1));
            await admin.AssignMembershipAsync(section.Id, await CreateEnrollment(db), new DateTime(2026, 9, 1));

            await Assert.ThrowsAsync<SectionFullException>(() =>
                admin.AssignMembershipAsync(section.Id, 999, new DateTime(2026, 9, 1)));
        }

        [Fact]
        [BusinessRule("BR-SCN-005")]
        public async Task Transferring_a_student_closes_the_old_membership_and_opens_a_new_one()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);
            var sectionA = await admin.DefineSectionAsync(_profileId, "ثالث-أ", "3-A", 3, GenderPolicy.Mixed);
            var sectionB = await admin.DefineSectionAsync(_profileId, "ثالث-ب", "3-B", 3, GenderPolicy.Mixed);
            var enrollmentId = await CreateEnrollment(db);
            var original = await admin.AssignMembershipAsync(sectionA.Id, enrollmentId, new DateTime(2026, 9, 1));

            var transferred = await admin.TransferMembershipAsync(enrollmentId, sectionB.Id, "Balancing", new DateTime(2026, 10, 1));

            Assert.Equal(new DateTime(2026, 10, 1), db.SectionMemberships.Single(m => m.Id == original.Id).EffectiveToUtc);
            Assert.Equal(sectionB.Id, db.SectionMemberships.Single(m => m.Id == transferred.Id).SectionId);
            Assert.Null(db.SectionMemberships.Single(m => m.Id == transferred.Id).EffectiveToUtc);
        }

        // --- BR-SCN-007 close-with-zero-members ---------------------------------

        [Fact]
        [BusinessRule("BR-SCN-007")]
        public async Task Closing_a_section_with_assigned_students_is_rejected()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);
            var section = await admin.DefineSectionAsync(_profileId, "ثالث-أ", "3-A", 3, GenderPolicy.Mixed);
            await admin.AssignMembershipAsync(section.Id, await CreateEnrollment(db), new DateTime(2026, 9, 1));

            await Assert.ThrowsAsync<SectionCloseWithMembersException>(() => admin.CloseSectionAsync(section.Id));
        }

        [Fact]
        [BusinessRule("BR-SCN-007")]
        public async Task Closing_an_empty_section_succeeds_and_it_stays_in_history()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);
            var section = await admin.DefineSectionAsync(_profileId, "ثالث-أ", "3-A", 3, GenderPolicy.Mixed);

            await admin.CloseSectionAsync(section.Id);

            Assert.Equal(SectionStatus.Closed, db.Sections.Single(s => s.Id == section.Id).Status);
        }
    }
}
