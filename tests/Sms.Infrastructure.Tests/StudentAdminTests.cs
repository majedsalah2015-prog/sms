using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Guards;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Attendance;
using Sms.Domain.Audit;
using Sms.Domain.Common;
using Sms.Domain.Geography;
using Sms.Domain.Grades;
using Sms.Domain.Numbering;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Students;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Students;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// E-202 (slice: Students, doc/Modules/10, BR-STU-001..003) over a real
    /// Sqlite-backed AppDbContext, including E-006's real INumberIssuer
    /// (the "STU" series) — the first real consumer of that framework.
    /// </summary>
    public sealed class StudentAdminTests : IDisposable
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

        public StudentAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            db.NumberingSeries.Add(new NumberingSeries
            {
                Code = "STU", EntityName = "Student", FormatTemplate = "STU-{SEQ:6}",
                ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Normal, EffectiveFromUtc = _clock.UtcNow, IsActive = true,
            });

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
            _profileId = profile.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        /// <summary>
        /// The real audit writer, not a stub: <c>RemoveEnrollmentAsync</c>'s whole contract is that
        /// the entry and the removal commit together, and a no-op double would let a regression that
        /// loses the entry pass every test here.
        /// </summary>
        private StudentAdmin CreateAdmin(AppDbContext db)
            => new(db, new NumberIssuer(db, _tenant, _tenant, _clock),
                new AuditEventWriter(db, _tenant, _tenant, _user, _clock, _audit));

        private static Task<Student> Register(StudentAdmin admin, string suffix = "1")
            => admin.RegisterStudentAsync(
                "طالب" + suffix, "أب", "جد", "عائلة", "Student" + suffix, "Father", "Grandfather", "Family",
                Gender.Male, new DateTime(2018, 1, 1), nationalityLookupId: 1);

        // --- BR-STU-001 registration + real numbering integration --------------

        [Fact]
        [BusinessRule("BR-STU-001")]
        public async Task Registering_a_student_issues_a_real_permanent_number_via_the_STU_series()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            var student = await Register(admin);

            Assert.Equal("STU-000001", student.StudentNo);
        }

        [Fact]
        [BusinessRule("BR-STU-001")]
        public async Task Successive_registrations_get_distinct_sequential_numbers()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            var first = await Register(admin, "1");
            var second = await Register(admin, "2");

            Assert.Equal("STU-000001", first.StudentNo);
            Assert.Equal("STU-000002", second.StudentNo);
        }

        [Fact]
        [BusinessRule("BR-STU-001")]
        public async Task Editing_identity_fields_without_a_reason_is_rejected()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var student = await Register(admin);

            _audit.Reason = null;
            var tracked = await db.Students.SingleAsync(s => s.Id == student.Id);
            tracked.FirstNameEn = "Changed";
            await Assert.ThrowsAsync<MissingAuditReasonException>(() => db.SaveChangesAsync());
        }

        // --- BR-STU-002 status lifecycle ----------------------------------------

        [Fact]
        [BusinessRule("BR-STU-002")]
        public async Task An_illegal_status_transition_is_rejected()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var student = await Register(admin);

            await Assert.ThrowsAsync<InvalidStudentStatusTransitionException>(() =>
                admin.ChangeStatusAsync(student.Id, StudentStatus.Alumni)); // Enrolled -> Alumni skips Graduated
        }

        // --- BR-STU-003 guardian links + financial-responsibility guard --------

        [Fact]
        [BusinessRule("BR-STU-003")]
        public async Task Unlinking_the_last_financially_responsible_guardian_is_rejected()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var student = await Register(admin);
            var link = await admin.LinkGuardianAsync(
                student.Id, parentId: 1, relationshipLookupId: 1, isPrimaryContact: true, isFinanciallyResponsible: true,
                isPickupAuthorized: true, isPortalVisible: true, new DateTime(2026, 9, 1));

            await Assert.ThrowsAsync<LastFinanciallyResponsibleGuardianException>(() =>
                admin.UnlinkGuardianAsync(link.Id, new DateTime(2026, 10, 1)));
        }

        [Fact]
        [BusinessRule("BR-STU-003")]
        public async Task Unlinking_one_of_two_financially_responsible_guardians_succeeds()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var student = await Register(admin);
            var first = await admin.LinkGuardianAsync(student.Id, 1, 1, true, true, true, true, new DateTime(2026, 9, 1));
            await admin.LinkGuardianAsync(student.Id, 2, 1, false, true, false, true, new DateTime(2026, 9, 1));

            await admin.UnlinkGuardianAsync(first.Id, new DateTime(2026, 10, 1));

            Assert.Equal(new DateTime(2026, 10, 1), db.StudentGuardianLinks.Single(l => l.Id == first.Id).EffectiveToUtc);
        }

        // --- Enrollment (BR-GLB-024) --------------------------------------------

        [Fact]
        public async Task Enrolling_a_student_creates_an_active_enrollment_for_the_grade_year()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var student = await Register(admin);

            var enrollment = await admin.EnrollAsync(student.Id, _profileId, new DateTime(2026, 9, 1), EnrollmentSourceType.Admission);

            Assert.Equal(EnrollmentStatus.Active, db.Enrollments.Single(e => e.Id == enrollment.Id).Status);
        }

        [Fact]
        [BusinessRule("BR-GLB-024")]
        public async Task A_second_active_enrollment_for_the_same_student_and_year_is_rejected()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var student = await Register(admin);
            await admin.EnrollAsync(student.Id, _profileId, new DateTime(2026, 9, 1), EnrollmentSourceType.Admission);

            await Assert.ThrowsAsync<DuplicateEnrollmentException>(() =>
                admin.EnrollAsync(student.Id, _profileId, new DateTime(2026, 9, 2), EnrollmentSourceType.Admission));
        }

        // --- Correcting and removing an enrollment (doc/Modules/10 §8.10) -------
        //
        // The two halves of "the clerk put him in the wrong grade". Until these existed the record
        // could only be added to, so a wrong grade stayed wrong: BR-GLB-024 refuses a second
        // enrollment in the year, and nothing else in the product writes GradeYearProfileId.

        /// <summary>Another grade in the same academic year — a legal correction target.</summary>
        private int SecondProfileInSameYear(AppDbContext db)
        {
            var year = db.AcademicYears.Single().Id;
            var stage = db.Stages.Single().Id;
            var grade = new GradeLevel { StageId = stage, Code = "G4", Name = new LocalizedName("رابع", "Grade 4"), SequenceOrder = 4 };
            db.GradeLevels.Add(grade);
            db.SaveChanges();
            var profile = new GradeYearProfile { GradeLevelId = grade.Id, AcademicYearId = year, GenderPolicy = GenderPolicy.Mixed, TargetSections = 2, TargetSectionSize = 25 };
            db.GradeYearProfiles.Add(profile);
            db.SaveChanges();
            return profile.Id;
        }

        /// <summary>The same grade in the year after — which a correction may never reach (BR-GLB-023).</summary>
        private int ProfileInAnotherYear(AppDbContext db)
        {
            var next = new AcademicYear
            {
                LabelAr = "٢٠٢٧-٢٠٢٨", LabelEn = "2027-2028", HijriLabel = "١٤٤٩هـ",
                StartDate = new DateTime(2027, 9, 1), EndDate = new DateTime(2028, 6, 30), Status = AcademicYearStatus.Preparation,
            };
            db.AcademicYears.Add(next);
            db.SaveChanges();
            var profile = new GradeYearProfile
            {
                GradeLevelId = db.GradeLevels.First().Id, AcademicYearId = next.Id,
                GenderPolicy = GenderPolicy.Mixed, TargetSections = 2, TargetSectionSize = 25,
            };
            db.GradeYearProfiles.Add(profile);
            db.SaveChanges();
            return profile.Id;
        }

        [Fact]
        public async Task Correcting_an_enrollment_re_points_it_at_the_grade_it_should_have_had()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var student = await Register(admin);
            var enrollment = await admin.EnrollAsync(student.Id, _profileId, new DateTime(2026, 9, 1), EnrollmentSourceType.Admission);
            var right = SecondProfileInSameYear(db);

            var corrected = await admin.CorrectEnrollmentAsync(
                enrollment.Id, right, new DateTime(2026, 9, 3), EnrollmentSourceType.Reinstatement);

            Assert.Equal(right, corrected.GradeYearProfileId);
            Assert.Equal(new DateTime(2026, 9, 3), corrected.EnrollmentDate);
            Assert.Equal(EnrollmentSourceType.Reinstatement, corrected.SourceType);

            // The id is the point: everything year-scoped hangs off it, so a correction that
            // replaced the row would have orphaned whatever already pointed at it.
            Assert.Equal(enrollment.Id, corrected.Id);
        }

        [Fact]
        [BusinessRule("BR-GLB-023")]
        public async Task Correcting_an_enrollment_into_another_academic_year_is_rejected()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var student = await Register(admin);
            var enrollment = await admin.EnrollAsync(student.Id, _profileId, new DateTime(2026, 9, 1), EnrollmentSourceType.Admission);
            var nextYear = ProfileInAnotherYear(db);

            await Assert.ThrowsAsync<EnrollmentYearChangeException>(() =>
                admin.CorrectEnrollmentAsync(enrollment.Id, nextYear, new DateTime(2026, 9, 1), EnrollmentSourceType.Admission));

            Assert.Equal(_profileId, db.Enrollments.Single(e => e.Id == enrollment.Id).GradeYearProfileId);
        }

        [Fact]
        [BusinessRule("BR-SCN-005")]
        public async Task The_grade_cannot_be_corrected_while_the_student_is_seated_in_a_section()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var student = await Register(admin);
            var enrollment = await admin.EnrollAsync(student.Id, _profileId, new DateTime(2026, 9, 1), EnrollmentSourceType.Admission);
            var right = SecondProfileInSameYear(db);

            var section = new Section { AcademicYearId = enrollment.AcademicYearId, GradeYearProfileId = _profileId, NameAr = "ثالث-أ", NameEn = "3-A", Capacity = 25, GenderPolicy = GenderPolicy.Mixed };
            db.Sections.Add(section);
            db.SaveChanges();
            db.SectionMemberships.Add(new SectionMembership { AcademicYearId = enrollment.AcademicYearId, SectionId = section.Id, EnrollmentId = enrollment.Id, EffectiveFromUtc = new DateTime(2026, 9, 1) });
            db.SaveChanges();

            var refusal = await Assert.ThrowsAsync<EnrollmentSeatedException>(() =>
                admin.CorrectEnrollmentAsync(enrollment.Id, right, new DateTime(2026, 9, 1), EnrollmentSourceType.Admission));

            // The refusal names the seat rather than only reporting one, so the screen can say which.
            Assert.Equal("3-A", refusal.SectionNameEn);
            Assert.Equal("ثالث-أ", refusal.SectionNameAr);
        }

        [Fact]
        public async Task Fixing_the_date_of_a_seated_students_enrollment_is_still_allowed()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var student = await Register(admin);
            var enrollment = await admin.EnrollAsync(student.Id, _profileId, new DateTime(2026, 9, 1), EnrollmentSourceType.Admission);

            var section = new Section { AcademicYearId = enrollment.AcademicYearId, GradeYearProfileId = _profileId, NameAr = "ثالث-أ", NameEn = "3-A", Capacity = 25, GenderPolicy = GenderPolicy.Mixed };
            db.Sections.Add(section);
            db.SaveChanges();
            db.SectionMemberships.Add(new SectionMembership { AcademicYearId = enrollment.AcademicYearId, SectionId = section.Id, EnrollmentId = enrollment.Id, EffectiveFromUtc = new DateTime(2026, 9, 1) });
            db.SaveChanges();

            // The seat blocks a *grade* change and nothing else — it is the grade the section
            // belongs to. Refusing a mistyped date as well would leave it uncorrectable all year.
            var corrected = await admin.CorrectEnrollmentAsync(
                enrollment.Id, _profileId, new DateTime(2026, 9, 8), EnrollmentSourceType.Admission);

            Assert.Equal(new DateTime(2026, 9, 8), corrected.EnrollmentDate);
        }

        [Fact]
        [BusinessRule("BR-GLB-005")]
        public async Task An_enrollment_nothing_was_recorded_against_can_be_removed_with_its_memberships()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var student = await Register(admin);
            var enrollment = await admin.EnrollAsync(student.Id, _profileId, new DateTime(2026, 9, 1), EnrollmentSourceType.Admission);

            var section = new Section { AcademicYearId = enrollment.AcademicYearId, GradeYearProfileId = _profileId, NameAr = "ثالث-أ", NameEn = "3-A", Capacity = 25, GenderPolicy = GenderPolicy.Mixed };
            db.Sections.Add(section);
            db.SaveChanges();
            db.SectionMemberships.Add(new SectionMembership { AcademicYearId = enrollment.AcademicYearId, SectionId = section.Id, EnrollmentId = enrollment.Id, EffectiveFromUtc = new DateTime(2026, 9, 1) });
            db.SaveChanges();

            await admin.RemoveEnrollmentAsync(enrollment.Id, "keyed against the wrong student");

            Assert.Empty(db.Enrollments.Where(e => e.Id == enrollment.Id));
            Assert.Empty(db.SectionMemberships.Where(m => m.EnrollmentId == enrollment.Id));
        }

        [Fact]
        [BusinessRule("BR-GLB-005")]
        public async Task An_enrollment_with_attendance_against_it_is_refused_and_says_what_is_in_the_way()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var student = await Register(admin);
            var enrollment = await admin.EnrollAsync(student.Id, _profileId, new DateTime(2026, 9, 1), EnrollmentSourceType.Admission);

            var section = new Section { AcademicYearId = enrollment.AcademicYearId, GradeYearProfileId = _profileId, NameAr = "ثالث-أ", NameEn = "3-A", Capacity = 25, GenderPolicy = GenderPolicy.Mixed };
            db.Sections.Add(section);
            db.SaveChanges();
            db.AttendanceDays.Add(new AttendanceDay
            {
                AcademicYearId = enrollment.AcademicYearId, EnrollmentId = enrollment.Id, SectionId = section.Id,
                Date = new DateTime(2026, 9, 2), Status = AttendanceStatus.Present,
            });
            db.SaveChanges();

            var refusal = await Assert.ThrowsAsync<RecordInUseException>(() =>
                admin.RemoveEnrollmentAsync(enrollment.Id, "keyed against the wrong student"));

            Assert.Contains("attendance day(s)", refusal.Usage.Describe(arabic: false));
            Assert.Single(db.Enrollments.Where(e => e.Id == enrollment.Id));
        }

        [Fact]
        public async Task The_batch_usage_report_attributes_each_years_history_to_its_own_enrollment()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var student = await Register(admin);
            var thisYear = await admin.EnrollAsync(student.Id, _profileId, new DateTime(2026, 9, 1), EnrollmentSourceType.Admission);
            var nextYearProfile = ProfileInAnotherYear(db);
            var nextYear = await admin.EnrollAsync(student.Id, nextYearProfile, new DateTime(2027, 9, 1), EnrollmentSourceType.Reinstatement);

            var section = new Section { AcademicYearId = thisYear.AcademicYearId, GradeYearProfileId = _profileId, NameAr = "ثالث-أ", NameEn = "3-A", Capacity = 25, GenderPolicy = GenderPolicy.Mixed };
            db.Sections.Add(section);
            db.SaveChanges();
            db.AttendanceDays.Add(new AttendanceDay
            {
                AcademicYearId = thisYear.AcademicYearId, EnrollmentId = thisYear.Id, SectionId = section.Id,
                Date = new DateTime(2026, 9, 2), Status = AttendanceStatus.Present,
            });
            db.SaveChanges();

            var reports = await new EnrollmentUsageInspector(db).InspectManyAsync(new[] { thisYear.Id, nextYear.Id });

            // The batch reads every table once for the whole set, so the risk it carries — and the
            // reason this test exists — is one row's history being reported against another's row.
            Assert.True(reports[thisYear.Id].IsInUse);
            Assert.False(reports[nextYear.Id].IsInUse);
            Assert.Contains("1 attendance day(s)", reports[thisYear.Id].Describe(arabic: false));
        }

        [Fact]
        [BusinessRule("BR-GLB-032")]
        public async Task Removing_an_enrollment_without_a_reason_is_refused_and_with_one_is_audited()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var student = await Register(admin);
            var enrollment = await admin.EnrollAsync(student.Id, _profileId, new DateTime(2026, 9, 1), EnrollmentSourceType.Admission);

            await Assert.ThrowsAsync<MissingRemovalReasonException>(() =>
                admin.RemoveEnrollmentAsync(enrollment.Id, "   "));

            await admin.RemoveEnrollmentAsync(enrollment.Id, "duplicate of the September entry");

            // AuditCaptor diffs added and modified entries only, so without this explicit entry the
            // row would leave no trace whatever that it had ever existed.
            var entry = db.AuditEntries.Single(a => a.EntityType == nameof(Enrollment) && a.Action == AuditAction.Delete);
            Assert.Equal(enrollment.Id, entry.EntityId);
            Assert.Equal("duplicate of the September entry", entry.Reason);
            Assert.Contains($"student {student.Id}", entry.BusinessKey);
        }

        [Fact]
        [BusinessRule("BR-STU-002")]
        public async Task Renaming_a_student_requires_an_audit_reason_because_identity_is_T1()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var student = await Register(admin);

            _audit.Reason = null;
            await Assert.ThrowsAsync<Sms.Application.Common.Exceptions.MissingAuditReasonException>(() => admin.UpdateStudentAsync(
                student.Id, "جديد", "أب", "جد", "عائلة", "Renamed", "Father", "Grandfather", "Family", Gender.Male, new DateTime(2018, 1, 1), 1));

            _audit.Reason = "birth certificate";
            var updated = await admin.UpdateStudentAsync(
                student.Id, "جديد", "أب", "جد", "عائلة", "Renamed", "Father", "Grandfather", "Family", Gender.Male, new DateTime(2018, 1, 1), 1, primaryIdNo: "1098765432");
            Assert.Equal("Renamed", updated.FirstNameEn);
            Assert.Equal("1098765432", db.Students.Single(s => s.Id == student.Id).PrimaryIdNo);
            _audit.Reason = null;
        }

        // --- residence: محافظة ← منطقة ← حي on the student's own record ---------
        //
        // Owner request, 2026-08-31. The same three cases the parent register is held to, asserted
        // again here rather than assumed from it: the two services share an exception type and
        // nothing else, so a student that silently stored a quarter belonging to another locality
        // would pass every parent test in the suite.

        /// <summary>
        /// Most localities have no quarters recorded at all — 7 across 34 in the seeded hierarchy —
        /// so a locality on its own is a complete address, not a half-filled one. A student who
        /// could only be placed by quarter would be unrecordable nearly everywhere.
        /// </summary>
        [Fact]
        [BusinessRule("BR-STU-001")]
        public async Task A_locality_on_its_own_is_a_complete_student_residence()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (areaId, _, _) = SeedResidenceHierarchy(db);
            var student = await Register(admin);

            await admin.SetResidenceAsync(student.Id, areaId, neighbourhoodId: null);

            var stored = db.Students.Single(s => s.Id == student.Id);
            Assert.Equal(areaId, stored.ResidenceAreaId);
            Assert.Null(stored.NeighbourhoodId);
        }

        /// <summary>A quarter with no locality under it is not a place, and is refused rather than stored.</summary>
        [Fact]
        [BusinessRule("BR-STU-001")]
        public async Task A_student_quarter_cannot_be_recorded_without_its_locality()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (_, _, hoodId) = SeedResidenceHierarchy(db);
            var student = await Register(admin);

            var refusal = await Assert.ThrowsAsync<InvalidResidenceSelectionException>(() =>
                admin.SetResidenceAsync(student.Id, residenceAreaId: null, neighbourhoodId: hoodId));

            Assert.Equal(ResidenceSelectionFault.QuarterWithoutLocality, refusal.Fault);
            Assert.Null(db.Students.Single(s => s.Id == student.Id).ResidenceAreaId);
        }

        /// <summary>
        /// A quarter belonging to a different locality is a worse record than none: the two levels
        /// would disagree, and every question asked of either would answer from the wrong place.
        /// </summary>
        [Fact]
        [BusinessRule("BR-STU-001")]
        public async Task A_student_quarter_from_another_locality_is_refused_rather_than_stored()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (_, otherAreaId, hoodId) = SeedResidenceHierarchy(db);
            var student = await Register(admin);

            var refusal = await Assert.ThrowsAsync<InvalidResidenceSelectionException>(() =>
                admin.SetResidenceAsync(student.Id, otherAreaId, hoodId));

            Assert.Equal(ResidenceSelectionFault.QuarterOutsideLocality, refusal.Fault);
            Assert.Null(db.Students.Single(s => s.Id == student.Id).NeighbourhoodId);
        }

        /// <summary>
        /// Clearing the locality clears the quarter beneath it. Otherwise blanking one box would be
        /// a back door to the orphaned quarter the refusal above exists to prevent — and there is no
        /// delete verb to undo it with (BR-GLB-005), so a residence entered by mistake has to be
        /// removable this way.
        /// </summary>
        [Fact]
        [BusinessRule("BR-GLB-005")]
        public async Task Clearing_a_students_locality_clears_the_quarter_under_it()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (areaId, _, hoodId) = SeedResidenceHierarchy(db);
            var student = await Register(admin);
            await admin.SetResidenceAsync(student.Id, areaId, hoodId);
            Assert.Equal(hoodId, db.Students.Single(s => s.Id == student.Id).NeighbourhoodId);

            await admin.SetResidenceAsync(student.Id, residenceAreaId: null, neighbourhoodId: null);

            var stored = db.Students.Single(s => s.Id == student.Id);
            Assert.Null(stored.ResidenceAreaId);
            Assert.Null(stored.NeighbourhoodId);
        }

        /// <summary>
        /// The student's address and the guardian's are independent (owner request, 2026-08-31):
        /// this is the cost the 2026-08-22 move was made to avoid, so it is asserted rather than
        /// left to be discovered — recording one does not touch the other.
        /// </summary>
        [Fact]
        [BusinessRule("BR-STU-001")]
        public async Task A_students_residence_does_not_reach_the_guardians_file()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (areaId, otherAreaId, _) = SeedResidenceHierarchy(db);
            var student = await Register(admin);
            var parent = new Sms.Domain.Parents.Parent
            {
                ParentFileNo = "PAR-1", NameAr = "أب", NameEn = "Father",
                PrimaryMobile = "0500000000", ResidenceAreaId = otherAreaId,
            };
            db.Parents.Add(parent);
            db.SaveChanges();

            await admin.SetResidenceAsync(student.Id, areaId, neighbourhoodId: null);

            Assert.Equal(areaId, db.Students.Single(s => s.Id == student.Id).ResidenceAreaId);
            Assert.Equal(otherAreaId, db.Parents.Single(p => p.Id == parent.Id).ResidenceAreaId);
        }

        /// <summary>One governorate, two localities under it, and a quarter inside the first only.</summary>
        private static (int AreaId, int OtherAreaId, int NeighbourhoodId) SeedResidenceHierarchy(AppDbContext db)
        {
            var governorate = new Governorate { Code = "GZ", Name = new LocalizedName("غزة", "Gaza"), SortOrder = 1 };
            db.Governorates.Add(governorate);
            db.SaveChanges();

            var area = new ResidenceArea { GovernorateId = governorate.Id, Code = "GZC", Name = new LocalizedName("غزة المدينة", "Gaza City"), SortOrder = 1 };
            var other = new ResidenceArea { GovernorateId = governorate.Id, Code = "JBL", Name = new LocalizedName("جباليا", "Jabalia"), SortOrder = 2 };
            db.ResidenceAreas.AddRange(area, other);
            db.SaveChanges();

            var hood = new Neighbourhood { ResidenceAreaId = area.Id, Code = "RMD", Name = new LocalizedName("الرمال", "Al Rimal"), SortOrder = 1 };
            db.Neighbourhoods.Add(hood);
            db.SaveChanges();

            return (area.Id, other.Id, hood.Id);
        }
    }
}
