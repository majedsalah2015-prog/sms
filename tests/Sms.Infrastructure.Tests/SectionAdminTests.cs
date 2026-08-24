using System;
using System.Collections.Generic;
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
        private async Task<int> CreateEnrollment(
            AppDbContext db, int gradeYearProfileId = -1,
            Sms.Domain.Common.Gender gender = Sms.Domain.Common.Gender.Male)
        {
            var profileId = gradeYearProfileId == -1 ? _profileId : gradeYearProfileId;
            var seq = _nextStudentSeq++;
            var student = new Student
            {
                StudentNo = $"STU-TEST-{seq}",
                FirstNameAr = "طالب", FatherNameAr = "أب", GrandfatherNameAr = "جد", FamilyNameAr = "عائلة",
                FirstNameEn = "Student", FatherNameEn = "Father", GrandfatherNameEn = "Grandfather", FamilyNameEn = "Family",
                Gender = gender,
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

        // --- edit / delete ------------------------------------------------------

        [Fact]
        [BusinessRule("BR-SCN-001")]
        public async Task Editing_a_section_applies_the_same_plan_gender_and_name_rules()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);
            var a = await admin.DefineSectionAsync(_profileId, "ثالث-أ", "3-A", 3, GenderPolicy.Mixed);
            await admin.DefineSectionAsync(_profileId, "ثالث-ب", "3-B", 3, GenderPolicy.Mixed);

            var updated = await admin.UpdateSectionAsync(a.Id, "ثالث-أ (بنين)", "3-A boys", 2, GenderPolicy.Boys);
            Assert.Equal("3-A boys", db.Sections.Single(s => s.Id == a.Id).NameEn);
            Assert.Equal(GenderPolicy.Boys, updated.GenderPolicy);

            await Assert.ThrowsAsync<SectionCapacityPlanExceededException>(() => admin.UpdateSectionAsync(a.Id, "أ", "3-A", 4, GenderPolicy.Mixed)); // plan size is 3
            await Assert.ThrowsAsync<DuplicateSectionNameException>(() => admin.UpdateSectionAsync(a.Id, "أ", "3-B", 3, GenderPolicy.Mixed));
        }

        [Fact]
        public async Task Capacity_cannot_drop_below_the_currently_assigned_students()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);
            var section = await admin.DefineSectionAsync(_profileId, "ثالث-أ", "3-A", 3, GenderPolicy.Mixed);
            await admin.AssignMembershipAsync(section.Id, await CreateEnrollment(db), new DateTime(2026, 9, 1));
            await admin.AssignMembershipAsync(section.Id, await CreateEnrollment(db), new DateTime(2026, 9, 1));

            await Assert.ThrowsAsync<SectionInUseException>(() => admin.UpdateSectionAsync(section.Id, "ثالث-أ", "3-A", 1, GenderPolicy.Mixed));
            await admin.UpdateSectionAsync(section.Id, "ثالث-أ", "3-A", 2, GenderPolicy.Mixed);
            Assert.Equal(2, db.Sections.Single(s => s.Id == section.Id).Capacity);
        }

        [Fact]
        [BusinessRule("BR-SCN-007")]
        public async Task A_section_is_deletable_only_while_it_never_had_students_or_a_homeroom()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);
            var fresh = await admin.DefineSectionAsync(_profileId, "ثالث-أ", "3-A", 3, GenderPolicy.Mixed);
            var used = await admin.DefineSectionAsync(_profileId, "ثالث-ب", "3-B", 3, GenderPolicy.Mixed);
            var enrollmentId = await CreateEnrollment(db);
            await admin.AssignMembershipAsync(used.Id, enrollmentId, new DateTime(2026, 9, 1));
            await admin.TransferMembershipAsync(enrollmentId, fresh.Id, "BALANCE", new DateTime(2026, 10, 1)); // `used` now has 0 current members but history

            await Assert.ThrowsAsync<SectionInUseException>(() => admin.DeleteSectionAsync(used.Id));
            Assert.Single(db.Sections.Where(s => s.Id == used.Id));

            var empty = await admin.DefineSectionAsync(_profileId, "ثالث-ج", "3-C", 3, GenderPolicy.Mixed);
            await admin.DeleteSectionAsync(empty.Id);
            Assert.Empty(db.Sections.Where(s => s.Id == empty.Id));
        }

        /// <summary>
        /// BR-SCN-001: a grade is planned as a number of sections, so opening them is
        /// one operation named from the grade's own pattern rather than four trips
        /// through a form — which is how "1-A", "1-b" and "1 - C" end up in one grade.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SCN-001")]
        public async Task Opening_several_sections_names_them_from_the_grades_own_pattern()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);

            var created = await admin.DefineSectionsAsync(_profileId, 3, 3, GenderPolicy.Mixed);

            Assert.Equal(new[] { "ثالث-أ", "ثالث-ب", "ثالث-ج" }, created.Select(s => s.NameAr).ToArray());
            Assert.Equal(new[] { "Grade 3-A", "Grade 3-B", "Grade 3-C" }, created.Select(s => s.NameEn).ToArray());
            Assert.All(created, s => Assert.Equal(3, s.Capacity));
            Assert.Equal(3, db.Sections.Count(s => s.GradeYearProfileId == _profileId));
        }

        [Fact]
        [BusinessRule("BR-SCN-001")]
        public async Task A_second_batch_continues_past_what_the_grade_already_holds()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);
            await admin.DefineSectionsAsync(_profileId, 2, 3, GenderPolicy.Mixed);

            var more = await admin.DefineSectionsAsync(_profileId, 2, 3, GenderPolicy.Mixed);

            Assert.Equal(new[] { "Grade 3-C", "Grade 3-D" }, more.Select(s => s.NameEn).ToArray());
        }

        /// <summary>
        /// The whole batch is checked before any of it is written: a capacity that
        /// breaks the grade's plan must refuse all four rather than leave two behind
        /// for somebody to find and clean up.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SCN-002")]
        public async Task A_batch_that_breaks_the_grade_plan_writes_none_of_it()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);

            await Assert.ThrowsAsync<SectionCapacityPlanExceededException>(
                () => admin.DefineSectionsAsync(_profileId, 4, capacity: 99, GenderPolicy.Mixed));

            Assert.Empty(db.Sections.Where(s => s.GradeYearProfileId == _profileId));
        }

        [Fact]
        public async Task Opening_none_writes_nothing_and_does_not_throw()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);

            Assert.Empty(await admin.DefineSectionsAsync(_profileId, 0, 3, GenderPolicy.Mixed));
            Assert.Empty(db.Sections.Where(s => s.GradeYearProfileId == _profileId));
        }

        // --- BR-SCN-003 on assignment, and §8.3's whole-board apply ------------

        /// <summary>
        /// A section's gender policy was checked when the section was defined and
        /// never when a student was put in one, so the roster screen would seat a girl
        /// in a boys' section without a word.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SCN-003")]
        public async Task A_student_cannot_be_assigned_to_a_section_their_gender_bars()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);
            var boys = await admin.DefineSectionAsync(_profileId, "ثالث-أ", "3-A", 3, GenderPolicy.Boys);
            var girl = await CreateEnrollment(db, gender: Sms.Domain.Common.Gender.Female);

            await Assert.ThrowsAsync<SectionGenderMismatchException>(
                () => admin.AssignMembershipAsync(boys.Id, girl, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)));

            Assert.Empty(db.SectionMemberships.Where(m => m.SectionId == boys.Id));
        }

        [Fact]
        [BusinessRule("BR-SCN-003")]
        public async Task A_transfer_cannot_land_a_student_in_a_section_their_gender_bars()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);
            var mixed = await admin.DefineSectionAsync(_profileId, "ثالث-أ", "3-A", 3, GenderPolicy.Mixed);
            var boys = await admin.DefineSectionAsync(_profileId, "ثالث-ب", "3-B", 3, GenderPolicy.Boys);
            var girl = await CreateEnrollment(db, gender: Sms.Domain.Common.Gender.Female);
            await admin.AssignMembershipAsync(mixed.Id, girl, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));

            await Assert.ThrowsAsync<SectionGenderMismatchException>(
                () => admin.TransferMembershipAsync(girl, boys.Id, "balancing", new DateTime(2026, 10, 1)));

            Assert.Single(db.SectionMemberships.Where(m => m.EnrollmentId == girl && m.EffectiveToUtc == null));
        }

        [Fact]
        [BusinessRule("BR-SCN-008")]
        public async Task Applying_a_board_layout_seats_the_unassigned_and_transfers_the_rest()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);
            var a = await admin.DefineSectionAsync(_profileId, "ثالث-أ", "3-A", 3, GenderPolicy.Mixed);
            var b = await admin.DefineSectionAsync(_profileId, "ثالث-ب", "3-B", 3, GenderPolicy.Mixed);
            var seated = await CreateEnrollment(db);
            var fresh = await CreateEnrollment(db);
            await admin.AssignMembershipAsync(a.Id, seated, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));

            var outcome = await admin.ApplyDistributionAsync(
                new Dictionary<int, int> { [seated] = b.Id, [fresh] = a.Id },
                "balancing", new DateTime(2026, 10, 1));

            // Counted apart: a first seat and a transfer are different events on a
            // child's record, and only the second carries a reason.
            Assert.Equal(1, outcome.Seated);
            Assert.Equal(1, outcome.Transferred);
            Assert.Equal(2, outcome.Total);
            var seatedNow = db.SectionMemberships.Single(m => m.EnrollmentId == seated && m.EffectiveToUtc == null);
            Assert.Equal(b.Id, seatedNow.SectionId);
            Assert.Equal("balancing", seatedNow.TransferReasonCode);

            // The old membership is closed, not erased — BR-SCN-005 keeps history.
            Assert.Single(db.SectionMemberships.Where(m => m.EnrollmentId == seated && m.EffectiveToUtc != null));

            // A first seat is not a transfer, so it carries no reason code.
            Assert.Null(db.SectionMemberships.Single(m => m.EnrollmentId == fresh).TransferReasonCode);
        }

        /// <summary>
        /// Capacity belongs to the section after every move lands. Checked per move,
        /// this layout passes twice and then fails — leaving two children written and
        /// the third refused, which is the worst of both outcomes.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SCN-002")]
        public async Task A_board_layout_that_overfills_a_section_is_refused_whole()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);
            var a = await admin.DefineSectionAsync(_profileId, "ثالث-أ", "3-A", capacity: 2, genderPolicy: GenderPolicy.Mixed);
            var one = await CreateEnrollment(db);
            var two = await CreateEnrollment(db);
            var three = await CreateEnrollment(db);

            await Assert.ThrowsAsync<SectionFullException>(() => admin.ApplyDistributionAsync(
                new Dictionary<int, int> { [one] = a.Id, [two] = a.Id, [three] = a.Id },
                "balancing", new DateTime(2026, 10, 1)));

            Assert.Empty(db.SectionMemberships.Where(m => m.SectionId == a.Id));
        }

        /// <summary>
        /// The students nobody is moving still occupy their seats — a layout checked
        /// only against the students it names would fill a section twice over.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SCN-002")]
        public async Task A_board_layout_counts_the_students_it_is_not_moving()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);
            var a = await admin.DefineSectionAsync(_profileId, "ثالث-أ", "3-A", capacity: 2, genderPolicy: GenderPolicy.Mixed);
            var b = await admin.DefineSectionAsync(_profileId, "ثالث-ب", "3-B", capacity: 2, genderPolicy: GenderPolicy.Mixed);
            var staying = await CreateEnrollment(db);
            var alsoStaying = await CreateEnrollment(db);
            var incoming = await CreateEnrollment(db);
            await admin.AssignMembershipAsync(a.Id, staying, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
            await admin.AssignMembershipAsync(a.Id, alsoStaying, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
            await admin.AssignMembershipAsync(b.Id, incoming, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));

            await Assert.ThrowsAsync<SectionFullException>(() => admin.ApplyDistributionAsync(
                new Dictionary<int, int> { [incoming] = a.Id }, "balancing", new DateTime(2026, 10, 1)));
        }

        [Fact]
        [BusinessRule("BR-SCN-003")]
        public async Task A_board_layout_is_refused_whole_when_one_placement_breaks_gender_policy()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);
            var mixed = await admin.DefineSectionAsync(_profileId, "ثالث-أ", "3-A", 3, GenderPolicy.Mixed);
            var boys = await admin.DefineSectionAsync(_profileId, "ثالث-ب", "3-B", 3, GenderPolicy.Boys);
            var boy = await CreateEnrollment(db);
            var girl = await CreateEnrollment(db, gender: Sms.Domain.Common.Gender.Female);

            await Assert.ThrowsAsync<SectionGenderMismatchException>(() => admin.ApplyDistributionAsync(
                new Dictionary<int, int> { [boy] = mixed.Id, [girl] = boys.Id },
                "balancing", new DateTime(2026, 10, 1)));

            Assert.Empty(db.SectionMemberships);
        }

        /// <summary>
        /// A student dragged out of a column and back into it is not a transfer, and
        /// writing one would put a fictitious move on a child's record.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SCN-005")]
        public async Task A_placement_that_names_the_section_a_student_is_already_in_writes_nothing()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);
            var a = await admin.DefineSectionAsync(_profileId, "ثالث-أ", "3-A", 3, GenderPolicy.Mixed);
            var seated = await CreateEnrollment(db);
            await admin.AssignMembershipAsync(a.Id, seated, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));

            var outcome = await admin.ApplyDistributionAsync(
                new Dictionary<int, int> { [seated] = a.Id }, "balancing", new DateTime(2026, 10, 1));

            Assert.Equal(0, outcome.Total);
            Assert.Single(db.SectionMemberships.Where(m => m.EnrollmentId == seated));
        }

        [Fact]
        public async Task A_board_layout_naming_a_section_from_another_grade_is_refused()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);
            var otherGrade = new GradeLevel { StageId = db.Stages.First().Id, Code = "G4", Name = new Sms.Domain.Common.LocalizedName("رابع", "Grade 4"), SequenceOrder = 4 };
            db.GradeLevels.Add(otherGrade);
            await db.SaveChangesAsync();
            var otherProfile = new GradeYearProfile
            {
                GradeLevelId = otherGrade.Id, AcademicYearId = _yearId,
                GenderPolicy = GenderPolicy.Mixed, TargetSections = 1, TargetSectionSize = 3,
            };
            db.GradeYearProfiles.Add(otherProfile);
            await db.SaveChangesAsync();

            var elsewhere = await admin.DefineSectionAsync(otherProfile.Id, "رابع-أ", "4-A", 3, GenderPolicy.Mixed);
            var ourStudent = await CreateEnrollment(db);

            await Assert.ThrowsAsync<SectionGradeMismatchException>(() => admin.ApplyDistributionAsync(
                new Dictionary<int, int> { [ourStudent] = elsewhere.Id }, "balancing", new DateTime(2026, 10, 1)));
        }

        [Fact]
        public async Task Applying_an_empty_layout_writes_nothing_and_does_not_throw()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);

            var outcome = await admin.ApplyDistributionAsync(
                new Dictionary<int, int>(), "balancing", new DateTime(2026, 10, 1));

            Assert.Equal(0, outcome.Total);
        }

        // --- BR-SCN-007 merge / close ----------------------------------------

        [Fact]
        [BusinessRule("BR-SCN-007")]
        public async Task Merging_moves_every_student_out_and_closes_the_section_together()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);
            var closing = await admin.DefineSectionAsync(_profileId, "ثالث-أ", "3-A", 3, GenderPolicy.Mixed);
            var target = await admin.DefineSectionAsync(_profileId, "ثالث-ب", "3-B", 3, GenderPolicy.Mixed);
            var one = await CreateEnrollment(db);
            var two = await CreateEnrollment(db);
            await admin.AssignMembershipAsync(closing.Id, one, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
            await admin.AssignMembershipAsync(closing.Id, two, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));

            var moved = await admin.MergeAndCloseSectionAsync(
                closing.Id,
                new Dictionary<int, int> { [one] = target.Id, [two] = target.Id },
                "balancing", new DateTime(2026, 10, 1));

            Assert.Equal(2, moved);
            Assert.Equal(SectionStatus.Closed, db.Sections.Single(s => s.Id == closing.Id).Status);
            Assert.Empty(db.SectionMemberships.Where(m => m.SectionId == closing.Id && m.EffectiveToUtc == null));
            Assert.Equal(2, db.SectionMemberships.Count(m => m.SectionId == target.Id && m.EffectiveToUtc == null));

            // Closed is not deleted — the old memberships still name the section, which
            // is what keeps last year's records readable (BR-GLB-005).
            Assert.Equal(2, db.SectionMemberships.Count(m => m.SectionId == closing.Id && m.EffectiveToUtc != null));
        }

        /// <summary>
        /// A student left out of the mapping would end up recorded in a section that no
        /// longer runs. The whole operation is refused rather than half-applied.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SCN-007")]
        public async Task Merging_refuses_when_a_student_has_nowhere_to_go()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);
            var closing = await admin.DefineSectionAsync(_profileId, "ثالث-أ", "3-A", 3, GenderPolicy.Mixed);
            var target = await admin.DefineSectionAsync(_profileId, "ثالث-ب", "3-B", 3, GenderPolicy.Mixed);
            var one = await CreateEnrollment(db);
            var two = await CreateEnrollment(db);
            await admin.AssignMembershipAsync(closing.Id, one, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
            await admin.AssignMembershipAsync(closing.Id, two, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));

            await Assert.ThrowsAsync<SectionCloseWithMembersException>(() => admin.MergeAndCloseSectionAsync(
                closing.Id, new Dictionary<int, int> { [one] = target.Id }, "balancing", new DateTime(2026, 10, 1)));

            Assert.Equal(SectionStatus.Active, db.Sections.Single(s => s.Id == closing.Id).Status);
            Assert.Equal(2, db.SectionMemberships.Count(m => m.SectionId == closing.Id && m.EffectiveToUtc == null));
        }

        [Fact]
        [BusinessRule("BR-SCN-007")]
        public async Task Merging_refuses_a_mapping_that_sends_a_student_back_into_the_closing_section()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);
            var closing = await admin.DefineSectionAsync(_profileId, "ثالث-أ", "3-A", 3, GenderPolicy.Mixed);
            await admin.DefineSectionAsync(_profileId, "ثالث-ب", "3-B", 3, GenderPolicy.Mixed);
            var one = await CreateEnrollment(db);
            await admin.AssignMembershipAsync(closing.Id, one, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));

            await Assert.ThrowsAsync<SectionCloseWithMembersException>(() => admin.MergeAndCloseSectionAsync(
                closing.Id, new Dictionary<int, int> { [one] = closing.Id }, "balancing", new DateTime(2026, 10, 1)));

            Assert.Equal(SectionStatus.Active, db.Sections.Single(s => s.Id == closing.Id).Status);
        }

        /// <summary>
        /// A teacher cannot go on being homeroom of a section that no longer runs, and
        /// BR-SCN-004 keeps the assignment as history rather than removing it.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SCN-004")]
        public async Task Merging_ends_the_sections_homeroom_assignment_at_the_effective_date()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);
            var closing = await admin.DefineSectionAsync(_profileId, "ثالث-أ", "3-A", 3, GenderPolicy.Mixed);
            await admin.AssignHomeroomTeacherAsync(closing.Id, _teacherId, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));

            await admin.MergeAndCloseSectionAsync(
                closing.Id, new Dictionary<int, int>(), "balancing", new DateTime(2026, 10, 1));

            var assignment = db.HomeroomAssignments.Single(h => h.SectionId == closing.Id);
            Assert.Equal(new DateTime(2026, 10, 1), assignment.EffectiveToUtc);
            Assert.Equal(SectionStatus.Closed, db.Sections.Single(s => s.Id == closing.Id).Status);
        }

        [Fact]
        [BusinessRule("BR-SCN-002")]
        public async Task Merging_refuses_when_the_target_section_has_no_room_for_everybody()
        {
            using var db = CreateContext();
            var admin = new SectionAdmin(db);
            var closing = await admin.DefineSectionAsync(_profileId, "ثالث-أ", "3-A", 3, GenderPolicy.Mixed);
            var target = await admin.DefineSectionAsync(_profileId, "ثالث-ب", "3-B", capacity: 1, genderPolicy: GenderPolicy.Mixed);
            var one = await CreateEnrollment(db);
            var two = await CreateEnrollment(db);
            await admin.AssignMembershipAsync(closing.Id, one, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
            await admin.AssignMembershipAsync(closing.Id, two, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));

            await Assert.ThrowsAsync<SectionFullException>(() => admin.MergeAndCloseSectionAsync(
                closing.Id,
                new Dictionary<int, int> { [one] = target.Id, [two] = target.Id },
                "balancing", new DateTime(2026, 10, 1)));

            Assert.Equal(SectionStatus.Active, db.Sections.Single(s => s.Id == closing.Id).Status);
        }
    }
}
