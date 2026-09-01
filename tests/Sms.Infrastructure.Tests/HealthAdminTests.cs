using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Health;
using Sms.Domain.Attendance;
using Sms.Domain.Audit;
using Sms.Domain.Common;
using Sms.Domain.Grades;
using Sms.Domain.Health;
using Sms.Domain.Numbering;
using Sms.Domain.Parents;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Students;
using Sms.Infrastructure.Attendance;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Health;
using Sms.Infrastructure.Notifications;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>S6/E-602 (Health, doc/Modules/24, BR-HLT-001..010) over a real Sqlite-backed AppDbContext, with E-301 attendance and E-004 audit events.</summary>
    public sealed class HealthAdminTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 10, 5, 10, 5, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; } = 7;
        }

        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;

            public int AcademicYearId { get; set; }
        }

        private static readonly HashSet<DayOfWeek> KsaWeekend = new() { DayOfWeek.Friday, DayOfWeek.Saturday };

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();
        private int _yearId;
        private int _studentId;
        private int _parentId;
        private int _sectionId;
        private int _profileId;

        public HealthAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            db.NumberingSeries.Add(new NumberingSeries { Code = "MED", EntityName = "ClinicVisit", FormatTemplate = "MED-{SEQ:5}", ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Normal, EffectiveFromUtc = _clock.UtcNow, IsActive = true });
            var year = new AcademicYear { LabelAr = "Year", LabelEn = "2026-2027", HijriLabel = "Hijri", StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 6, 30), Status = AcademicYearStatus.Active };
            db.AcademicYears.Add(year);
            var stage = new Stage { Name = new LocalizedName("Stage", "Elementary"), SequenceOrder = 1, DefaultGenderPolicy = GenderPolicy.Mixed };
            db.Stages.Add(stage);
            db.SaveChanges();
            _tenant.AcademicYearId = year.Id;
            var grade = new GradeLevel { StageId = stage.Id, Code = "G3", Name = new LocalizedName("Grade", "Grade 3"), SequenceOrder = 3 };
            db.GradeLevels.Add(grade);
            db.SaveChanges();
            var profile = new GradeYearProfile { GradeLevelId = grade.Id, AcademicYearId = year.Id, GenderPolicy = GenderPolicy.Mixed, TargetSections = 1, TargetSectionSize = 25 };
            db.GradeYearProfiles.Add(profile);
            db.SaveChanges();
            var section = new Section { AcademicYearId = year.Id, GradeYearProfileId = profile.Id, NameAr = "Section", NameEn = "3-A", Capacity = 25, GenderPolicy = GenderPolicy.Mixed };
            db.Sections.Add(section);
            var parent = new Parent { ParentFileNo = "PAR-000001", NameAr = "Guardian", NameEn = "Guardian", PrimaryMobile = "0500000000", UserAccountId = 42 };
            db.Parents.Add(parent);
            db.SaveChanges();

            _yearId = year.Id;
            _profileId = profile.Id;
            _sectionId = section.Id;
            _parentId = parent.Id;
            _studentId = EnrollChild(db, "STU-1", new DateTime(2020, 1, 1));
        }

        public void Dispose() => _connection.Dispose();

        private int EnrollChild(AppDbContext db, string no, DateTime dob)
        {
            var student = new Student
            {
                StudentNo = no, FirstNameAr = "S", FatherNameAr = "F", GrandfatherNameAr = "G", FamilyNameAr = "Fam",
                FirstNameEn = "S", FatherNameEn = "F", GrandfatherNameEn = "G", FamilyNameEn = "Fam",
                Gender = Gender.Male, DateOfBirth = dob, NationalityLookupId = 1,
            };
            db.Students.Add(student);
            db.SaveChanges();
            var enrollment = new Enrollment { AcademicYearId = _yearId, StudentId = student.Id, GradeYearProfileId = _profileId, EnrollmentDate = new DateTime(2026, 9, 1), SourceType = EnrollmentSourceType.Admission };
            db.Enrollments.Add(enrollment);
            db.SaveChanges();
            db.SectionMemberships.Add(new SectionMembership { AcademicYearId = _yearId, SectionId = _sectionId, EnrollmentId = enrollment.Id, EffectiveFromUtc = new DateTime(2026, 9, 1) });
            db.StudentGuardianLinks.Add(new StudentGuardianLink
            {
                StudentId = student.Id, ParentId = _parentId, RelationshipLookupId = 1, IsPrimaryContact = true, IsFinanciallyResponsible = true,
                IsPickupAuthorized = true, EffectiveFromUtc = new DateTime(2026, 9, 1),
            });
            db.SaveChanges();
            return student.Id;
        }

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private HealthAdmin CreateAdmin(AppDbContext db) => new(
            db, new NumberIssuer(db, _tenant, _tenant, _clock), _clock, _tenant,
            new AuditEventWriter(db, _tenant, _tenant, _user, _clock, _audit), new NotificationPublisher(db, new TestAddressBook()), new AttendanceAdmin(db));

        // --- BR-HLT-001/002 file + banner ------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-HLT-001")]
        public async Task Opening_the_full_file_writes_a_read_audit_event_but_reading_the_banner_does_not()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await admin.AddAllergyAsync(_studentId, "Peanuts", AllergySeverity.Severe);

            await admin.GetEmergencyBannerAsync(_studentId);
            Assert.Empty(db.AuditEntries.Where(e => e.Action == AuditAction.View));

            var file = await admin.OpenMedicalFileAsync(_studentId);

            var view = db.AuditEntries.Single(e => e.Action == AuditAction.View && e.EntityType == nameof(MedicalFile));
            Assert.Equal(file.Id, view.EntityId);
            Assert.Equal(7, view.ActorUserId);
        }

        [Fact]
        [BusinessRule("BR-HLT-002")]
        public async Task The_banner_carries_severe_allergies_critical_conditions_and_the_nurse_curated_text()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await admin.AddAllergyAsync(_studentId, "Peanuts", AllergySeverity.Severe);
            await admin.AddAllergyAsync(_studentId, "Pollen", AllergySeverity.Mild);
            await admin.AddConditionAsync(_studentId, "Asthma", isCritical: true);
            await admin.AddConditionAsync(_studentId, "Myopia", isCritical: false);
            await admin.SetEmergencyBannerAsync(_studentId, "EpiPen in bag", "EpiPen in bag");

            var banner = await admin.GetEmergencyBannerAsync(_studentId);

            Assert.NotNull(banner);
            Assert.Equal(new[] { "Peanuts" }, banner!.SevereAllergies);
            Assert.Equal(new[] { "Asthma" }, banner.CriticalConditions);
            Assert.Equal("EpiPen in bag", banner.BannerEn);
        }

        [Fact]
        [BusinessRule("BR-HLT-003")]
        public async Task Stale_reconfirmations_are_listed_until_the_parent_reconfirms_for_the_working_year()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await admin.EnsureMedicalFileAsync(_studentId);

            Assert.Equal(new[] { _studentId }, await admin.StaleReconfirmationsAsync());
            await admin.ReconfirmAsync(_studentId);
            Assert.Empty(await admin.StaleReconfirmationsAsync());
        }

        // --- BR-HLT-005 visits ---------------------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-HLT-005")]
        public async Task A_visit_is_numbered_and_sent_home_needs_a_pickup_authorized_person_or_an_exception()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            var visit = await admin.RecordVisitAsync(_studentId, nurseUserId: 7, "headache", ClinicVisitOutcome.ReturnedToClass, temperatureC: 37.2m);
            Assert.Equal("MED-00001", visit.VisitNo);

            await Assert.ThrowsAsync<SentHomeWithoutVerifiedPickupException>(() => admin.RecordVisitAsync(_studentId, 7, "fever", ClinicVisitOutcome.SentHome, pickupByName: "Stranger"));
            var sentHome = await admin.RecordVisitAsync(_studentId, 7, "fever", ClinicVisitOutcome.SentHome, pickupByName: "Guardian");
            Assert.Equal("Guardian", sentHome.PickupVerifiedByName);
            var exception = await admin.RecordVisitAsync(_studentId, 7, "fever", ClinicVisitOutcome.SentHome, pickupExceptionNote: "ambulance transfer");
            Assert.Null(exception.PickupVerifiedByName);
            Assert.Equal("ambulance transfer", exception.PickupExceptionNote);
        }

        // --- BR-HLT-006 medication --------------------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-HLT-006")]
        public async Task Administration_within_the_authorization_logs_cleanly_and_a_deviation_needs_a_reason()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var auth = await admin.AuthorizeMedicationAsync(_studentId, "Salbutamol", 2m, "puffs", "10:00,14:00", new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), _parentId, isControlled: true);

            var ok = await admin.LogAdministrationAsync(auth.Id, 7, 2m, AdministrationStatus.Given);   // clock 10:05
            Assert.False(ok.IsDeviation);

            await Assert.ThrowsAsync<MedicationDeviationReasonRequiredException>(() => admin.LogAdministrationAsync(auth.Id, 7, 4m, AdministrationStatus.Given));
            var deviation = await admin.LogAdministrationAsync(auth.Id, 7, 4m, AdministrationStatus.Given, "acute episode per care plan");
            Assert.True(deviation.IsDeviation);

            var refused = await admin.LogAdministrationAsync(auth.Id, 7, 0m, AdministrationStatus.Refused);
            Assert.False(refused.IsDeviation);
            Assert.Single(await admin.ControlledStorageListAsync());
        }

        // --- BR-HLT-004 vaccinations ---------------------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-HLT-004")]
        public async Task Campaign_doses_require_granted_consent_and_status_reflects_the_pack_schedule()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await admin.DefineVaccinationScheduleAsync(new[] { ("MMR", 1, 12), ("MMR", 2, 18), ("HPV", 1, 132) });
            await admin.RecordExternalVaccinationAsync(_studentId, "MMR", 1, new DateTime(2021, 1, 10));
            var campaign = await admin.DefineVaccinationCampaignAsync("MMR2", "MMR dose 2", "MMR", 2, new DateTime(2026, 10, 10));

            await Assert.ThrowsAsync<VaccinationConsentMissingException>(() => admin.AdministerCampaignDoseAsync(campaign.Id, _studentId, new DateTime(2026, 10, 10)));
            await admin.RecordConsentAsync(campaign.Id, _studentId, _parentId, isGranted: false);
            await Assert.ThrowsAsync<VaccinationConsentMissingException>(() => admin.AdministerCampaignDoseAsync(campaign.Id, _studentId, new DateTime(2026, 10, 10)));
            _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
            await admin.RecordConsentAsync(campaign.Id, _studentId, _parentId, isGranted: true);
            var record = await admin.AdministerCampaignDoseAsync(campaign.Id, _studentId, new DateTime(2026, 10, 10));

            Assert.Equal(VaccinationSource.SchoolAdministered, record.Source);
            var status = await admin.VaccinationStatusAsync(_studentId);
            Assert.Equal(VaccinationDueEvaluator.DoseState.Given, status.Single(s => s.VaccineCode == "MMR" && s.DoseNumber == 2).State);
            Assert.Equal(VaccinationDueEvaluator.DoseState.NotYetDue, status.Single(s => s.VaccineCode == "HPV").State);
        }

        // --- BR-HLT-008 screenings -----------------------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-HLT-008")]
        public async Task Abnormal_results_are_referred_and_stats_stay_anonymized_counts()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var second = EnrollChild(db, "STU-2", new DateTime(2020, 5, 5));
            var campaign = await admin.DefineScreeningCampaignAsync(ScreeningType.Vision, new DateTime(2026, 10, 5), _profileId);

            var abnormal = await admin.RecordScreeningResultAsync(campaign.Id, _studentId, isAbnormal: true, 0.4m, 0.5m);
            await admin.RecordScreeningResultAsync(campaign.Id, second, isAbnormal: false, 1.0m, 1.0m);
            await admin.CompleteFollowUpAsync(abnormal.Id);

            Assert.NotNull(abnormal.ReferralIssuedAtUtc);
            var stats = await admin.ScreeningStatsAsync(campaign.Id);
            Assert.Equal((2, 1, 1, 1), (stats.Screened, stats.Abnormal, stats.Referred, stats.FollowedUp));
        }

        // --- BR-HLT-009 infectious disease --------------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-HLT-009")]
        public async Task An_infectious_case_pre_captures_medical_leave_on_working_days_only()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var attendance = new AttendanceAdmin(db);
            var enrollmentId = db.Enrollments.Single(e => e.StudentId == _studentId).Id;
            await attendance.CaptureAsync(enrollmentId, new DateTime(2026, 10, 7), AttendanceStatus.Present, 1);   // already captured that day

            await admin.RecordInfectiousCaseAsync(_studentId, "Chickenpox", new DateTime(2026, 10, 7), new DateTime(2026, 10, 12), preApproveAbsence: true, KsaWeekend, recordedByUserId: 7);

            var days = db.AttendanceDays.Where(a => a.EnrollmentId == enrollmentId).OrderBy(a => a.Date).ToList();
            // 10/7 Wed (kept Present), 10/8 Thu leave, 10/9 Fri + 10/10 Sat weekend skipped, 10/11 Sun + 10/12 Mon leave
            Assert.Equal(new[] { new DateTime(2026, 10, 7), new DateTime(2026, 10, 8), new DateTime(2026, 10, 11), new DateTime(2026, 10, 12) }, days.Select(d => d.Date));
            Assert.Equal(AttendanceStatus.Present, days[0].Status);
            Assert.All(days.Skip(1), d => Assert.Equal(AttendanceStatus.MedicalLeave, d.Status));
        }

        [Fact]
        [BusinessRule("BR-HLT-009")]
        public async Task An_exposure_notice_is_principal_approved_sent_once_and_carries_no_student()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var notice = await admin.DraftExposureNoticeAsync(_sectionId, "Chickenpox", new DateTime(2026, 10, 5), new DateTime(2026, 10, 7));

            await admin.ApproveAndSendExposureNoticeAsync(notice.Id, approvedByUserId: 9);

            var sent = db.ExposureNotices.Single();
            Assert.Equal(ExposureNoticeStatus.Sent, sent.Status);
            Assert.Equal(9, sent.ApprovedByUserId);
            await Assert.ThrowsAsync<ExposureNoticeAlreadySentException>(() => admin.ApproveAndSendExposureNoticeAsync(notice.Id, 9));
        }

        // --- BR-HLT-007/010 -------------------------------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-HLT-007")]
        public async Task Care_plans_surface_for_annual_review()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await admin.DefineCarePlanAsync(_studentId, "Asthma", "exercise, cold air", "inhaler 2 puffs; if no relief call 997", new DateTime(2027, 9, 1));

            Assert.Empty(await admin.CarePlansDueForReviewAsync(new DateTime(2027, 6, 1)));
            Assert.Single(await admin.CarePlansDueForReviewAsync(new DateTime(2027, 9, 1)));
        }

        [Fact]
        [BusinessRule("BR-HLT-010")]
        public async Task File_changes_are_field_level_audited()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await admin.EnsureMedicalFileAsync(_studentId);

            await admin.SetEmergencyBannerAsync(_studentId, null, "Severe nut allergy");

            Assert.Contains(db.AuditEntries, e => e.EntityType == nameof(MedicalFile) && e.FieldName == nameof(MedicalFile.EmergencyBannerEn) && e.NewValue == "Severe nut allergy");
        }
    }
}
