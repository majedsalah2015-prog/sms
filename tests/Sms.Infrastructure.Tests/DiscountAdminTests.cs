using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Discounts;
using Sms.Application.Installments;
using Sms.Domain.Common;
using Sms.Domain.Discounts;
using Sms.Domain.Employees;
using Sms.Domain.Fees;
using Sms.Domain.Grades;
using Sms.Domain.Installments;
using Sms.Domain.Numbering;
using Sms.Domain.Parents;
using Sms.Domain.Payments;
using Sms.Domain.Schools;
using Sms.Domain.Students;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Discounts;
using Sms.Infrastructure.Fees;
using Sms.Infrastructure.Installments;
using Sms.Infrastructure.Notifications;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Payments;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Statements;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// S5/E-502 (Discounts + statements, doc/Modules/22, BR-DIS-001..010)
    /// over a real Sqlite-backed AppDbContext, with E-303 charges/payments
    /// and E-501 schedules so cross-module effects are exercised for real.
    /// </summary>
    public sealed class DiscountAdminTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 9, 15, 8, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; }
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
        private int _profileId;
        private int _studentId;
        private int _parentId;
        private int _payerId;
        private int _tuitionId;
        private int _transportId;

        public DiscountAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            foreach (var (code, template) in new[] { ("INV", "INV-{SEQ:6}"), ("RCP", "RCP-{SEQ:6}"), ("CRN", "CRN-{SEQ:5}"), ("DSC", "DSC-{SEQ:5}"), ("STM", "STM-{SEQ:6}") })
            {
                db.NumberingSeries.Add(new NumberingSeries
                {
                    Code = code, EntityName = code, FormatTemplate = template,
                    ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Strict, EffectiveFromUtc = _clock.UtcNow, IsActive = true,
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

            var parent = new Parent { ParentFileNo = "PAR-000001", NameAr = "Guardian", NameEn = "Guardian", PrimaryMobile = "0500000000" };
            db.Parents.Add(parent);
            db.SaveChanges();
            var payer = new Payer { Type = PayerType.Parent, ParentId = parent.Id };
            db.Payers.Add(payer);

            var tuition = new FeeCategory { NameAr = "Tuition", NameEn = "Tuition", IsMandatory = true, IsRefundable = true, VatRate = 0.15m };
            var transport = new FeeCategory { NameAr = "Transport", NameEn = "Transport", IsRefundable = true, IsServiceLinked = true };
            db.FeeCategories.AddRange(tuition, transport);
            db.SaveChanges();

            _yearId = year.Id;
            _profileId = profile.Id;
            _parentId = parent.Id;
            _payerId = payer.Id;
            _tuitionId = tuition.Id;
            _transportId = transport.Id;
            _studentId = EnrollChild(db, "STU-1", new DateTime(2015, 1, 1));
        }

        public void Dispose() => _connection.Dispose();

        private int EnrollChild(AppDbContext db, string no, DateTime dob, int? parentId = null)
        {
            var student = new Student
            {
                StudentNo = no,
                FirstNameAr = "S", FatherNameAr = "F", GrandfatherNameAr = "G", FamilyNameAr = "Fam",
                FirstNameEn = "S", FatherNameEn = "F", GrandfatherNameEn = "G", FamilyNameEn = "Fam",
                Gender = Gender.Male, DateOfBirth = dob, NationalityLookupId = 1,
            };
            db.Students.Add(student);
            db.SaveChanges();
            db.Enrollments.Add(new Enrollment
            {
                AcademicYearId = _yearId, StudentId = student.Id, GradeYearProfileId = _profileId,
                EnrollmentDate = new DateTime(2026, 9, 1), SourceType = EnrollmentSourceType.Admission,
            });
            db.StudentGuardianLinks.Add(new StudentGuardianLink
            {
                StudentId = student.Id, ParentId = parentId ?? _parentId, RelationshipLookupId = 1, IsPrimaryContact = true,
                IsFinanciallyResponsible = true, EffectiveFromUtc = new DateTime(2026, 9, 1),
            });
            db.SaveChanges();
            return student.Id;
        }

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private NumberIssuer Issuer(AppDbContext db) => new(db, _tenant, _tenant, _clock);

        private FeeAdmin CreateFeeAdmin(AppDbContext db) => new(db, Issuer(db), _clock);

        private PaymentAdmin CreatePaymentAdmin(AppDbContext db) => new(db, Issuer(db), _clock);

        private InstallmentAdmin CreateInstallmentAdmin(AppDbContext db) => new(db, _clock, _audit, _tenant, new NotificationPublisher(db));

        private DiscountAdmin CreateAdmin(AppDbContext db) => new(db, Issuer(db), _clock, _audit, _tenant, CreateFeeAdmin(db), CreateInstallmentAdmin(db));

        private StatementService CreateStatements(AppDbContext db) => new(db, Issuer(db), _clock);

        private Task<Charge> PostCharge(AppDbContext db, decimal net, int? studentId = null, int? categoryId = null)
            => CreateFeeAdmin(db).PostManualChargeAsync(studentId ?? _studentId, _payerId, categoryId ?? _tuitionId, net);

        // --- BR-DIS-003 manual grants + BR-DIS-001 stacking ------------------------------------

        [Fact]
        [BusinessRule("BR-DIS-003")]
        public async Task A_manual_grant_is_threshold_routed_and_hardship_types_need_documentation()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await PostCharge(db, 1000m);
            var small = await admin.DefineTypeAsync("Neg", "Negotiated", DiscountBasis.Percentage, DiscountEligibilityMode.Manual);
            var hardship = await admin.DefineTypeAsync("Hardship", "Hardship", DiscountBasis.Percentage, DiscountEligibilityMode.Manual, requiresHardshipDocumentation: true);

            var fm = await admin.ProposeManualGrantAsync(_studentId, small.Id, 10m, "negotiated", 1);
            var owner = await admin.ProposeManualGrantAsync(_studentId, small.Id, 30m, "owner discretion", 1);

            Assert.Equal(ApprovalTier.FinanceManager, fm.RequiredTier);
            Assert.Equal(ApprovalTier.Owner, owner.RequiredTier);
            await Assert.ThrowsAsync<HardshipDocumentationRequiredException>(() => admin.ProposeManualGrantAsync(_studentId, hardship.Id, 50m, "hardship", 1));
        }

        [Fact]
        [BusinessRule("BR-DIS-001")]
        public async Task Stacking_policy_is_enforced_at_grant()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await PostCharge(db, 1000m);
            var stackable = await admin.DefineTypeAsync("A", "A", DiscountBasis.Percentage, DiscountEligibilityMode.Manual, maxCombinedPercent: 30m);
            var exclusive = await admin.DefineTypeAsync("B", "B", DiscountBasis.Percentage, DiscountEligibilityMode.Manual, isStackable: false);
            await admin.ProposeManualGrantAsync(_studentId, stackable.Id, 20m, "first", 1);

            await admin.ProposeManualGrantAsync(_studentId, stackable.Id, 10m, "second within cap", 1);
            await Assert.ThrowsAsync<DiscountStackingViolationException>(() => admin.ProposeManualGrantAsync(_studentId, stackable.Id, 5m, "over cap", 1));
            await Assert.ThrowsAsync<DiscountStackingViolationException>(() => admin.ProposeManualGrantAsync(_studentId, exclusive.Id, 5m, "exclusive beside others", 1));
        }

        // --- BR-DIS-005 application ------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-DIS-005")]
        public async Task Approval_issues_numbered_discount_documents_that_reduce_the_position()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var fees = CreateFeeAdmin(db);
            await PostCharge(db, 1000m);   // 1150 gross with 15% VAT
            var type = await admin.DefineTypeAsync("Neg", "Negotiated", DiscountBasis.Percentage, DiscountEligibilityMode.Manual, feeCategoryId: _tuitionId);
            var grant = await admin.ProposeManualGrantAsync(_studentId, type.Id, 10m, "negotiated", 1);

            await admin.ApproveGrantAsync(grant.Id, approvedByUserId: 2);

            var doc = db.DiscountDocuments.Single();
            Assert.Equal("DSC-00001", doc.DocumentNo);
            Assert.Equal(115m, doc.Amount);
            Assert.Equal(1035m, await fees.ComputeStudentPositionAsync(_studentId));
            var stored = db.DiscountGrants.Single();
            Assert.Equal(DiscountGrantStatus.Approved, stored.Status);
            Assert.Equal(115m, stored.AppliedAmount);
            Assert.Equal(2, stored.ApprovedByUserId);
        }

        [Fact]
        [BusinessRule("BR-DIS-005")]
        public async Task A_discount_never_drives_a_charge_below_zero_remaining()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await PostCharge(db, 1000m, categoryId: _transportId);   // no VAT: 1000
            await CreatePaymentAdmin(db).CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 950m);
            var type = await admin.DefineTypeAsync("Big", "Big", DiscountBasis.Percentage, DiscountEligibilityMode.Manual);
            var grant = await admin.ProposeManualGrantAsync(_studentId, type.Id, 20m, "late discount", 1);

            await admin.ApproveGrantAsync(grant.Id, 2);

            Assert.Equal(50m, db.DiscountDocuments.Single().Amount);
            Assert.Equal(0m, await CreateFeeAdmin(db).ComputeStudentPositionAsync(_studentId));
        }

        [Fact]
        [BusinessRule("BR-DIS-005")]
        public async Task Payments_allocate_against_the_discounted_remainder()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var payments = CreatePaymentAdmin(db);
            await PostCharge(db, 1000m, categoryId: _transportId);
            var type = await admin.DefineTypeAsync("Neg", "Neg", DiscountBasis.Percentage, DiscountEligibilityMode.Manual);
            var grant = await admin.ProposeManualGrantAsync(_studentId, type.Id, 10m, "negotiated", 1);
            await admin.ApproveGrantAsync(grant.Id, 2);

            await payments.CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 1000m);

            Assert.Equal(900m, db.PaymentAllocations.ToList().Sum(a => a.AllocatedAmount));   // 100 stays as advance
        }

        [Fact]
        [BusinessRule("BR-DIS-005")]
        public async Task An_approved_discount_recomputes_the_forward_installments()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var installments = CreateInstallmentAdmin(db);
            await PostCharge(db, 1000m, categoryId: _transportId);
            var template = await installments.DefineTemplateAsync(_yearId, "Q", "Quarterly", new[]
            {
                new TemplateSplit(50m, new DateTime(2026, 10, 4)), new TemplateSplit(50m, new DateTime(2027, 2, 7)),
            });
            await installments.ApproveTemplateAsync(template.Id);
            var assignment = await installments.AssignPlanAsync(_studentId, _payerId, template.Id, KsaWeekend);
            var type = await admin.DefineTypeAsync("Neg", "Neg", DiscountBasis.Percentage, DiscountEligibilityMode.Manual);
            var grant = await admin.ProposeManualGrantAsync(_studentId, type.Id, 20m, "negotiated", 1);

            await admin.ApproveGrantAsync(grant.Id, 2);

            var schedule = await installments.GetScheduleAsync(assignment.Id);
            Assert.Equal(new[] { 500m, 300m }, schedule.Select(i => i.Amount));   // future-first reduction
            Assert.Single(db.ScheduleRevisions.Where(r => r.Cause == ScheduleRevisionCause.Reduced));
        }

        // --- BR-DIS-002 automatic eligibility ---------------------------------------------------

        [Fact]
        [BusinessRule("BR-DIS-002")]
        public async Task Sibling_ladder_proposes_grants_for_the_third_child_onward_and_batch_approves()
        {
            using var db = CreateContext();
            var second = EnrollChild(db, "STU-2", new DateTime(2017, 1, 1));
            var third = EnrollChild(db, "STU-3", new DateTime(2019, 1, 1));
            var fourth = EnrollChild(db, "STU-4", new DateTime(2021, 1, 1));
            foreach (var id in new[] { _studentId, second, third, fourth })
            {
                await PostCharge(db, 1000m, id, _transportId);
            }

            var admin = CreateAdmin(db);
            var type = await admin.DefineTypeAsync("Sibling", "Sibling", DiscountBasis.Percentage, DiscountEligibilityMode.Automatic,
                renewalMode: DiscountRenewalMode.AutoReevaluate,
                rules: new[] { new EligibilityRuleInput(EligibilityRuleKind.SiblingLadder, 10m, 3), new EligibilityRuleInput(EligibilityRuleKind.SiblingLadder, 15m, 4) });

            var proposed = await admin.ProposeAutomaticGrantsAsync(type.Id, proposedByUserId: 1);

            Assert.Equal(2, proposed.Count);
            Assert.Equal(10m, proposed.Single(g => g.StudentId == third).BasisValue);
            Assert.Equal(15m, proposed.Single(g => g.StudentId == fourth).BasisValue);
            Assert.All(proposed, g => Assert.Equal(DiscountGrantSource.Automatic, g.Source));

            await admin.ApproveGrantsAsync(proposed.Select(g => g.Id).ToList(), approvedByUserId: 2);
            Assert.Equal(new[] { 100m, 150m }, db.DiscountDocuments.OrderBy(d => d.Id).Select(d => d.Amount));
            Assert.Empty(await admin.ProposeAutomaticGrantsAsync(type.Id, 1));   // idempotent
        }

        [Fact]
        [BusinessRule("BR-DIS-002")]
        public async Task Staff_discount_reaches_children_of_active_employees_through_the_user_account_bridge()
        {
            using var db = CreateContext();
            db.Parents.Single(p => p.Id == _parentId).UserAccountId = 77;
            db.Employees.Add(new Employee
            {
                EmployeeNo = "EMP-1", UserAccountId = 77, FirstNameAr = "E", FatherNameAr = "F", GrandfatherNameAr = "G", FamilyNameAr = "Fam",
                FirstNameEn = "E", FatherNameEn = "F", GrandfatherNameEn = "G", FamilyNameEn = "Fam", Gender = Gender.Male,
                DateOfBirth = new DateTime(1985, 1, 1), NationalityLookupId = 1, Status = EmployeeStatus.Active,
            });
            await db.SaveChangesAsync();
            await PostCharge(db, 1000m, categoryId: _transportId);
            var admin = CreateAdmin(db);
            var type = await admin.DefineTypeAsync("Staff", "Staff", DiscountBasis.Percentage, DiscountEligibilityMode.Automatic,
                rules: new[] { new EligibilityRuleInput(EligibilityRuleKind.Staff, 50m) });

            var proposed = await admin.ProposeAutomaticGrantsAsync(type.Id, 1);

            Assert.Equal(_studentId, Assert.Single(proposed).StudentId);
            Assert.Equal(50m, proposed[0].BasisValue);
        }

        // --- BR-DIS-004 scholarships --------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-DIS-004")]
        public async Task Envelope_blocks_over_budget_awards_unless_the_owner_overrides_with_a_reason()
        {
            using var db = CreateContext();
            var second = EnrollChild(db, "STU-2", new DateTime(2017, 1, 1));
            await PostCharge(db, 1000m, categoryId: _transportId);
            await PostCharge(db, 1000m, second, _transportId);
            var admin = CreateAdmin(db);
            var type = await admin.DefineTypeAsync("Sch", "Scholarship", DiscountBasis.Percentage, DiscountEligibilityMode.Scholarship);
            var program = await admin.DefineScholarshipProgramAsync("Excellence", "Excellence", type.Id, maxAwards: 1, maxTotalAmount: null);
            var first = await admin.NominateForScholarshipAsync(_studentId, program.Id, 100m, "top of class", 1);
            var next = await admin.NominateForScholarshipAsync(second, program.Id, 100m, "runner up", 1, sponsorNote: "Sponsor X");
            Assert.Equal(ApprovalTier.Committee, first.RequiredTier);

            await admin.ApproveGrantAsync(first.Id, 3);
            await Assert.ThrowsAsync<ScholarshipEnvelopeExhaustedException>(() => admin.ApproveGrantAsync(next.Id, 3));
            await admin.ApproveGrantAsync(next.Id, 3, envelopeOverrideReason: "board minute 12");

            Assert.Equal("board minute 12", db.DiscountGrants.Single(g => g.Id == next.Id).EnvelopeOverrideReason);
            Assert.Equal(2, db.DiscountDocuments.Count());
        }

        // --- BR-DIS-008 revocation -----------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-DIS-008")]
        public async Task Revocation_needs_a_future_effective_date_and_a_reason_and_forgives_the_past_by_default()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await PostCharge(db, 1000m, categoryId: _transportId);
            var type = await admin.DefineTypeAsync("Neg", "Neg", DiscountBasis.Percentage, DiscountEligibilityMode.Manual);
            var grant = await admin.ProposeManualGrantAsync(_studentId, type.Id, 10m, "negotiated", 1);
            await admin.ApproveGrantAsync(grant.Id, 2);

            await Assert.ThrowsAsync<RevocationDateInPastException>(() => admin.RevokeGrantAsync(grant.Id, new DateTime(2026, 9, 1), "fraud"));
            await admin.RevokeGrantAsync(grant.Id, new DateTime(2026, 10, 1), "policy breach");

            var stored = db.DiscountGrants.Single();
            Assert.Equal(DiscountGrantStatus.Revoked, stored.Status);
            Assert.Equal(1, db.Charges.Count());   // no claw-back charge
            var audit = db.AuditEntries.Single(e => e.EntityType == nameof(DiscountGrant) && e.FieldName == nameof(DiscountGrant.RevokedEffectiveDate));
            Assert.Equal("policy breach", audit.Reason);
        }

        [Fact]
        [BusinessRule("BR-DIS-008")]
        public async Task Claw_back_posts_a_charge_for_the_forward_fraction_only()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await PostCharge(db, 1000m, categoryId: _transportId);
            var type = await admin.DefineTypeAsync("Neg", "Neg", DiscountBasis.Percentage, DiscountEligibilityMode.Manual);
            var grant = await admin.ProposeManualGrantAsync(_studentId, type.Id, 30.3m, "negotiated", 1);   // 303.00 applied
            await admin.ApproveGrantAsync(grant.Id, 2);
            _clock.UtcNow = new DateTime(2027, 3, 23, 8, 0, 0, DateTimeKind.Utc);   // 100 of 303 days left

            await admin.RevokeGrantAsync(grant.Id, new DateTime(2027, 3, 23), "employment ended", clawBack: true);

            var clawback = db.Charges.OrderByDescending(c => c.Id).First();
            Assert.Equal(2, db.Charges.Count());
            Assert.Equal(100m, clawback.GrossAmount);
        }

        // --- BR-DIS-006 waivers -----------------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-DIS-006")]
        public async Task A_waiver_is_capped_at_the_charge_remainder_and_materializes_as_a_credit_note()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var charge = await PostCharge(db, 200m, categoryId: _transportId);   // a late fee
            await CreatePaymentAdmin(db).CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 50m);

            await Assert.ThrowsAsync<WaiverExceedsChargeRemainderException>(() => admin.ProposeWaiverAsync(charge.Id, WaiverKind.LateFee, 151m, "goodwill", 1));
            var waiver = await admin.ProposeWaiverAsync(charge.Id, WaiverKind.LateFee, 150m, "goodwill", 1);
            Assert.Equal(ApprovalTier.FinanceManager, waiver.RequiredTier);

            await admin.DecideWaiverAsync(waiver.Id, approve: true, decidedByUserId: 2);

            var stored = db.Waivers.Single();
            Assert.Equal(WaiverStatus.Approved, stored.Status);
            Assert.NotNull(stored.CreditNoteId);
            Assert.Equal(150m, db.CreditNotes.Single().Amount);
            Assert.Equal(0m, await CreateFeeAdmin(db).ComputeStudentPositionAsync(_studentId));
            await Assert.ThrowsAsync<WaiverNotPendingException>(() => admin.DecideWaiverAsync(waiver.Id, true, 2));
        }

        // --- BR-DIS-007 renewal ---------------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-DIS-007")]
        public async Task Manual_grants_enter_the_renewal_queue_and_only_a_decision_creates_a_new_year_grant()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await PostCharge(db, 1000m, categoryId: _transportId);
            var manual = await admin.DefineTypeAsync("Neg", "Neg", DiscountBasis.Percentage, DiscountEligibilityMode.Manual);
            var auto = await admin.DefineTypeAsync("Sib", "Sib", DiscountBasis.Percentage, DiscountEligibilityMode.Automatic, renewalMode: DiscountRenewalMode.AutoReevaluate);
            var manualGrant = await admin.ProposeManualGrantAsync(_studentId, manual.Id, 10m, "negotiated", 1);
            var autoGrant = await admin.ProposeManualGrantAsync(_studentId, auto.Id, 10m, "seeded auto grant", 1);
            await admin.ApproveGrantsAsync(new[] { manualGrant.Id, autoGrant.Id }, 2);
            var nextYear = new AcademicYear { LabelAr = "Y", LabelEn = "2027-2028", HijriLabel = "H", StartDate = new DateTime(2027, 9, 1), EndDate = new DateTime(2028, 6, 30), Status = AcademicYearStatus.Preparation };
            db.AcademicYears.Add(nextYear);
            await db.SaveChangesAsync();

            var queue = await admin.BuildRenewalQueueAsync(_yearId, nextYear.Id);

            Assert.Equal(manualGrant.Id, Assert.Single(queue).PriorGrantId);   // the automatic type re-evaluates instead
            Assert.Empty(await admin.BuildRenewalQueueAsync(_yearId, nextYear.Id));

            await admin.DecideRenewalAsync(queue[0].Id, RenewalDecision.Adjusted, 2, adjustedBasisValue: 5m);

            var renewed = db.DiscountGrants.Single(g => g.RenewedFromGrantId == manualGrant.Id);
            Assert.Equal(nextYear.Id, renewed.AcademicYearId);
            Assert.Equal(5m, renewed.BasisValue);
            Assert.Equal(DiscountGrantStatus.Proposed, renewed.Status);
            Assert.Equal(DiscountGrantSource.Renewal, renewed.Source);
            await Assert.ThrowsAsync<RenewalItemNotPendingException>(() => admin.DecideRenewalAsync(queue[0].Id, RenewalDecision.Dropped, 2));
        }

        // --- BR-DIS-010 statements ---------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-DIS-010")]
        public async Task The_payer_statement_shows_gross_discounts_credit_notes_and_payments_separately()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var charge = await PostCharge(db, 1000m, categoryId: _transportId);
            var type = await admin.DefineTypeAsync("Neg", "Neg", DiscountBasis.Percentage, DiscountEligibilityMode.Manual);
            var grant = await admin.ProposeManualGrantAsync(_studentId, type.Id, 10m, "negotiated", 1);
            await admin.ApproveGrantAsync(grant.Id, 2);
            await CreateFeeAdmin(db).IssueCreditNoteAsync(charge.Id, 50m, "correction");
            await CreatePaymentAdmin(db).CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 300m);

            var statement = await CreateStatements(db).BuildAsync(_payerId);

            Assert.Equal(1000m, statement.GrossCharges);
            Assert.Equal(100m, statement.Discounts);
            Assert.Equal(50m, statement.CreditNotes);
            Assert.Equal(300m, statement.Payments);
            Assert.Equal(850m, statement.NetCharges);
            Assert.Equal(550m, statement.ClosingBalance);
            Assert.Equal(4, statement.Lines.Count);
            Assert.Equal(550m, statement.Lines.Last().RunningBalance);
        }

        [Fact]
        [BusinessRule("BR-DIS-010")]
        public async Task A_formal_statement_is_numbered_and_snapshotted()
        {
            using var db = CreateContext();
            await PostCharge(db, 1000m, categoryId: _transportId);

            var issue = await CreateStatements(db).IssueAsync(_payerId);

            Assert.Equal("STM-000001", issue.StatementNo);
            Assert.Equal(1000m, issue.ClosingBalance);
            Assert.Contains("\"GrossCharges\":1000", issue.SnapshotJson);
        }

        // --- BR-DIS-009 register audit -----------------------------------------------------------

        [Fact]
        [BusinessRule("BR-DIS-009")]
        public async Task Grant_and_document_rows_are_T1_audited()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await PostCharge(db, 1000m, categoryId: _transportId);
            var type = await admin.DefineTypeAsync("Neg", "Neg", DiscountBasis.Percentage, DiscountEligibilityMode.Manual);
            var grant = await admin.ProposeManualGrantAsync(_studentId, type.Id, 10m, "negotiated", 1);
            await admin.ApproveGrantAsync(grant.Id, 2);

            Assert.Contains(db.AuditEntries, e => e.EntityType == nameof(DiscountGrant) && e.FieldName == nameof(DiscountGrant.Status));
            Assert.Contains(db.AuditEntries, e => e.EntityType == nameof(DiscountDocument));
        }
    }
}
