using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Common;
using Sms.Domain.Fees;
using Sms.Domain.Grades;
using Sms.Domain.Numbering;
using Sms.Domain.Parents;
using Sms.Domain.Schools;
using Sms.Domain.Students;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Fees;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// S3/E-303 (slice: Fees, doc/Modules/19, BR-FEE-001/002/003/005/008)
    /// over a real Sqlite-backed AppDbContext, including E-006's real
    /// INumberIssuer (the "INV"/"CRN" series).
    /// </summary>
    public sealed class FeeAdminTests : IDisposable
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

            public int AcademicYearId => 2027;
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();
        private int _profileId;
        private int _studentId;
        private int _payerId;

        public FeeAdminTests()
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
            db.NumberingSeries.Add(new NumberingSeries
            {
                Code = "CRN", EntityName = "CreditNote", FormatTemplate = "CRN-{SEQ:5}",
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

            var grade = new GradeLevel { StageId = stage.Id, Code = "G3", Name = new LocalizedName("ثالث", "Grade 3"), SequenceOrder = 3 };
            db.GradeLevels.Add(grade);
            db.SaveChanges();

            var profile = new GradeYearProfile { GradeLevelId = grade.Id, AcademicYearId = year.Id, GenderPolicy = GenderPolicy.Mixed, TargetSections = 1, TargetSectionSize = 25 };
            db.GradeYearProfiles.Add(profile);
            db.SaveChanges();

            var student = new Student
            {
                StudentNo = "STU-TEST-1",
                FirstNameAr = "طالب", FatherNameAr = "أب", GrandfatherNameAr = "جد", FamilyNameAr = "عائلة",
                FirstNameEn = "Student", FatherNameEn = "Father", GrandfatherNameEn = "Grandfather", FamilyNameEn = "Family",
                Gender = Gender.Male, DateOfBirth = new DateTime(2018, 1, 1), NationalityLookupId = 1,
            };
            db.Students.Add(student);
            db.SaveChanges();

            db.Enrollments.Add(new Enrollment
            {
                AcademicYearId = year.Id, StudentId = student.Id, GradeYearProfileId = profile.Id,
                EnrollmentDate = new DateTime(2026, 9, 1), SourceType = EnrollmentSourceType.Admission,
            });

            var parent = new Parent { ParentFileNo = "PAR-000001", NameAr = "ولي أمر", NameEn = "Guardian", PrimaryMobile = "0500000000" };
            db.Parents.Add(parent);
            db.SaveChanges();

            var payer = new Payer { Type = PayerType.Parent, ParentId = parent.Id };
            db.Payers.Add(payer);
            db.SaveChanges();

            _profileId = profile.Id;
            _studentId = student.Id;
            _payerId = payer.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private FeeAdmin CreateAdmin(AppDbContext db) => new(db, new NumberIssuer(db, _tenant, _tenant, _clock), _clock);

        private async Task<int> DefineApprovedLine(FeeAdmin admin, int categoryId, decimal amount = 1000m)
        {
            var line = await admin.DefineStructureLineAsync(_profileId, categoryId, amount);
            await admin.ApproveStructureLineAsync(line.Id);
            return line.Id;
        }

        // --- BR-FEE-001/BR-GLB-061 categories + VAT --------------------------------

        [Fact]
        [BusinessRule("BR-FEE-001")]
        public async Task Defining_a_category_persists_its_vat_rate()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            var category = await admin.DefineCategoryAsync("رسوم دراسية", "Tuition", 0.15m, isMandatory: true, isRefundable: false, isServiceLinked: false);

            Assert.Equal(0.15m, db.FeeCategories.Single(c => c.Id == category.Id).VatRate);
        }

        // --- BR-FEE-002 structure lines ---------------------------------------------

        [Fact]
        [BusinessRule("BR-FEE-002")]
        public async Task Approving_a_line_along_an_illegal_path_is_rejected()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var category = await admin.DefineCategoryAsync("رسوم دراسية", "Tuition", null, true, false, false);
            var line = await admin.DefineStructureLineAsync(_profileId, category.Id, 1000m);
            await admin.ApproveStructureLineAsync(line.Id);

            await Assert.ThrowsAsync<InvalidFeeStructureLineStatusTransitionException>(() => admin.ApproveStructureLineAsync(line.Id));
        }

        // --- BR-FEE-003/005 charges --------------------------------------------------

        [Fact]
        [BusinessRule("BR-FEE-003")]
        public async Task Posting_a_charge_without_an_approved_line_is_rejected()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var category = await admin.DefineCategoryAsync("رسوم دراسية", "Tuition", null, true, false, false);

            await Assert.ThrowsAsync<FeeStructureLineNotApprovedException>(() =>
                admin.PostChargeAsync(_studentId, _payerId, _profileId, category.Id, ChargeSourceType.Registration));
        }

        [Fact]
        [BusinessRule("BR-FEE-005")]
        public async Task Posting_a_charge_computes_vat_and_issues_a_real_invoice_number()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var category = await admin.DefineCategoryAsync("رسوم دراسية", "Tuition", 0.15m, true, false, false);
            await DefineApprovedLine(admin, category.Id, 1000m);

            var charge = await admin.PostChargeAsync(_studentId, _payerId, _profileId, category.Id, ChargeSourceType.Registration);

            Assert.Equal("INV-000001", charge.ChargeNo);
            Assert.Equal(1000m, charge.NetAmount);
            Assert.Equal(150m, charge.VatAmount);
            Assert.Equal(1150m, charge.GrossAmount);
            Assert.NotEqual(Guid.Empty, charge.InvoiceUuid);
            Assert.NotNull(charge.InvoiceHash);
            Assert.Null(charge.PreviousInvoiceHash);
        }

        [Fact]
        [BusinessRule("BR-FEE-005")]
        public async Task Successive_charges_chain_their_invoice_hashes()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var category = await admin.DefineCategoryAsync("رسوم دراسية", "Tuition", null, true, false, false);
            await DefineApprovedLine(admin, category.Id);

            var first = await admin.PostChargeAsync(_studentId, _payerId, _profileId, category.Id, ChargeSourceType.Registration);
            var second = await admin.PostManualChargeAsync(_studentId, _payerId, category.Id, 200m);

            Assert.Equal(first.InvoiceHash, second.PreviousInvoiceHash);
        }

        // --- BR-GLB-062/doc §9 credit notes -----------------------------------------

        [Fact]
        [BusinessRule("BR-FEE-003")]
        public async Task A_credit_note_exceeding_the_charge_value_is_rejected()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var category = await admin.DefineCategoryAsync("رسوم دراسية", "Tuition", null, true, true, false);
            await DefineApprovedLine(admin, category.Id, 1000m);
            var charge = await admin.PostChargeAsync(_studentId, _payerId, _profileId, category.Id, ChargeSourceType.Registration);

            await Assert.ThrowsAsync<CreditNoteExceedsChargeException>(() =>
                admin.IssueCreditNoteAsync(charge.Id, 1000.01m, "Withdrawal"));
        }

        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task Student_position_reflects_a_posted_charge_reduced_by_its_credit_note()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var category = await admin.DefineCategoryAsync("رسوم دراسية", "Tuition", null, true, true, false);
            await DefineApprovedLine(admin, category.Id, 1000m);
            var charge = await admin.PostChargeAsync(_studentId, _payerId, _profileId, category.Id, ChargeSourceType.Registration);
            await admin.IssueCreditNoteAsync(charge.Id, 300m, "Partial waiver");

            var position = await admin.ComputeStudentPositionAsync(_studentId);

            Assert.Equal(700m, position);
        }

        // --- E-303 screens: catalog edit/deactivate, draft-line edit/delete, copy-from-year, payer materialization ---

        [Fact]
        [BusinessRule("BR-FEE-001")]
        public async Task A_category_referenced_by_a_structure_line_cannot_be_deactivated_but_an_unused_one_can()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var used = await admin.DefineCategoryAsync("رسوم دراسية", "Tuition", null, true, false, false);
            var unused = await admin.DefineCategoryAsync("نقل", "Transport", 0.15m, false, true, true);
            await admin.DefineStructureLineAsync(_profileId, used.Id, 1000m);

            await Assert.ThrowsAsync<FeeCategoryInUseException>(() => admin.DeactivateCategoryAsync(used.Id));
            await admin.UpdateCategoryAsync(unused.Id, "حافلات", "Bus", 0.15m, false, true, true, "4100");
            await admin.DeactivateCategoryAsync(unused.Id);

            var row = db.FeeCategories.IgnoreQueryFilters().Single(c => c.Id == unused.Id);
            Assert.False(row.IsActive);
            Assert.Equal("Bus", row.NameEn);
            Assert.Equal("4100", row.GlExportCode);
        }

        [Fact]
        [BusinessRule("BR-FEE-002")]
        public async Task Only_a_draft_line_can_be_edited_or_deleted_and_a_pair_cannot_be_defined_twice()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var category = await admin.DefineCategoryAsync("رسوم دراسية", "Tuition", null, true, false, false);
            var line = await admin.DefineStructureLineAsync(_profileId, category.Id, 1000m);

            await Assert.ThrowsAsync<FeeStructureLineAlreadyExistsException>(() => admin.DefineStructureLineAsync(_profileId, category.Id, 900m));
            _audit.Reason = "Board revised tuition";
            await admin.UpdateStructureLineAsync(line.Id, 1200m);
            Assert.Equal(1200m, db.FeeStructureLines.Single(l => l.Id == line.Id).Amount);

            await admin.ApproveStructureLineAsync(line.Id);
            await Assert.ThrowsAsync<FeeStructureLineNotDraftException>(() => admin.UpdateStructureLineAsync(line.Id, 1300m));
            await Assert.ThrowsAsync<FeeStructureLineNotDraftException>(() => admin.DeleteStructureLineAsync(line.Id));

            var draft = await admin.DefineStructureLineAsync(_profileId, (await admin.DefineCategoryAsync("كتب", "Books", null, false, false, false)).Id, 100m);
            await admin.DeleteStructureLineAsync(draft.Id);
            Assert.False(db.FeeStructureLines.Any(l => l.Id == draft.Id));
        }

        /// <summary>
        /// Before this an approved line had no exit: the amount is immutable
        /// (BR-FEE-002), the delete path is draft-only, and the only transition was
        /// Draft → Approved — so a price approved against the wrong grade stayed in
        /// the list for good. Withdrawing is the exit, and it is not a delete: the row
        /// and its figure stay readable (BR-GLB-005).
        /// </summary>
        [Fact]
        [BusinessRule("BR-FEE-002")]
        public async Task An_approved_price_is_withdrawn_rather_than_deleted_and_stops_billing()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var category = await admin.DefineCategoryAsync("أوراق امتحان", "Exam papers", null, false, false, false);
            var lineId = await DefineApprovedLine(admin, category.Id, 12500m);

            await admin.WithdrawStructureLineAsync(lineId, "أُقرّت على الصف الخطأ.");

            var stored = db.FeeStructureLines.Single(l => l.Id == lineId);
            Assert.Equal(FeeStructureLineStatus.Withdrawn, stored.Status);
            Assert.Equal(12500m, stored.Amount);

            // PostChargeAsync reads approved lines only, so withdrawing stops it billing
            // without any further wiring — the point of using the status rather than a flag.
            await Assert.ThrowsAsync<FeeStructureLineNotApprovedException>(
                () => admin.PostChargeAsync(_studentId, _payerId, _profileId, category.Id, ChargeSourceType.Registration));
        }

        [Fact]
        [BusinessRule("BR-FEE-002")]
        public async Task Withdrawing_needs_a_reason_and_is_refused_from_draft()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var category = await admin.DefineCategoryAsync("نقل", "Transport", null, false, false, false);
            var draft = await admin.DefineStructureLineAsync(_profileId, category.Id, 500m);

            // A draft is deleted, not withdrawn — there is nothing to keep on the record.
            await Assert.ThrowsAsync<InvalidFeeStructureLineStatusTransitionException>(
                () => admin.WithdrawStructureLineAsync(draft.Id, "لا داعي له."));

            await admin.ApproveStructureLineAsync(draft.Id);
            await Assert.ThrowsAsync<InvalidOperationException>(() => admin.WithdrawStructureLineAsync(draft.Id, "   "));
        }

        /// <summary>
        /// Once a price has billed somebody it is not a plan any more. Removing it from
        /// the list would leave those invoices with nothing explaining what they were for.
        /// </summary>
        [Fact]
        [BusinessRule("BR-GLB-004")]
        public async Task A_price_that_has_already_been_charged_cannot_be_withdrawn()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var category = await admin.DefineCategoryAsync("رسوم دراسية", "Tuition", null, true, false, false);
            var lineId = await DefineApprovedLine(admin, category.Id, 12000m);
            await admin.PostChargeAsync(_studentId, _payerId, _profileId, category.Id, ChargeSourceType.Registration);

            await Assert.ThrowsAsync<FeeStructureLineInUseException>(
                () => admin.WithdrawStructureLineAsync(lineId, "غيّرنا رأينا."));

            Assert.Equal(FeeStructureLineStatus.Approved, db.FeeStructureLines.Single(l => l.Id == lineId).Status);
        }

        [Fact]
        [BusinessRule("BR-FEE-002")]
        public async Task Copying_a_structure_to_the_next_year_creates_uplifted_draft_lines_once_per_pair()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var category = await admin.DefineCategoryAsync("رسوم دراسية", "Tuition", null, true, false, false);
            await DefineApprovedLine(admin, category.Id, 1000m);
            var sourceYearId = db.GradeYearProfiles.Single(p => p.Id == _profileId).AcademicYearId;
            var gradeLevelId = db.GradeYearProfiles.Single(p => p.Id == _profileId).GradeLevelId;

            var nextYear = new AcademicYear
            {
                LabelAr = "٢٠٢٧-٢٠٢٨", LabelEn = "2027-2028", HijriLabel = "١٤٤٩هـ",
                StartDate = new DateTime(2027, 9, 1), EndDate = new DateTime(2028, 6, 30), Status = AcademicYearStatus.Preparation,
            };
            db.AcademicYears.Add(nextYear);
            await db.SaveChangesAsync();
            var nextProfile = new GradeYearProfile { GradeLevelId = gradeLevelId, AcademicYearId = nextYear.Id, GenderPolicy = GenderPolicy.Mixed, TargetSections = 1, TargetSectionSize = 25 };
            db.GradeYearProfiles.Add(nextProfile);
            await db.SaveChangesAsync();

            var created = await admin.CopyStructureAsync(sourceYearId, nextYear.Id, 5m);
            var again = await admin.CopyStructureAsync(sourceYearId, nextYear.Id, 5m);

            Assert.Equal(1, created);
            Assert.Equal(0, again);
            var copied = db.FeeStructureLines.Single(l => l.GradeYearProfileId == nextProfile.Id);
            Assert.Equal(1050m, copied.Amount);
            Assert.Equal(FeeStructureLineStatus.Draft, copied.Status);
            Assert.Equal(nextYear.Id, copied.AcademicYearId);
        }

        // --- BR-GLB-062 void ("delete" on the charge explorer) ----------------------

        [Fact]
        [BusinessRule("BR-GLB-062")]
        public async Task Voiding_an_untouched_charge_marks_it_void_and_removes_it_from_the_position()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var category = await admin.DefineCategoryAsync("رسوم دراسية", "Tuition", null, true, false, false);
            var kept = await admin.PostManualChargeAsync(_studentId, _payerId, category.Id, 300m);
            var voided = await admin.PostManualChargeAsync(_studentId, _payerId, category.Id, 200m);

            _audit.Reason = "Posted against the wrong student";
            await admin.VoidChargeAsync(voided.Id);

            Assert.Equal(ChargeStatus.Void, (await db.Charges.AsNoTracking().SingleAsync(c => c.Id == voided.Id)).Status);
            Assert.Equal(kept.GrossAmount, await admin.ComputeStudentPositionAsync(_studentId));
        }

        [Fact]
        [BusinessRule("BR-GLB-062")]
        public async Task A_charge_with_a_credit_note_cannot_be_voided()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var category = await admin.DefineCategoryAsync("رسوم دراسية", "Tuition", null, true, true, false);
            var charge = await admin.PostManualChargeAsync(_studentId, _payerId, category.Id, 500m);
            await admin.IssueCreditNoteAsync(charge.Id, 100m, "Partial correction");

            await Assert.ThrowsAsync<ChargeHasActivityException>(() => admin.VoidChargeAsync(charge.Id));
        }

        [Fact]
        [BusinessRule("BR-GLB-062")]
        public async Task A_void_charge_cannot_be_voided_again()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var category = await admin.DefineCategoryAsync("رسوم دراسية", "Tuition", null, true, false, false);
            var charge = await admin.PostManualChargeAsync(_studentId, _payerId, category.Id, 200m);
            await admin.VoidChargeAsync(charge.Id);

            await Assert.ThrowsAsync<ChargeNotPostedException>(() => admin.VoidChargeAsync(charge.Id));
        }

        [Fact]
        public async Task Ensuring_a_payer_for_a_parent_is_idempotent()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var parent = new Parent { ParentFileNo = "PAR-000002", NameAr = "ولي", NameEn = "Guardian 2", PrimaryMobile = "0500000001" };
            db.Parents.Add(parent);
            await db.SaveChangesAsync();

            var first = await admin.EnsurePayerForParentAsync(parent.Id);
            var second = await admin.EnsurePayerForParentAsync(parent.Id);

            Assert.Equal(first.Id, second.Id);
            Assert.Equal(1, db.Payers.Count(p => p.ParentId == parent.Id));
        }
    }
}
