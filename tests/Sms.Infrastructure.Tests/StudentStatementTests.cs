using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Statements;
using Sms.Domain.Common;
using Sms.Domain.Discounts;
using Sms.Domain.Fees;
using Sms.Domain.Grades;
using Sms.Domain.Numbering;
using Sms.Domain.Parents;
using Sms.Domain.Payments;
using Sms.Domain.Schools;
using Sms.Domain.Students;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Statements;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// <see cref="StatementService.BuildForStudentAsync"/> — doc/Modules/19 §8.7 read down one child
    /// (BR-FEE-008), with BR-DIS-010's separation intact.
    /// <para>
    /// The case that made this its own method rather than a filter over the payer statement is the
    /// sibling one: a guardian pays once, the engine allocates oldest-first across two children, and
    /// a naive per-student statement built from receipts would credit each child with the whole
    /// payment. Every test below has two children on one payer for that reason.
    /// </para>
    /// </summary>
    public sealed class StudentStatementTests : IDisposable
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
        private int _yearId;
        private int _elderId;
        private int _youngerId;
        private int _payerId;
        private int _categoryId;

        public StudentStatementTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            db.NumberingSeries.Add(new NumberingSeries
            {
                Code = "STM", EntityName = "StatementIssue", FormatTemplate = "STM-{SEQ:5}",
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

            var category = new FeeCategory { NameAr = "رسوم دراسية", NameEn = "Tuition", IsMandatory = true };
            db.FeeCategories.Add(category);
            db.SaveChanges();

            var elder = NewStudent("STU-ELDER", "كبرى", "Elder");
            var younger = NewStudent("STU-YOUNGER", "صغرى", "Younger");
            db.Students.AddRange(elder, younger);

            var parent = new Parent { ParentFileNo = "PAR-000001", NameAr = "ولي أمر", NameEn = "Guardian", PrimaryMobile = "0500000000" };
            db.Parents.Add(parent);
            db.SaveChanges();

            var payer = new Payer { Type = PayerType.Parent, ParentId = parent.Id };
            db.Payers.Add(payer);
            db.SaveChanges();

            _yearId = year.Id;
            _elderId = elder.Id;
            _youngerId = younger.Id;
            _payerId = payer.Id;
            _categoryId = category.Id;
        }

        private static Student NewStudent(string no, string ar, string en) => new()
        {
            StudentNo = no,
            FirstNameAr = ar, FatherNameAr = "أب", GrandfatherNameAr = "جد", FamilyNameAr = "عائلة",
            FirstNameEn = en, FatherNameEn = "Father", GrandfatherNameEn = "Grandfather", FamilyNameEn = "Family",
            Gender = Gender.Female, DateOfBirth = new DateTime(2018, 1, 1), NationalityLookupId = 1,
        };

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private StatementService CreateService(AppDbContext db) => new(db, new NumberIssuer(db, _tenant, _tenant, _clock), _clock);

        private Charge PostCharge(AppDbContext db, int studentId, string no, decimal gross, DateTime postedAt)
        {
            var charge = new Charge
            {
                AcademicYearId = _yearId, StudentId = studentId, PayerId = _payerId, FeeCategoryId = _categoryId,
                SourceType = ChargeSourceType.Registration, ChargeNo = no,
                NetAmount = gross, VatAmount = 0m, GrossAmount = gross,
                Status = ChargeStatus.Posted, PostedAtUtc = postedAt, InvoiceUuid = Guid.NewGuid(),
            };
            db.Charges.Add(charge);
            db.SaveChanges();
            return charge;
        }

        private Receipt Receipt(AppDbContext db, string no, decimal amount, DateTime issuedAt)
        {
            var receipt = new Receipt
            {
                PayerId = _payerId, ReceiptNo = no, Method = PaymentMethod.Cash, Amount = amount,
                Status = ReceiptStatus.Posted, Purpose = ReceiptPurpose.FeePayment, IssuedAtUtc = issuedAt,
            };
            db.Receipts.Add(receipt);
            db.SaveChanges();
            return receipt;
        }

        // --- BR-FEE-008: the per-student position is one subtraction ------------------

        /// <summary>
        /// The sibling case. One receipt of 1,200 is allocated 1,000 to the elder child and 200 to
        /// the younger; each statement must credit only its own share. Built from receipts instead
        /// of allocations, both children would read as having paid 1,200.
        /// </summary>
        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task A_shared_receipt_credits_each_child_only_with_what_was_allocated_to_them()
        {
            using var db = CreateContext();
            var elderCharge = PostCharge(db, _elderId, "INV-1", 1000m, new DateTime(2026, 9, 1));
            var youngerCharge = PostCharge(db, _youngerId, "INV-2", 800m, new DateTime(2026, 9, 2));
            var receipt = Receipt(db, "RCP-1", 1200m, new DateTime(2026, 10, 1));
            db.PaymentAllocations.AddRange(
                new PaymentAllocation { ReceiptId = receipt.Id, ChargeId = elderCharge.Id, AllocatedAmount = 1000m },
                new PaymentAllocation { ReceiptId = receipt.Id, ChargeId = youngerCharge.Id, AllocatedAmount = 200m });
            db.SaveChanges();
            var service = CreateService(db);

            var elder = await service.BuildForStudentAsync(_elderId);
            var younger = await service.BuildForStudentAsync(_youngerId);

            Assert.Equal(1000m, elder.Payments);
            Assert.Equal(0m, elder.ClosingBalance);
            Assert.Equal(200m, younger.Payments);
            Assert.Equal(600m, younger.ClosingBalance);
        }

        /// <summary>
        /// A child's statement holds that child's documents and no sibling's — the filter is on the
        /// charge's student, not on the payer the two share.
        /// </summary>
        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task A_siblings_invoice_never_appears_on_the_other_childs_statement()
        {
            using var db = CreateContext();
            PostCharge(db, _elderId, "INV-1", 1000m, new DateTime(2026, 9, 1));
            PostCharge(db, _youngerId, "INV-2", 800m, new DateTime(2026, 9, 2));
            var service = CreateService(db);

            var elder = await service.BuildForStudentAsync(_elderId);

            Assert.Equal(1000m, elder.GrossCharges);
            Assert.All(elder.Lines, l => Assert.NotEqual("INV-2", l.DocumentNo));
        }

        /// <summary>
        /// A receipt the cashier has taken but not yet allocated is family money: BR-PAY-003 has not
        /// yet said which child it paid for, so it belongs on neither statement. It is visible as the
        /// payer's advance balance instead.
        /// </summary>
        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task An_unallocated_receipt_appears_on_no_childs_statement()
        {
            using var db = CreateContext();
            PostCharge(db, _elderId, "INV-1", 1000m, new DateTime(2026, 9, 1));
            Receipt(db, "RCP-1", 500m, new DateTime(2026, 10, 1));
            var service = CreateService(db);

            var elder = await service.BuildForStudentAsync(_elderId);

            Assert.Equal(0m, elder.Payments);
            Assert.Equal(1000m, elder.ClosingBalance);
        }

        /// <summary>A voided receipt's allocation is not a payment, and must not credit the child.</summary>
        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task An_allocation_of_a_voided_receipt_is_not_counted()
        {
            using var db = CreateContext();
            var charge = PostCharge(db, _elderId, "INV-1", 1000m, new DateTime(2026, 9, 1));
            var receipt = Receipt(db, "RCP-1", 400m, new DateTime(2026, 10, 1));
            receipt.Status = ReceiptStatus.Void;
            db.PaymentAllocations.Add(new PaymentAllocation { ReceiptId = receipt.Id, ChargeId = charge.Id, AllocatedAmount = 400m });
            db.SaveChanges();
            var service = CreateService(db);

            var elder = await service.BuildForStudentAsync(_elderId);

            Assert.Equal(0m, elder.Payments);
            Assert.Equal(1000m, elder.ClosingBalance);
        }

        /// <summary>A void charge was never owed, so it is not on the statement either.</summary>
        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task A_void_charge_is_not_on_the_statement()
        {
            using var db = CreateContext();
            PostCharge(db, _elderId, "INV-1", 1000m, new DateTime(2026, 9, 1));
            var voided = PostCharge(db, _elderId, "INV-2", 300m, new DateTime(2026, 9, 5));
            voided.Status = ChargeStatus.Void;
            db.SaveChanges();
            var service = CreateService(db);

            var elder = await service.BuildForStudentAsync(_elderId);

            Assert.Equal(1000m, elder.GrossCharges);
        }

        // --- BR-DIS-010: gross, discounts and net never collapse into one figure -------

        /// <summary>
        /// The rule the finance screens exist to keep visible: a family that was billed 1,000 and
        /// granted 250 must be able to read all three of gross, discount and net — not a single
        /// "750" that hides whether the school ever charged full price.
        /// </summary>
        [Fact]
        [BusinessRule("BR-DIS-010")]
        public async Task Gross_discounts_and_net_stay_three_separate_figures()
        {
            using var db = CreateContext();
            var charge = PostCharge(db, _elderId, "INV-1", 1000m, new DateTime(2026, 9, 1));
            var type = new DiscountType { NameAr = "خصم إخوة", NameEn = "Sibling discount", Basis = DiscountBasis.Percentage };
            db.DiscountTypes.Add(type);
            db.SaveChanges();
            var grant = new DiscountGrant
            {
                AcademicYearId = _yearId, StudentId = _elderId, DiscountTypeId = type.Id,
                Source = DiscountGrantSource.Manual, BasisValue = 25m, Status = DiscountGrantStatus.Approved,
                Reason = "أخوة", ProposedByUserId = 1, AppliedAmount = 250m,
            };
            db.DiscountGrants.Add(grant);
            db.SaveChanges();
            db.DiscountDocuments.Add(new DiscountDocument
            {
                DiscountGrantId = grant.Id, ChargeId = charge.Id, DocumentNo = "DSC-1",
                Amount = 250m, IssuedAtUtc = new DateTime(2026, 9, 10),
            });
            db.CreditNotes.Add(new CreditNote
            {
                ChargeId = charge.Id, CreditNoteNo = "CRN-1", Amount = 100m,
                Reason = "تصحيح", IssuedAtUtc = new DateTime(2026, 9, 15),
            });
            db.SaveChanges();
            var service = CreateService(db);

            var statement = await service.BuildForStudentAsync(_elderId);

            Assert.Equal(1000m, statement.GrossCharges);
            Assert.Equal(250m, statement.Discounts);
            Assert.Equal(100m, statement.CreditNotes);
            Assert.Equal(650m, statement.NetCharges);
            Assert.Equal(650m, statement.ClosingBalance);
        }

        // --- BR-GLB-064: as-of any date ------------------------------------------------

        /// <summary>
        /// The statement rebuilds as it stood at the end of a chosen day — a payment made after it
        /// must not appear, or a printed statement could never be reconciled against the day it
        /// claims to describe.
        /// </summary>
        [Fact]
        [BusinessRule("BR-GLB-064")]
        public async Task An_as_of_date_excludes_everything_after_it()
        {
            using var db = CreateContext();
            var charge = PostCharge(db, _elderId, "INV-1", 1000m, new DateTime(2026, 9, 1));
            var receipt = Receipt(db, "RCP-1", 400m, new DateTime(2026, 11, 20));
            db.PaymentAllocations.Add(new PaymentAllocation { ReceiptId = receipt.Id, ChargeId = charge.Id, AllocatedAmount = 400m });
            db.SaveChanges();
            var service = CreateService(db);

            var before = await service.BuildForStudentAsync(_elderId, new DateTime(2026, 10, 31, 23, 59, 59, DateTimeKind.Utc));
            var after = await service.BuildForStudentAsync(_elderId);

            Assert.Equal(0m, before.Payments);
            Assert.Equal(1000m, before.ClosingBalance);
            Assert.Equal(400m, after.Payments);
            Assert.Equal(600m, after.ClosingBalance);
        }

        /// <summary>The running balance walks the documents in date order, so the last line is the closing balance.</summary>
        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task The_running_balance_ends_at_the_closing_balance()
        {
            using var db = CreateContext();
            var first = PostCharge(db, _elderId, "INV-1", 1000m, new DateTime(2026, 9, 1));
            PostCharge(db, _elderId, "INV-2", 500m, new DateTime(2026, 10, 1));
            var receipt = Receipt(db, "RCP-1", 600m, new DateTime(2026, 11, 1));
            db.PaymentAllocations.Add(new PaymentAllocation { ReceiptId = receipt.Id, ChargeId = first.Id, AllocatedAmount = 600m });
            db.SaveChanges();
            var service = CreateService(db);

            var statement = await service.BuildForStudentAsync(_elderId);

            Assert.Equal(3, statement.Lines.Count);
            Assert.Equal(900m, statement.Lines.Last().RunningBalance);
            Assert.Equal(900m, statement.ClosingBalance);
        }

        /// <summary>A child with nothing billed gets an empty statement, not a failure.</summary>
        [Fact]
        public async Task A_student_with_no_documents_gets_an_empty_statement()
        {
            using var db = CreateContext();
            var service = CreateService(db);

            var statement = await service.BuildForStudentAsync(_youngerId);

            Assert.Empty(statement.Lines);
            Assert.Equal(0m, statement.ClosingBalance);
        }
    }
}
