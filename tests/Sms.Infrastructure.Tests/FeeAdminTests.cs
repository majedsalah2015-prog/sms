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
    }
}
