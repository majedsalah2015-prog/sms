using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Certificates;
using Sms.Domain.Common;
using Sms.Domain.Grades;
using Sms.Domain.Numbering;
using Sms.Domain.Schools;
using Sms.Domain.Students;
using Sms.Domain.Subjects;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Certificates;
using Sms.Infrastructure.Fees;
using Sms.Infrastructure.Grading;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// S4/E-403 (Certificates, doc/Modules/18, BR-CRT-001..010) over a real
    /// Sqlite-backed AppDbContext. Prerequisite checks reuse E-302's
    /// TermResult and E-303's IFeeAdmin.ComputeStudentPositionAsync for real.
    /// </summary>
    public sealed class CertificateAdminTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2027, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; }
        }

        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;

            // Mutable - CertificateAdmin.HasPublishedResultsAsync filters real Enrollment rows by
            // the tenant's "current year" pointer, so it must match the actual AcademicYear.Id.
            public int AcademicYearId { get; set; } = 2027;
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();
        private int _yearId;
        private int _studentId;
        private int _enrollmentId;
        private int _offeringId;
        private int _termId;
        private int _stageId;

        public CertificateAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            foreach (var (code, entity, format) in new[]
            {
                ("CERT", "Certificate", "CERT-{SEQ:5}"), ("TC", "TransferCertificate", "TC-{SEQ:4}"), ("INV", "Charge", "INV-{SEQ:6}"),
            })
            {
                db.NumberingSeries.Add(new NumberingSeries
                {
                    Code = code, EntityName = entity, FormatTemplate = format,
                    ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Normal, EffectiveFromUtc = _clock.UtcNow, IsActive = true,
                });
            }

            var year = new AcademicYear
            {
                LabelAr = "Year", LabelEn = "2026-2027", HijriLabel = "Hijri",
                StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 6, 30), Status = AcademicYearStatus.Active,
            };
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

            var subject = new Subject { SchoolId = 1, Code = "MATH", Name = new LocalizedName("Subject", "Math"), Category = "core" };
            db.Subjects.Add(subject);
            db.SaveChanges();

            var offering = new CurriculumOffering
            {
                SchoolId = 1, AcademicYearId = year.Id, GradeYearProfileId = profile.Id, SubjectId = subject.Id,
                WeeklyPeriods = 5, IsAssessable = true, GpaWeight = 1m, EffectiveFromUtc = new DateTime(2026, 9, 1),
            };
            db.CurriculumOfferings.Add(offering);
            db.SaveChanges();

            var semester = new Semester { AcademicYearId = year.Id, SequenceNumber = 1, NameAr = "S1", NameEn = "Semester 1", StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 1, 31) };
            db.Semesters.Add(semester);
            db.SaveChanges();
            var term = new Term { AcademicYearId = year.Id, SemesterId = semester.Id, SequenceNumber = 1, NameAr = "T1", NameEn = "Term 1", StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2026, 11, 30) };
            db.Terms.Add(term);
            db.SaveChanges();

            var student = new Student
            {
                StudentNo = "STU-TEST-1",
                FirstNameAr = "Student", FatherNameAr = "Father", GrandfatherNameAr = "Grandfather", FamilyNameAr = "Family",
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

            _yearId = year.Id;
            _studentId = student.Id;
            _enrollmentId = enrollment.Id;
            _offeringId = offering.Id;
            _termId = term.Id;
            _stageId = stage.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private CertificateAdmin CreateAdmin(AppDbContext db)
        {
            var numberIssuer = new NumberIssuer(db, _tenant, _tenant, _clock);
            var feeAdmin = new FeeAdmin(db, numberIssuer, _clock);
            return new CertificateAdmin(db, numberIssuer, _clock, _audit, feeAdmin, _tenant, _tenant);
        }

        private async Task PublishATermResultAsync(AppDbContext db)
        {
            var gradingAdmin = new GradingAdmin(db, _clock, _audit);
            var scale = await gradingAdmin.DefineScaleAsync(_stageId, "Scale", "Percentage");
            await gradingAdmin.AddScaleBandAsync(scale.Id, 0m, 100m, "P", "Pass", "Pass", isPassing: true, sortOrder: 1);
            var blueprint = await gradingAdmin.DefineBlueprintAsync(_offeringId, _termId, scale.Id);
            var component = await gradingAdmin.AddBlueprintComponentAsync(blueprint.Id, "Final", "Final", weight: 100m, maxScore: 100m);
            await gradingAdmin.LockBlueprintAsync(blueprint.Id);

            var section = new Domain.Sections.Section
            {
                SchoolId = 1, AcademicYearId = _yearId, GradeYearProfileId = db.GradeYearProfiles.Single().Id,
                NameAr = "Section", NameEn = "3-A", Capacity = 25, GenderPolicy = GenderPolicy.Mixed,
            };
            db.Sections.Add(section);
            await db.SaveChangesAsync();
            db.SectionMemberships.Add(new Domain.Sections.SectionMembership
            {
                AcademicYearId = _yearId, SectionId = section.Id, EnrollmentId = _enrollmentId, EffectiveFromUtc = new DateTime(2026, 9, 1),
            });
            await db.SaveChangesAsync();

            var marksheet = await gradingAdmin.CreateMarksheetAsync(blueprint.Id, section.Id);
            await gradingAdmin.EnterMarkAsync(marksheet.Id, component.Id, _enrollmentId, 90m, isAbsent: false, isExempt: false);
            await gradingAdmin.ChangeMarksheetStatusAsync(marksheet.Id, Domain.Grading.MarksheetStatus.Submitted);
            await gradingAdmin.ChangeMarksheetStatusAsync(marksheet.Id, Domain.Grading.MarksheetStatus.HoDReviewed);
            await gradingAdmin.ChangeMarksheetStatusAsync(marksheet.Id, Domain.Grading.MarksheetStatus.Approved);
            await gradingAdmin.ChangeMarksheetStatusAsync(marksheet.Id, Domain.Grading.MarksheetStatus.Published);
        }

        // --- BR-CRT-003 published-results prerequisite ------------------------------

        [Fact]
        [BusinessRule("BR-CRT-003")]
        public async Task Approving_without_published_results_is_rejected_when_required()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var type = await admin.DefineTypeAsync(CertificateKind.Transcript, "Transcript", "Transcript", requiresPublishedResults: true, feeClearanceRule: FeeClearanceRule.Disabled, isPortalRequestable: false);
            var request = await admin.RequestAsync(type.Id, _studentId, requestedByUserId: 1);

            await Assert.ThrowsAsync<CertificatePrerequisitesNotMetException>(() => admin.ApproveAsync(request.Id));
        }

        [Fact]
        [BusinessRule("BR-CRT-003")]
        public async Task Approving_with_published_results_succeeds()
        {
            using var db = CreateContext();
            await PublishATermResultAsync(db);
            var admin = CreateAdmin(db);
            var type = await admin.DefineTypeAsync(CertificateKind.Transcript, "Transcript", "Transcript", requiresPublishedResults: true, feeClearanceRule: FeeClearanceRule.Disabled, isPortalRequestable: false);
            var request = await admin.RequestAsync(type.Id, _studentId, requestedByUserId: 1);

            await admin.ApproveAsync(request.Id);

            Assert.Equal(Domain.Certificates.CertificateRequestStatus.Approved, db.CertificateRequests.Single(r => r.Id == request.Id).Status);
        }

        // --- BR-CRT-008 fee-clearance prerequisite -----------------------------------

        [Fact]
        [BusinessRule("BR-CRT-008")]
        public async Task Approving_with_an_outstanding_balance_is_rejected_when_clearance_required()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var numberIssuer = new NumberIssuer(db, _tenant, _tenant, _clock);
            var feeAdmin = new FeeAdmin(db, numberIssuer, _clock);
            var category = await feeAdmin.DefineCategoryAsync("Tuition", "Tuition", vatRate: null, isMandatory: true, isRefundable: false, isServiceLinked: false);
            var payer = new Domain.Fees.Payer { Type = Domain.Fees.PayerType.Parent };
            db.Payers.Add(payer);
            await db.SaveChangesAsync();
            await feeAdmin.PostManualChargeAsync(_studentId, payer.Id, category.Id, amount: 500m);

            var type = await admin.DefineTypeAsync(CertificateKind.EnrollmentProof, "EnrollmentProof", "Enrollment Proof", requiresPublishedResults: false, feeClearanceRule: FeeClearanceRule.FullClearance, isPortalRequestable: true);
            var request = await admin.RequestAsync(type.Id, _studentId, requestedByUserId: 1);

            await Assert.ThrowsAsync<CertificateFeeClearanceBlockedException>(() => admin.ApproveAsync(request.Id));
        }

        [Fact]
        [BusinessRule("BR-CRT-008")]
        public async Task Approving_with_no_outstanding_balance_succeeds()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var type = await admin.DefineTypeAsync(CertificateKind.EnrollmentProof, "EnrollmentProof", "Enrollment Proof", requiresPublishedResults: false, feeClearanceRule: FeeClearanceRule.FullClearance, isPortalRequestable: true);
            var request = await admin.RequestAsync(type.Id, _studentId, requestedByUserId: 1);

            await admin.ApproveAsync(request.Id);

            Assert.Equal(Domain.Certificates.CertificateRequestStatus.Approved, db.CertificateRequests.Single(r => r.Id == request.Id).Status);
        }

        // --- BR-CRT-002/003 issuance ---------------------------------------------------

        [Fact]
        [BusinessRule("BR-CRT-002")]
        public async Task Issuing_before_approval_is_rejected()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var type = await admin.DefineTypeAsync(CertificateKind.EnrollmentProof, "EnrollmentProof", "Enrollment Proof", false, FeeClearanceRule.Disabled, true);
            var request = await admin.RequestAsync(type.Id, _studentId, requestedByUserId: 1);

            await Assert.ThrowsAsync<InvalidCertificateRequestStatusTransitionException>(() => admin.IssueAsync(request.Id));
        }

        [Fact]
        [BusinessRule("BR-CRT-002")]
        public async Task Issuing_an_approved_request_issues_a_real_number_and_a_verification_code()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var type = await admin.DefineTypeAsync(CertificateKind.EnrollmentProof, "EnrollmentProof", "Enrollment Proof", false, FeeClearanceRule.Disabled, true);
            var request = await admin.RequestAsync(type.Id, _studentId, requestedByUserId: 1);
            await admin.ApproveAsync(request.Id);

            var issue = await admin.IssueAsync(request.Id);

            Assert.Equal("CERT-00001", issue.CertificateNo);
            Assert.NotEmpty(issue.VerificationCode);
            Assert.NotEmpty(issue.DataSnapshotJson);
            Assert.Equal(Domain.Certificates.CertificateRequestStatus.Issued, db.CertificateRequests.Single(r => r.Id == request.Id).Status);
        }

        [Fact]
        [BusinessRule("BR-CRT-001")]
        public async Task A_type_with_its_own_series_code_numbers_from_that_series()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var type = await admin.DefineTypeAsync(CertificateKind.TransferCertificate, "TransferCertificate", "Transfer Certificate", false, FeeClearanceRule.Disabled, false, numberingSeriesCode: "TC");
            var request = await admin.RequestAsync(type.Id, _studentId, requestedByUserId: 1);
            await admin.ApproveAsync(request.Id);

            var issue = await admin.IssueAsync(request.Id);

            Assert.StartsWith("TC-", issue.CertificateNo);
        }

        // --- BR-CRT-006 revocation -------------------------------------------------------

        [Fact]
        [BusinessRule("BR-CRT-006")]
        public async Task Revoking_an_issued_certificate_records_the_reason()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var type = await admin.DefineTypeAsync(CertificateKind.EnrollmentProof, "EnrollmentProof", "Enrollment Proof", false, FeeClearanceRule.Disabled, true);
            var request = await admin.RequestAsync(type.Id, _studentId, requestedByUserId: 1);
            await admin.ApproveAsync(request.Id);
            var issue = await admin.IssueAsync(request.Id);

            await admin.RevokeAsync(issue.Id, "issued in error");

            var updated = db.CertificateIssues.Single(i => i.Id == issue.Id);
            Assert.Equal(Domain.Certificates.CertificateIssueStatus.Revoked, updated.Status);
            Assert.Equal("issued in error", updated.RevokedReason);
        }

        [Fact]
        [BusinessRule("BR-CRT-006")]
        public async Task Revoking_an_already_revoked_certificate_is_rejected()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var type = await admin.DefineTypeAsync(CertificateKind.EnrollmentProof, "EnrollmentProof", "Enrollment Proof", false, FeeClearanceRule.Disabled, true);
            var request = await admin.RequestAsync(type.Id, _studentId, requestedByUserId: 1);
            await admin.ApproveAsync(request.Id);
            var issue = await admin.IssueAsync(request.Id);
            await admin.RevokeAsync(issue.Id, "first revocation");

            await Assert.ThrowsAsync<CertificateNotIssuedException>(() => admin.RevokeAsync(issue.Id, "second attempt"));
        }

        // --- BR-CRT-007 reprints ----------------------------------------------------------

        [Fact]
        [BusinessRule("BR-CRT-007")]
        public async Task Reprinting_increments_the_count()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var type = await admin.DefineTypeAsync(CertificateKind.EnrollmentProof, "EnrollmentProof", "Enrollment Proof", false, FeeClearanceRule.Disabled, true);
            var request = await admin.RequestAsync(type.Id, _studentId, requestedByUserId: 1);
            await admin.ApproveAsync(request.Id);
            var issue = await admin.IssueAsync(request.Id);

            await admin.ReprintAsync(issue.Id);
            var updated = await admin.ReprintAsync(issue.Id);

            Assert.Equal(2, updated.ReprintCount);
        }

        // --- BR-CRT-005 verification -------------------------------------------------------

        [Fact]
        [BusinessRule("BR-CRT-005")]
        public async Task Verifying_a_real_code_finds_the_certificate_and_logs_the_hit()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var type = await admin.DefineTypeAsync(CertificateKind.EnrollmentProof, "EnrollmentProof", "Enrollment Proof", false, FeeClearanceRule.Disabled, true);
            var request = await admin.RequestAsync(type.Id, _studentId, requestedByUserId: 1);
            await admin.ApproveAsync(request.Id);
            var issue = await admin.IssueAsync(request.Id);

            var found = await admin.VerifyAsync(issue.VerificationCode);

            Assert.NotNull(found);
            Assert.Equal(issue.Id, found!.Id);
            Assert.True(db.VerificationLogs.Single().WasFound);
        }

        [Fact]
        [BusinessRule("BR-CRT-005")]
        public async Task Verifying_an_unknown_code_returns_null_and_logs_the_miss()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            var found = await admin.VerifyAsync("does-not-exist");

            Assert.Null(found);
            Assert.False(db.VerificationLogs.Single().WasFound);
        }

        // --- BR-CRT-008 blocking rule, legal gate, Principal override ------------------------

        [Fact]
        [BusinessRule("BR-CRT-008")]
        public async Task A_transfer_certificate_type_cannot_be_fee_gated_under_the_KSA_pack()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            await Assert.ThrowsAsync<CertificateKindNotGateableException>(() => admin.DefineTypeAsync(
                CertificateKind.TransferCertificate, "TC", "Transfer Certificate", false, FeeClearanceRule.FullClearance, false, numberingSeriesCode: "TC"));
        }

        [Fact]
        [BusinessRule("BR-CRT-008")]
        public async Task The_no_overdue_rule_is_refused_until_charges_carry_due_dates()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            await Assert.ThrowsAsync<FeeClearanceRuleNotSupportedException>(() => admin.DefineTypeAsync(
                CertificateKind.EnrollmentProof, "EnrollmentProof", "Enrollment Proof", false, FeeClearanceRule.NoOverdue, true));
        }

        private async Task PostAChargeAsync(AppDbContext db, int studentId, decimal amount)
        {
            var numberIssuer = new NumberIssuer(db, _tenant, _tenant, _clock);
            var feeAdmin = new FeeAdmin(db, numberIssuer, _clock);
            var category = await feeAdmin.DefineCategoryAsync("Tuition", "Tuition", vatRate: null, isMandatory: true, isRefundable: false, isServiceLinked: false);
            var payer = new Domain.Fees.Payer { Type = Domain.Fees.PayerType.Parent };
            db.Payers.Add(payer);
            await db.SaveChangesAsync();
            await feeAdmin.PostManualChargeAsync(studentId, payer.Id, category.Id, amount);
        }

        [Fact]
        [BusinessRule("BR-CRT-008")]
        public async Task A_clearance_block_reports_the_position_and_leaves_the_request_pending()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await PostAChargeAsync(db, _studentId, 750m);
            var type = await admin.DefineTypeAsync(CertificateKind.EnrollmentProof, "EnrollmentProof", "Enrollment Proof", false, FeeClearanceRule.FullClearance, true);
            var request = await admin.RequestAsync(type.Id, _studentId, requestedByUserId: 1);

            var ex = await Assert.ThrowsAsync<CertificateFeeClearanceBlockedException>(() => admin.ApproveAsync(request.Id));

            Assert.Equal(750m, ex.Position);
            Assert.Equal(CertificateRequestStatus.Requested, db.CertificateRequests.Single(r => r.Id == request.Id).Status);
        }

        [Fact]
        [BusinessRule("BR-CRT-008")]
        public async Task A_principal_override_with_a_reason_approves_past_the_block_and_is_T1_audited()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await PostAChargeAsync(db, _studentId, 750m);
            var type = await admin.DefineTypeAsync(CertificateKind.EnrollmentProof, "EnrollmentProof", "Enrollment Proof", false, FeeClearanceRule.FullClearance, true);
            var request = await admin.RequestAsync(type.Id, _studentId, requestedByUserId: 1);

            await admin.ApproveAsync(request.Id, clearanceOverrideReason: "principal approved hardship case");

            var updated = db.CertificateRequests.Single(r => r.Id == request.Id);
            Assert.Equal(CertificateRequestStatus.Approved, updated.Status);
            Assert.True(updated.ClearanceOverridden);
            var audit = db.AuditEntries.Single(e => e.EntityType == nameof(CertificateRequest) && e.FieldName == nameof(CertificateRequest.ClearanceOverridden));
            Assert.Equal("principal approved hardship case", audit.Reason);
        }

        [Fact]
        [BusinessRule("BR-CRT-008")]
        public async Task An_override_never_bypasses_the_published_results_prerequisite()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var type = await admin.DefineTypeAsync(CertificateKind.Transcript, "Transcript", "Transcript", requiresPublishedResults: true, feeClearanceRule: FeeClearanceRule.FullClearance, isPortalRequestable: false);
            var request = await admin.RequestAsync(type.Id, _studentId, requestedByUserId: 1);

            await Assert.ThrowsAsync<CertificatePrerequisitesNotMetException>(() => admin.ApproveAsync(request.Id, clearanceOverrideReason: "override attempt"));
        }

        [Fact]
        [BusinessRule("BR-CRT-008")]
        public async Task Approving_without_override_does_not_demand_an_audit_reason()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            _audit.Reason = null;
            var type = await admin.DefineTypeAsync(CertificateKind.EnrollmentProof, "EnrollmentProof", "Enrollment Proof", false, FeeClearanceRule.FullClearance, true);
            var request = await admin.RequestAsync(type.Id, _studentId, requestedByUserId: 1);

            await admin.ApproveAsync(request.Id);

            Assert.False(db.CertificateRequests.Single(r => r.Id == request.Id).ClearanceOverridden);
        }

        // --- BR-CRT-001 validity ---------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-CRT-001")]
        public async Task An_expiring_type_stamps_the_expiry_and_a_non_expiring_one_does_not()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var proof = await admin.DefineTypeAsync(CertificateKind.EnrollmentProof, "EnrollmentProof", "Enrollment Proof", false, FeeClearanceRule.Disabled, true, validityDays: 90);
            var honor = await admin.DefineTypeAsync(CertificateKind.Honor, "Honor", "Honor", false, FeeClearanceRule.Disabled, false);
            var proofRequest = await admin.RequestAsync(proof.Id, _studentId, 1);
            var honorRequest = await admin.RequestAsync(honor.Id, _studentId, 1);
            await admin.ApproveAsync(proofRequest.Id);
            await admin.ApproveAsync(honorRequest.Id);

            var proofIssue = await admin.IssueAsync(proofRequest.Id);
            var honorIssue = await admin.IssueAsync(honorRequest.Id);

            Assert.Equal(_clock.UtcNow.AddDays(90), proofIssue.ExpiresAtUtc);
            Assert.Null(honorIssue.ExpiresAtUtc);
        }

        // --- BR-CRT-004 snapshot + reissue ------------------------------------------------------

        [Fact]
        [BusinessRule("BR-CRT-004")]
        public async Task The_snapshot_freezes_student_and_school_identity_at_issuance()
        {
            using var db = CreateContext();
            db.Schools.Add(new School { NameAr = "School", NameEn = "Test School", LicenseNumber = "LIC-1", MinistryCode = "MIN-1" });
            await db.SaveChangesAsync();
            var admin = CreateAdmin(db);
            var type = await admin.DefineTypeAsync(CertificateKind.EnrollmentProof, "EnrollmentProof", "Enrollment Proof", false, FeeClearanceRule.Disabled, true);
            var request = await admin.RequestAsync(type.Id, _studentId, 1);
            await admin.ApproveAsync(request.Id);
            var issue = await admin.IssueAsync(request.Id);

            var student = db.Students.Single(s => s.Id == _studentId);
            student.FamilyNameEn = "Renamed";
            _audit.Reason = "identity correction";
            await db.SaveChangesAsync();

            var persisted = db.CertificateIssues.Single(i => i.Id == issue.Id);
            Assert.Contains("\"FamilyNameEn\":\"Family\"", persisted.DataSnapshotJson);
            Assert.Contains("\"SchoolNameEn\":\"Test School\"", persisted.DataSnapshotJson);
            Assert.DoesNotContain("Renamed", persisted.DataSnapshotJson);
        }

        [Fact]
        [BusinessRule("BR-CRT-004")]
        public async Task Reissuing_creates_a_new_certificate_with_a_new_number_and_revokes_the_original_when_asked()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var type = await admin.DefineTypeAsync(CertificateKind.EnrollmentProof, "EnrollmentProof", "Enrollment Proof", false, FeeClearanceRule.Disabled, true);
            var request = await admin.RequestAsync(type.Id, _studentId, 1);
            await admin.ApproveAsync(request.Id);
            var original = await admin.IssueAsync(request.Id);

            var reissue = await admin.ReissueAsync(original.Id, revokeOriginalReason: "name corrected");

            Assert.NotEqual(original.Id, reissue.Id);
            Assert.NotEqual(original.CertificateNo, reissue.CertificateNo);
            Assert.NotEqual(original.CertificateRequestId, reissue.CertificateRequestId);
            Assert.Equal(original.Id, reissue.ReissuedFromCertificateIssueId);
            Assert.Equal(CertificateIssueStatus.Revoked, db.CertificateIssues.Single(i => i.Id == original.Id).Status);
            Assert.Equal(CertificateIssueStatus.Issued, db.CertificateIssues.Single(i => i.Id == reissue.Id).Status);
        }

        [Fact]
        [BusinessRule("BR-CRT-004")]
        public async Task Reissuing_without_a_revocation_reason_leaves_the_original_issued()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var type = await admin.DefineTypeAsync(CertificateKind.EnrollmentProof, "EnrollmentProof", "Enrollment Proof", false, FeeClearanceRule.Disabled, true);
            var request = await admin.RequestAsync(type.Id, _studentId, 1);
            await admin.ApproveAsync(request.Id);
            var original = await admin.IssueAsync(request.Id);

            await admin.ReissueAsync(original.Id);

            Assert.Equal(CertificateIssueStatus.Issued, db.CertificateIssues.Single(i => i.Id == original.Id).Status);
            Assert.Equal(2, db.CertificateIssues.Count());
        }

        // --- BR-CRT-009 bulk issuance ----------------------------------------------------------

        private async Task<Student> EnrollAnotherStudentAsync(AppDbContext db, int profileId, int ordinal)
        {
            var student = new Student
            {
                StudentNo = $"STU-TEST-{ordinal}",
                FirstNameAr = "S", FatherNameAr = "F", GrandfatherNameAr = "G", FamilyNameAr = "Fam",
                FirstNameEn = "S", FatherNameEn = "F", GrandfatherNameEn = "G", FamilyNameEn = "Fam",
                Gender = Gender.Male, DateOfBirth = new DateTime(2018, 1, ordinal), NationalityLookupId = 1,
            };
            db.Students.Add(student);
            await db.SaveChangesAsync();
            db.Enrollments.Add(new Enrollment
            {
                AcademicYearId = _yearId, StudentId = student.Id, GradeYearProfileId = profileId,
                EnrollmentDate = new DateTime(2026, 9, 1), SourceType = EnrollmentSourceType.Admission,
            });
            await db.SaveChangesAsync();
            return student;
        }

        [Fact]
        [BusinessRule("BR-CRT-009")]
        public async Task A_batch_issues_individual_numbers_and_queues_the_students_that_fail_the_check()
        {
            using var db = CreateContext();
            var profileId = db.GradeYearProfiles.Single().Id;
            var second = await EnrollAnotherStudentAsync(db, profileId, 2);
            await PostAChargeAsync(db, second.Id, 300m);

            var admin = CreateAdmin(db);
            var type = await admin.DefineTypeAsync(CertificateKind.Completion, "Completion", "Completion", false, FeeClearanceRule.FullClearance, false);

            var batch = await admin.IssueBatchAsync(type.Id, profileId, requestedByUserId: 1);

            Assert.Single(batch.Issued);
            Assert.Equal(_studentId, batch.Issued[0].StudentId);
            Assert.Single(batch.Exceptions);
            Assert.Equal(second.Id, batch.Exceptions[0].StudentId);
            Assert.Equal(CertificateRequestStatus.Requested, db.CertificateRequests.Single(r => r.Id == batch.Exceptions[0].CertificateRequestId).Status);
        }

        [Fact]
        [BusinessRule("BR-CRT-009")]
        public async Task A_batch_gives_every_member_its_own_number()
        {
            using var db = CreateContext();
            var profileId = db.GradeYearProfiles.Single().Id;
            await EnrollAnotherStudentAsync(db, profileId, 2);
            await EnrollAnotherStudentAsync(db, profileId, 3);

            var admin = CreateAdmin(db);
            var type = await admin.DefineTypeAsync(CertificateKind.Completion, "Completion", "Completion", false, FeeClearanceRule.Disabled, false);

            var batch = await admin.IssueBatchAsync(type.Id, profileId, requestedByUserId: 1);

            Assert.Equal(3, batch.Issued.Count);
            Assert.Equal(3, batch.Issued.Select(i => i.CertificateNo).Distinct().Count());
            Assert.Empty(batch.Exceptions);
        }

        // --- BR-CRT-010 permanent T1 register ---------------------------------------------------

        [Fact]
        [BusinessRule("BR-CRT-010")]
        public async Task Revocation_is_field_level_audited_with_its_reason()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var type = await admin.DefineTypeAsync(CertificateKind.EnrollmentProof, "EnrollmentProof", "Enrollment Proof", false, FeeClearanceRule.Disabled, true);
            var request = await admin.RequestAsync(type.Id, _studentId, 1);
            await admin.ApproveAsync(request.Id);
            var issue = await admin.IssueAsync(request.Id);

            await admin.RevokeAsync(issue.Id, "fraud");

            var audit = db.AuditEntries.Single(e => e.EntityType == nameof(CertificateIssue) && e.FieldName == nameof(CertificateIssue.Status));
            Assert.Equal("fraud", audit.Reason);
        }
    }
}
