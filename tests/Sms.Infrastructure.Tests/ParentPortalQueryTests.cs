using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Attendance;
using Sms.Domain.Common;
using Sms.Domain.Fees;
using Sms.Domain.Grades;
using Sms.Domain.Grading;
using Sms.Domain.Numbering;
using Sms.Domain.Parents;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Security;
using Sms.Domain.Students;
using Sms.Domain.Subjects;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Portal;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// S3/E-304 (Portal essentials, BR-SEC-010..013) over a real
    /// Sqlite-backed AppDbContext — a read-only composition over E-301
    /// Attendance + E-302 Grading + E-303 Fees data.
    /// </summary>
    public sealed class ParentPortalQueryTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2027, 3, 1, 8, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; }
        }

        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;

            // Mutable (unlike other fixtures' fixed 2027) - ParentPortalQuery is the
            // first consumer that filters real Enrollment rows by the tenant's
            // "current year" pointer, so it must match the actual AcademicYear.Id.
            public int AcademicYearId { get; set; } = 2027;
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();
        private int _studentId;
        private int _parentUserAccountId;
        private int _studentUserAccountId;
        private int _enrollmentId;
        private int _offeringId;
        private int _termId;

        public ParentPortalQueryTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            db.NumberingSeries.Add(new NumberingSeries
            {
                Code = "INV", EntityName = "Charge", FormatTemplate = "INV-{SEQ:6}",
                ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Strict, EffectiveFromUtc = _clock.UtcNow, IsActive = true,
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
            _tenant.AcademicYearId = year.Id;

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

            var semester = new Semester { AcademicYearId = year.Id, SequenceNumber = 1, NameAr = "الفصل الأول", NameEn = "Semester 1", StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 1, 31) };
            db.Semesters.Add(semester);
            db.SaveChanges();

            var term = new Term { AcademicYearId = year.Id, SemesterId = semester.Id, SequenceNumber = 1, NameAr = "الفترة الأولى", NameEn = "Term 1", StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2026, 11, 30) };
            db.Terms.Add(term);
            db.SaveChanges();

            var studentAccount = new UserAccount { UserName = "student1", AccountType = AccountType.Student };
            var parentAccount = new UserAccount { UserName = "parent1", AccountType = AccountType.Parent };
            db.UserAccounts.Add(studentAccount);
            db.UserAccounts.Add(parentAccount);
            db.SaveChanges();

            var student = new Student
            {
                StudentNo = "STU-TEST-1", UserAccountId = studentAccount.Id,
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

            var parent = new Parent { ParentFileNo = "PAR-000001", NameAr = "ولي أمر", NameEn = "Guardian", PrimaryMobile = "0500000000", UserAccountId = parentAccount.Id };
            db.Parents.Add(parent);
            db.SaveChanges();

            db.StudentGuardianLinks.Add(new StudentGuardianLink
            {
                StudentId = student.Id, ParentId = parent.Id, RelationshipLookupId = 1,
                IsPrimaryContact = true, IsFinanciallyResponsible = true, IsPickupAuthorized = true, IsPortalVisible = true,
                EffectiveFromUtc = new DateTime(2026, 9, 1),
            });

            var payer = new Payer { Type = PayerType.Parent, ParentId = parent.Id };
            db.Payers.Add(payer);
            db.SaveChanges();

            // Attendance: 3 captured days, 1 unexcused absence.
            var dates = new[] { new DateTime(2026, 9, 1), new DateTime(2026, 9, 2), new DateTime(2026, 9, 3) };
            var statuses = new[] { AttendanceStatus.Present, AttendanceStatus.Present, AttendanceStatus.AbsentUnexcused };
            for (var i = 0; i < dates.Length; i++)
            {
                db.AttendanceDays.Add(new AttendanceDay
                {
                    AcademicYearId = year.Id, EnrollmentId = enrollment.Id, SectionId = section.Id,
                    Date = dates[i], Status = statuses[i], CapturedByUserId = 1,
                });
            }

            // Grading: a finalized blueprint + published marksheet, giving one TermResult.
            var scale = new GradingScale { StageId = stage.Id, NameAr = "نسبة", NameEn = "Percentage" };
            db.GradingScales.Add(scale);
            db.SaveChanges();
            db.ScaleBands.Add(new ScaleBand { GradingScaleId = scale.Id, MinPercent = 0m, MaxPercent = 100m, BandCode = "P", LabelAr = "ناجح", LabelEn = "Pass", IsPassing = true, SortOrder = 1 });
            db.SaveChanges();

            var blueprint = new Blueprint { AcademicYearId = year.Id, CurriculumOfferingId = offering.Id, TermId = term.Id, GradingScaleId = scale.Id, IsLocked = true };
            db.Blueprints.Add(blueprint);
            db.SaveChanges();
            var component = new BlueprintComponent { BlueprintId = blueprint.Id, NameAr = "اختبار", NameEn = "Test", Weight = 100m, MaxScore = 100m };
            db.BlueprintComponents.Add(component);
            db.SaveChanges();

            var marksheet = new Marksheet { AcademicYearId = year.Id, BlueprintId = blueprint.Id, SectionId = section.Id, Status = MarksheetStatus.Published, PublishedAtUtc = _clock.UtcNow };
            db.Marksheets.Add(marksheet);
            db.SaveChanges();
            db.TermResults.Add(new TermResult
            {
                AcademicYearId = year.Id, EnrollmentId = enrollment.Id, CurriculumOfferingId = offering.Id, TermId = term.Id,
                ScorePercent = 88m, ScaleBandId = db.ScaleBands.Single().Id, CalculationSnapshotJson = "{}", PublishedAtUtc = _clock.UtcNow,
            });

            // Fees: a posted charge.
            var category = new FeeCategory { NameAr = "رسوم دراسية", NameEn = "Tuition", IsMandatory = true };
            db.FeeCategories.Add(category);
            db.SaveChanges();
            db.Charges.Add(new Charge
            {
                AcademicYearId = year.Id, StudentId = student.Id, PayerId = payer.Id, FeeCategoryId = category.Id,
                SourceType = ChargeSourceType.Manual, ChargeNo = "INV-000001", NetAmount = 1000m, GrossAmount = 1000m,
                Status = ChargeStatus.Posted, PostedAtUtc = _clock.UtcNow, InvoiceUuid = Guid.NewGuid(),
            });

            db.SaveChanges();

            _studentId = student.Id;
            _parentUserAccountId = parentAccount.Id;
            _studentUserAccountId = studentAccount.Id;
            _enrollmentId = enrollment.Id;
            _offeringId = offering.Id;
            _termId = term.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        // --- BR-PAR-004/BR-SEC-011 visibility ---------------------------------------

        [Fact]
        [BusinessRule("BR-PAR-004")]
        public async Task A_parent_sees_their_linked_child()
        {
            using var db = CreateContext();
            var query = new ParentPortalQuery(db, _tenant);

            var children = await query.GetVisibleChildrenAsync(_parentUserAccountId);

            Assert.Single(children);
            Assert.Equal(_studentId, children[0].StudentId);
        }

        [Fact]
        [BusinessRule("BR-SEC-011")]
        public async Task An_account_with_no_parent_record_sees_no_children()
        {
            using var db = CreateContext();
            var query = new ParentPortalQuery(db, _tenant);

            var children = await query.GetVisibleChildrenAsync(_studentUserAccountId);

            Assert.Empty(children);
        }

        [Fact]
        [BusinessRule("BR-SEC-011")]
        public async Task An_unrelated_account_is_denied_access_to_a_students_data()
        {
            using var db = CreateContext();
            var query = new ParentPortalQuery(db, _tenant);

            await Assert.ThrowsAsync<PortalAccessDeniedException>(() => query.GetAttendanceSummaryAsync(requestingUserAccountId: 9999, _studentId));
        }

        [Fact]
        [BusinessRule("BR-SEC-011")]
        public async Task A_student_can_view_their_own_attendance()
        {
            using var db = CreateContext();
            var query = new ParentPortalQuery(db, _tenant);

            var summary = await query.GetAttendanceSummaryAsync(_studentUserAccountId, _studentId);

            Assert.Equal(3, summary.ScheduledDays);
        }

        [Fact]
        [BusinessRule("BR-SEC-011")]
        public async Task A_revoked_guardianship_link_removes_portal_visibility()
        {
            using var db = CreateContext();
            var link = db.StudentGuardianLinks.Single(l => l.StudentId == _studentId);
            link.EffectiveToUtc = _clock.UtcNow;
            db.SaveChanges();
            var query = new ParentPortalQuery(db, _tenant);

            await Assert.ThrowsAsync<PortalAccessDeniedException>(() => query.GetAttendanceSummaryAsync(_parentUserAccountId, _studentId));
        }

        // --- BR-ATD-009 attendance summary -------------------------------------------

        [Fact]
        [BusinessRule("BR-ATD-009")]
        public async Task Attendance_summary_computes_the_central_percentage()
        {
            using var db = CreateContext();
            var query = new ParentPortalQuery(db, _tenant);

            var summary = await query.GetAttendanceSummaryAsync(_parentUserAccountId, _studentId);

            Assert.Equal(3, summary.ScheduledDays);
            Assert.Equal(1, summary.AbsentDays);
            Assert.Equal(0, summary.ExemptedDays);
            Assert.Equal(Sms.Application.Attendance.AttendancePercentageCalculator.Calculate(3, 0, 1), summary.AttendancePercent);
        }

        // --- BR-SEC-012 published results only ---------------------------------------

        [Fact]
        [BusinessRule("BR-SEC-012")]
        public async Task Published_results_are_returned_with_their_band()
        {
            using var db = CreateContext();
            var query = new ParentPortalQuery(db, _tenant);

            var results = await query.GetPublishedResultsAsync(_parentUserAccountId, _studentId);

            Assert.Single(results);
            Assert.Equal(_offeringId, results[0].CurriculumOfferingId);
            Assert.Equal(_termId, results[0].TermId);
            Assert.Equal(88m, results[0].ScorePercent);
            Assert.Equal("P", results[0].BandCode);
        }

        // --- BR-SEC-012 posted charges only + BR-FEE-008 position ---------------------

        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task Fee_position_reflects_the_posted_charge()
        {
            using var db = CreateContext();
            var query = new ParentPortalQuery(db, _tenant);

            var position = await query.GetFeePositionAsync(_parentUserAccountId, _studentId);

            Assert.Equal(1000m, position.Position);
            Assert.Single(position.Charges);
            Assert.Equal("INV-000001", position.Charges[0].ChargeNo);
        }
    }
}
