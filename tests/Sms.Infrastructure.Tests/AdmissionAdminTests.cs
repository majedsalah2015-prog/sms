using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Admissions;
using Sms.Domain.Common;
using Sms.Domain.Grades;
using Sms.Domain.Numbering;
using Sms.Domain.Parents;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Students;
using Sms.Infrastructure.Admissions;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Sections;
using Sms.Infrastructure.Students;
using Sms.TestSupport;
using Xunit;
using AdmissionApplication = Sms.Domain.Admissions.Application;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// S2/E-201 (Admissions, doc/Modules/09, BR-ADM-001..011) over a real
    /// Sqlite-backed AppDbContext. RegisterAsync is the one method that
    /// composes IStudentAdmin + ISectionAdmin under an explicit transaction
    /// (BR-ADM-007) — covered here rather than by a mock-based test since
    /// the whole point is proving the composed SaveChangesAsync calls
    /// commit atomically against a real provider.
    /// </summary>
    public sealed class AdmissionAdminTests : IDisposable
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
        private int _campaignId;
        private int _parentId;

        public AdmissionAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            db.NumberingSeries.Add(new NumberingSeries
            {
                Code = "APP", EntityName = "AdmissionApplication", FormatTemplate = "APP-{SEQ:6}",
                ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Normal, EffectiveFromUtc = _clock.UtcNow, IsActive = true,
            });
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

            var grade = new GradeLevel { StageId = stage.Id, Code = "KG1", Name = new LocalizedName("روضة أولى", "KG1"), SequenceOrder = 1 };
            db.GradeLevels.Add(grade);
            db.SaveChanges();

            var profile = new GradeYearProfile
            {
                GradeLevelId = grade.Id, AcademicYearId = year.Id, GenderPolicy = GenderPolicy.Mixed,
                TargetSections = 1, TargetSectionSize = 25,
                MinAgeAtCutoff = 5m, MaxAgeAtCutoff = 6m, AgeCutoffDate = new DateTime(2026, 9, 1),
            };
            db.GradeYearProfiles.Add(profile);
            db.SaveChanges();

            var campaign = new AdmissionCampaign
            {
                SchoolId = 1, AcademicYearId = year.Id, GradeYearProfileId = profile.Id,
                OpenDate = new DateTime(2026, 1, 1), CloseDate = new DateTime(2026, 6, 1), IsActive = true,
            };
            db.AdmissionCampaigns.Add(campaign);

            var parent = new Parent
            {
                SchoolId = 1, ParentFileNo = "PAR-000001", NameAr = "ولي أمر", NameEn = "Guardian", PrimaryMobile = "0500000000",
            };
            db.Parents.Add(parent);
            db.SaveChanges();

            _yearId = year.Id;
            _profileId = profile.Id;
            _campaignId = campaign.Id;
            _parentId = parent.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private AdmissionAdmin CreateAdmin(AppDbContext db)
        {
            var numberIssuer = new NumberIssuer(db, _tenant, _tenant, _clock);
            var studentAdmin = new StudentAdmin(db, numberIssuer);
            var sectionAdmin = new SectionAdmin(db);
            return new AdmissionAdmin(db, numberIssuer, studentAdmin, sectionAdmin);
        }

        private Task<AdmissionApplication> Submit(AdmissionAdmin admin, DateTime dateOfBirth, int? parentId = null, string suffix = "1")
            => admin.SubmitApplicationAsync(
                _campaignId, "متقدم" + suffix, "أب", "جد", "عائلة", "Applicant" + suffix, "Father", "Grandfather", "Family",
                Gender.Male, dateOfBirth, nationalityLookupId: 1, parentId: parentId);

        // --- BR-ADM-001/002 campaign + submission -------------------------------

        [Fact]
        [BusinessRule("BR-ADM-001")]
        public async Task Defining_a_campaign_copies_school_and_year_from_its_grade_year_profile()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            var campaign = await admin.DefineCampaignAsync(_profileId, new DateTime(2027, 1, 1), new DateTime(2027, 6, 1), requiresAssessment: false, applicationFeeAmount: null);

            Assert.Equal(1, campaign.SchoolId);
            Assert.Equal(_yearId, campaign.AcademicYearId);
        }

        [Fact]
        [BusinessRule("BR-ADM-002")]
        public async Task Submitting_issues_a_real_application_number_via_the_APP_series()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            var application = await Submit(admin, new DateTime(2021, 1, 1));

            Assert.Equal("APP-000001", application.ApplicationNo);
            Assert.Equal(ApplicationStatus.Draft, application.Status);
        }

        [Fact]
        [BusinessRule("BR-ADM-002")]
        public async Task A_second_live_application_for_the_same_parent_in_the_same_campaign_is_blocked()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            await Submit(admin, new DateTime(2021, 1, 1), parentId: _parentId, suffix: "1");

            await Assert.ThrowsAsync<DuplicateLiveApplicationException>(() =>
                Submit(admin, new DateTime(2021, 2, 1), parentId: _parentId, suffix: "2"));
        }

        [Fact]
        [BusinessRule("BR-GRD-005")]
        public async Task An_applicant_outside_the_grades_age_range_is_rejected()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            // Cutoff 2026-09-01, min 5 / max 6 — born 2010 is far too old.
            await Assert.ThrowsAsync<AgeIneligibleException>(() => Submit(admin, new DateTime(2010, 1, 1)));
        }

        // --- BR-ADM-005 status transitions --------------------------------------

        [Fact]
        [BusinessRule("BR-ADM-005")]
        public async Task Changing_status_along_a_legal_path_succeeds()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var application = await Submit(admin, new DateTime(2021, 1, 1));

            await admin.ChangeStatusAsync(application.Id, ApplicationStatus.Submitted);

            Assert.Equal(ApplicationStatus.Submitted, db.Applications.Single(a => a.Id == application.Id).Status);
        }

        [Fact]
        [BusinessRule("BR-ADM-005")]
        public async Task Changing_status_along_an_illegal_path_is_rejected()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var application = await Submit(admin, new DateTime(2021, 1, 1));

            await Assert.ThrowsAsync<InvalidApplicationStatusTransitionException>(() =>
                admin.ChangeStatusAsync(application.Id, ApplicationStatus.Approved));
        }

        // --- BR-ADM-006 waiting list ---------------------------------------------

        [Fact]
        [BusinessRule("BR-ADM-006")]
        public async Task Waiting_list_entries_are_ranked_in_submission_order()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var first = await Submit(admin, new DateTime(2021, 1, 1), suffix: "1");
            var second = await Submit(admin, new DateTime(2021, 2, 1), suffix: "2");

            var firstEntry = await admin.AddToWaitingListAsync(first.Id, _profileId);
            var secondEntry = await admin.AddToWaitingListAsync(second.Id, _profileId);

            Assert.Equal(1, firstEntry.OrderRank);
            Assert.Equal(2, secondEntry.OrderRank);
        }

        // --- BR-ADM-007 registration (one transaction) ---------------------------

        private async Task<(AdmissionApplication application, int sectionId)> ApproveApplicationWithSection(AdmissionAdmin admin, AppDbContext db)
        {
            var application = await Submit(admin, new DateTime(2021, 1, 1), parentId: _parentId);
            await admin.ChangeStatusAsync(application.Id, ApplicationStatus.Submitted);
            await admin.ChangeStatusAsync(application.Id, ApplicationStatus.UnderReview);
            await admin.ChangeStatusAsync(application.Id, ApplicationStatus.Recommended);
            await admin.ChangeStatusAsync(application.Id, ApplicationStatus.Approved);

            var sectionAdmin = new SectionAdmin(db);
            var section = await sectionAdmin.DefineSectionAsync(_profileId, "روضة-أ", "KG1-A", capacity: 25, GenderPolicy.Mixed);

            return (await db.Applications.SingleAsync(a => a.Id == application.Id), section.Id);
        }

        [Fact]
        [BusinessRule("BR-ADM-007")]
        public async Task Registering_an_approved_application_creates_student_enrollment_and_membership_atomically()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (application, sectionId) = await ApproveApplicationWithSection(admin, db);

            var student = await admin.RegisterAsync(application.Id, sectionId, new DateTime(2026, 9, 1), guardianRelationshipLookupId: 1);

            Assert.Equal("STU-000001", student.StudentNo);
            Assert.Equal(ApplicationStatus.Registered, db.Applications.Single(a => a.Id == application.Id).Status);
            Assert.Equal(student.Id, db.Applications.Single(a => a.Id == application.Id).RegisteredStudentId);

            var enrollment = db.Enrollments.Single(e => e.StudentId == student.Id);
            Assert.Equal(EnrollmentStatus.Active, enrollment.Status);

            var membership = db.SectionMemberships.Single(m => m.EnrollmentId == enrollment.Id);
            Assert.Equal(sectionId, membership.SectionId);

            Assert.True(db.StudentGuardianLinks.Any(l => l.StudentId == student.Id && l.ParentId == _parentId));
        }

        [Fact]
        [BusinessRule("BR-ADM-007")]
        public async Task Registering_a_non_approved_application_is_rejected()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var application = await Submit(admin, new DateTime(2021, 1, 1), parentId: _parentId);

            var sectionAdmin = new SectionAdmin(db);
            var section = await sectionAdmin.DefineSectionAsync(_profileId, "روضة-أ", "KG1-A", capacity: 25, GenderPolicy.Mixed);

            await Assert.ThrowsAsync<ApplicationNotReadyForRegistrationException>(() =>
                admin.RegisterAsync(application.Id, section.Id, new DateTime(2026, 9, 1), guardianRelationshipLookupId: 1));
        }

        [Fact]
        [BusinessRule("BR-ADM-007")]
        public async Task Registering_without_a_linked_parent_is_rejected()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var application = await Submit(admin, new DateTime(2021, 1, 1)); // no parentId
            await admin.ChangeStatusAsync(application.Id, ApplicationStatus.Submitted);
            await admin.ChangeStatusAsync(application.Id, ApplicationStatus.UnderReview);
            await admin.ChangeStatusAsync(application.Id, ApplicationStatus.Recommended);
            await admin.ChangeStatusAsync(application.Id, ApplicationStatus.Approved);

            var sectionAdmin = new SectionAdmin(db);
            var section = await sectionAdmin.DefineSectionAsync(_profileId, "روضة-أ", "KG1-A", capacity: 25, GenderPolicy.Mixed);

            await Assert.ThrowsAsync<ApplicationNotReadyForRegistrationException>(() =>
                admin.RegisterAsync(application.Id, section.Id, new DateTime(2026, 9, 1), guardianRelationshipLookupId: 1));
        }
    }
}
