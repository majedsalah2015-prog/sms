using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Cafeteria;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Store;
using Sms.Domain.Cafeteria;
using Sms.Domain.Common;
using Sms.Domain.Fees;
using Sms.Domain.Grades;
using Sms.Domain.Numbering;
using Sms.Domain.Parents;
using Sms.Domain.Payments;
using Sms.Domain.Schools;
using Sms.Domain.Store;
using Sms.Domain.Students;
using Sms.Infrastructure.Attendance;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Cafeteria;
using Sms.Infrastructure.Fees;
using Sms.Infrastructure.Health;
using Sms.Infrastructure.Notifications;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Payments;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Store;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>S6/E-606 (School Store, doc/Modules/28, BR-STO-001..008) over a real Sqlite-backed AppDbContext with E-303/E-605 money paths.</summary>
    public sealed class StoreAdminTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
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
        private int _uniformCategoryId;
        private int _bookCategoryId;

        public StoreAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();
            foreach (var (code, template) in new[] { ("INV", "INV-{SEQ:6}"), ("RCP", "RCP-{SEQ:6}"), ("CRN", "CRN-{SEQ:5}") })
            {
                db.NumberingSeries.Add(new NumberingSeries { Code = code, EntityName = code, FormatTemplate = template, ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Strict, EffectiveFromUtc = _clock.UtcNow, IsActive = true });
            }

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
            var parent = new Parent { ParentFileNo = "PAR-000001", NameAr = "Guardian", NameEn = "Guardian", PrimaryMobile = "0500000000" };
            db.Parents.Add(parent);
            db.SaveChanges();
            var payer = new Payer { Type = PayerType.Parent, ParentId = parent.Id };
            db.Payers.Add(payer);
            var uniforms = new FeeCategory { NameAr = "Uniform", NameEn = "Uniforms", IsServiceLinked = true, IsRefundable = true, VatRate = 0.15m };
            var books = new FeeCategory { NameAr = "Books", NameEn = "Books", IsServiceLinked = true, IsRefundable = true };
            db.FeeCategories.AddRange(uniforms, books);
            db.SaveChanges();

            _yearId = year.Id;
            _profileId = profile.Id;
            _parentId = parent.Id;
            _payerId = payer.Id;
            _uniformCategoryId = uniforms.Id;
            _bookCategoryId = books.Id;
            _studentId = EnrollChild(db, "STU-1");
        }

        public void Dispose() => _connection.Dispose();

        private int EnrollChild(AppDbContext db, string no)
        {
            var student = new Student
            {
                StudentNo = no, FirstNameAr = "S", FatherNameAr = "F", GrandfatherNameAr = "G", FamilyNameAr = "Fam",
                FirstNameEn = "S", FatherNameEn = "F", GrandfatherNameEn = "G", FamilyNameEn = "Fam", Gender = Gender.Male, DateOfBirth = new DateTime(2018, 1, 1), NationalityLookupId = 1,
            };
            db.Students.Add(student);
            db.SaveChanges();
            db.Enrollments.Add(new Enrollment { AcademicYearId = _yearId, StudentId = student.Id, GradeYearProfileId = _profileId, EnrollmentDate = new DateTime(2026, 9, 1), SourceType = EnrollmentSourceType.Admission });
            db.StudentGuardianLinks.Add(new StudentGuardianLink { StudentId = student.Id, ParentId = _parentId, RelationshipLookupId = 1, IsPrimaryContact = true, IsFinanciallyResponsible = true, EffectiveFromUtc = new DateTime(2026, 9, 1) });
            db.SaveChanges();
            return student.Id;
        }

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private NumberIssuer Issuer(AppDbContext db) => new(db, _tenant, _tenant, _clock);

        private FeeAdmin Fees(AppDbContext db) => new(db, Issuer(db), _clock);

        private StoreAdmin CreateAdmin(AppDbContext db) => new(db, Issuer(db), _clock, _audit, _tenant, Fees(db));

        private CafeteriaAdmin Cafeteria(AppDbContext db)
        {
            var events = new AuditEventWriter(db, _tenant, _tenant, _user, _clock, _audit);
            return new CafeteriaAdmin(db, Issuer(db), _clock, _audit, events, Fees(db), new HealthAdmin(db, Issuer(db), _clock, _tenant, events, new NotificationPublisher(db, new TestAddressBook()), new AttendanceAdmin(db)));
        }

        private async Task<(StoreItem Shirt, StoreVariant Small, StoreVariant Medium)> ShirtAsync(StoreAdmin admin, decimal price = 100m, int stock = 5)
        {
            var shirt = await admin.DefineItemAsync("قميص", "Shirt", StoreItemCategory.Uniform, _uniformCategoryId, new[] { new VariantInput("SH-S", Size: "S", LowStockThreshold: 2), new VariantInput("SH-M", Size: "M", LowStockThreshold: 2) });
            await admin.PublishPriceListAsync(new DateTime(2026, 9, 1), new[] { (shirt.Id, price) });
            var small = shirt.Variants.Single(v => v.Sku == "SH-S");
            var medium = shirt.Variants.Single(v => v.Sku == "SH-M");
            await admin.ReceiveStockAsync(small.Id, stock);
            await admin.ReceiveStockAsync(medium.Id, stock);
            return (shirt, small, medium);
        }

        // --- BR-STO-001/008 catalog + prices --------------------------------------------------------

        [Fact]
        [BusinessRule("BR-STO-008")]
        public async Task Sales_price_from_the_latest_effective_list_and_refuse_unpriced_items()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (shirt, small, _) = await ShirtAsync(admin, price: 100m);
            await admin.PublishPriceListAsync(new DateTime(2026, 10, 1), new[] { (shirt.Id, 110m) });
            var till = await new PaymentAdmin(db, Issuer(db), _clock).OpenTillSessionAsync(3, "STORE", 0m);

            var sale = await admin.RecordSaleAsync(_payerId, new[] { new StoreBasketLine(small.Id, 1) }, StoreTender.Cash, 3, _studentId, till.Id);
            Assert.Equal(110m, sale.Total);

            var unpriced = await admin.DefineItemAsync("قلم", "Pen", StoreItemCategory.Stationery, _bookCategoryId, new[] { new VariantInput("PEN") });
            await admin.ReceiveStockAsync(unpriced.Variants[0].Id, 5);
            await Assert.ThrowsAsync<StorePriceMissingException>(() => admin.RecordSaleAsync(_payerId, new[] { new StoreBasketLine(unpriced.Variants[0].Id, 1) }, StoreTender.Cash, 3, _studentId, till.Id));
        }

        // --- BR-STO-003 tenders -------------------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-STO-003")]
        public async Task A_cash_sale_is_a_charge_plus_a_receipt_allocated_to_it_and_needs_an_open_till()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (_, small, _) = await ShirtAsync(admin);
            var payments = new PaymentAdmin(db, Issuer(db), _clock);
            await Fees(db).PostManualChargeAsync(_studentId, _payerId, _bookCategoryId, 500m);   // older open fee charge must NOT absorb the store cash

            await Assert.ThrowsAsync<StoreTenderRejectedException>(() => admin.RecordSaleAsync(_payerId, new[] { new StoreBasketLine(small.Id, 1) }, StoreTender.Cash, 3, _studentId));
            var till = await payments.OpenTillSessionAsync(3, "STORE", 0m);
            var sale = await admin.RecordSaleAsync(_payerId, new[] { new StoreBasketLine(small.Id, 1) }, StoreTender.Cash, 3, _studentId, till.Id);

            var charge = db.Charges.Single(c => c.Id == sale.ChargeId);
            Assert.Equal(115m, charge.GrossAmount);   // 100 + 15% VAT from the uniform fee category
            Assert.Equal(_uniformCategoryId, charge.FeeCategoryId);
            var receipt = db.Receipts.Single(r => r.Id == sale.ReceiptId);
            Assert.Equal(ReceiptPurpose.FeePayment, receipt.Purpose);
            Assert.Equal(charge.Id, db.PaymentAllocations.Single(a => a.ReceiptId == receipt.Id).ChargeId);
            Assert.Equal(500m, await Fees(db).ComputeStudentPositionAsync(_studentId));   // only the fee charge stays open
            Assert.Equal(4, await admin.StockLevelAsync(small.Id));
        }

        [Fact]
        [BusinessRule("BR-STO-003")]
        public async Task Account_charge_is_category_and_cap_gated_with_a_finance_override()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (_, small, medium) = await ShirtAsync(admin);

            await Assert.ThrowsAsync<AccountChargeNotAllowedException>(() => admin.RecordSaleAsync(_payerId, new[] { new StoreBasketLine(small.Id, 1) }, StoreTender.AccountCharge, 3, _studentId));
            await admin.SetAccountChargePolicyAsync(StoreItemCategory.Uniform, isAllowed: true, capPerSale: 150m);
            var within = await admin.RecordSaleAsync(_payerId, new[] { new StoreBasketLine(small.Id, 1) }, StoreTender.AccountCharge, 3, _studentId);
            Assert.NotNull(within.ChargeId);
            Assert.Null(within.ReceiptId);
            await Assert.ThrowsAsync<AccountChargeNotAllowedException>(() => admin.RecordSaleAsync(_payerId, new[] { new StoreBasketLine(medium.Id, 2) }, StoreTender.AccountCharge, 3, _studentId));
            var overridden = await admin.RecordSaleAsync(_payerId, new[] { new StoreBasketLine(medium.Id, 2) }, StoreTender.AccountCharge, 3, _studentId, financeOverrideReason: "finance approved");
            Assert.Equal("finance approved", overridden.FinanceOverrideReason);
            Assert.Equal(345m, await Fees(db).ComputeStudentPositionAsync(_studentId));   // (100 + 200) * 1.15 open on the account
        }

        [Fact]
        [BusinessRule("BR-STO-003")]
        public async Task Wallet_tender_debits_the_cafeteria_ledger()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var cafeteria = Cafeteria(db);
            var (_, small, _) = await ShirtAsync(admin);
            var wallet = await cafeteria.EnsureWalletAsync(WalletHolderKind.Student, _studentId);
            await cafeteria.TopUpAsync(wallet.Id, _payerId, PaymentMethod.Cash, 150m);

            await Assert.ThrowsAsync<StoreTenderRejectedException>(() => admin.RecordSaleAsync(_payerId, new[] { new StoreBasketLine(small.Id, 1) }, StoreTender.Wallet, 3, _studentId, allowWalletTender: false));
            var sale = await admin.RecordSaleAsync(_payerId, new[] { new StoreBasketLine(small.Id, 1) }, StoreTender.Wallet, 3, _studentId);

            Assert.Null(sale.ChargeId);
            Assert.Equal(50m, await cafeteria.BalanceAsync(wallet.Id));
            await Assert.ThrowsAsync<StoreTenderRejectedException>(() => admin.RecordSaleAsync(_payerId, new[] { new StoreBasketLine(small.Id, 1) }, StoreTender.Wallet, 3, _studentId));
        }

        // --- BR-STO-005/008 returns + voids ------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-STO-005")]
        public async Task Exchanges_swap_stock_for_free_and_returns_credit_the_charge_within_the_window()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (_, small, medium) = await ShirtAsync(admin);
            await admin.SetAccountChargePolicyAsync(StoreItemCategory.Uniform, true, null);
            await admin.SetReturnPolicyAsync(StoreItemCategory.Uniform, windowDays: 7, sealedOnly: false);
            var sale = await admin.RecordSaleAsync(_payerId, new[] { new StoreBasketLine(small.Id, 2) }, StoreTender.AccountCharge, 3, _studentId);
            var line = db.StoreSaleLines.Single(l => l.StoreSaleId == sale.Id);

            await admin.ReturnOrExchangeAsync(line.Id, ReturnKind.Exchange, 1, isSealed: false, newStoreVariantId: medium.Id);
            Assert.Equal(4, await admin.StockLevelAsync(small.Id));
            Assert.Equal(4, await admin.StockLevelAsync(medium.Id));
            Assert.Empty(db.CreditNotes);

            var returned = await admin.ReturnOrExchangeAsync(line.Id, ReturnKind.Return, 1, isSealed: false);
            Assert.NotNull(returned.CreditNoteId);
            Assert.Equal(115m, db.CreditNotes.Single().Amount);   // one shirt at gross (100 + 15% VAT)
            await Assert.ThrowsAsync<ReturnNotAllowedException>(() => admin.ReturnOrExchangeAsync(line.Id, ReturnKind.Return, 1, false));   // both units already handled

            _clock.UtcNow = _clock.UtcNow.AddDays(8);
            var late = await admin.RecordSaleAsync(_payerId, new[] { new StoreBasketLine(medium.Id, 1) }, StoreTender.AccountCharge, 3, _studentId);
            _clock.UtcNow = _clock.UtcNow.AddDays(8);
            await Assert.ThrowsAsync<ReturnNotAllowedException>(() => admin.ReturnOrExchangeAsync(db.StoreSaleLines.Single(l => l.StoreSaleId == late.Id).Id, ReturnKind.Return, 1, false));
        }

        [Fact]
        [BusinessRule("BR-STO-005")]
        public async Task Sealed_only_categories_refuse_opened_returns()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var book = await admin.DefineItemAsync("كتاب", "Textbook", StoreItemCategory.Book, _bookCategoryId, new[] { new VariantInput("BK-1") });
            await admin.PublishPriceListAsync(new DateTime(2026, 9, 1), new[] { (book.Id, 60m) });
            await admin.ReceiveStockAsync(book.Variants[0].Id, 3);
            await admin.SetAccountChargePolicyAsync(StoreItemCategory.Book, true, null);
            await admin.SetReturnPolicyAsync(StoreItemCategory.Book, 14, sealedOnly: true);
            var sale = await admin.RecordSaleAsync(_payerId, new[] { new StoreBasketLine(book.Variants[0].Id, 1) }, StoreTender.AccountCharge, 3, _studentId);
            var line = db.StoreSaleLines.Single();

            await Assert.ThrowsAsync<ReturnNotAllowedException>(() => admin.ReturnOrExchangeAsync(line.Id, ReturnKind.Return, 1, isSealed: false));
            Assert.NotNull((await admin.ReturnOrExchangeAsync(line.Id, ReturnKind.Return, 1, isSealed: true)).CreditNoteId);
        }

        [Fact]
        [BusinessRule("BR-STO-008")]
        public async Task Voids_are_session_bound_reason_required_and_credit_the_charge()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (_, small, _) = await ShirtAsync(admin);
            var payments = new PaymentAdmin(db, Issuer(db), _clock);
            var till = await payments.OpenTillSessionAsync(3, "STORE", 0m);
            var sale = await admin.RecordSaleAsync(_payerId, new[] { new StoreBasketLine(small.Id, 1) }, StoreTender.Cash, 3, _studentId, till.Id);

            await admin.VoidSaleAsync(sale.Id, "duplicate scan");

            Assert.Equal(StoreSaleStatus.Voided, db.StoreSales.Single().Status);
            Assert.Equal(115m, db.CreditNotes.Single().Amount);
            Assert.Equal(5, await admin.StockLevelAsync(small.Id));
            Assert.Contains(db.AuditEntries, e => e.EntityType == nameof(StoreSale) && e.FieldName == nameof(StoreSale.VoidReason));
            var second = await admin.RecordSaleAsync(_payerId, new[] { new StoreBasketLine(small.Id, 1) }, StoreTender.Cash, 3, _studentId, till.Id);
            await payments.CloseTillSessionAsync(till.Id, 115m);
            await Assert.ThrowsAsync<StoreSaleNotVoidableException>(() => admin.VoidSaleAsync(second.Id, "late"));
        }

        // --- BR-STO-002/004/007 bundles -------------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-STO-002")]
        public async Task A_bundle_batch_assigns_and_charges_every_active_enrollment_of_the_grade_once()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (shirt, _, _) = await ShirtAsync(admin);
            EnrollChild(db, "STU-2");
            var bundle = await admin.DefineBundleAsync("طقم", "Uniform Kit G3", _profileId, _uniformCategoryId, 180m, BundleChargeMode.AtRegistration, new[] { new BundleLineInput(shirt.Id, 2) });

            var assignments = await admin.AssignBundleBatchAsync(bundle.Id);
            Assert.Empty(await admin.AssignBundleBatchAsync(bundle.Id));

            Assert.Equal(2, assignments.Count);
            Assert.All(assignments, a => Assert.Equal(BundleAssignmentStatus.Charged, a.Status));
            Assert.Equal(2, db.Charges.Count(c => c.GrossAmount == 207m));   // 180 * 1.15
        }

        [Fact]
        [BusinessRule("BR-STO-004")]
        public async Task Handout_is_pay_first_per_line_with_sizes_and_completes_the_assignment()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (shirt, small, medium) = await ShirtAsync(admin);
            var bundle = await admin.DefineBundleAsync("طقم", "Uniform Kit G3", _profileId, _uniformCategoryId, 180m, BundleChargeMode.OptIn, new[] { new BundleLineInput(shirt.Id, 2) });
            var line = db.BundleLines.Single();
            var unpaidAssignment = new BundleAssignment { BundleId = bundle.Id, StudentId = _studentId, PayerId = _payerId };
            db.BundleAssignments.Add(unpaidAssignment);
            await db.SaveChangesAsync();
            var session = await admin.OpenDistributionAsync(bundle.Id, new DateTime(2026, 10, 6));

            await Assert.ThrowsAsync<HandoutBeforeChargeException>(() => admin.HandOutAsync(session.Id, unpaidAssignment.Id, line.Id, small.Id, 1, acknowledged: true));
            await admin.HandOutAsync(session.Id, unpaidAssignment.Id, line.Id, small.Id, 1, true, requireChargedFirst: false);   // distribute-then-collect config
            Assert.Equal(BundleAssignmentStatus.Assigned, db.BundleAssignments.Single().Status);   // never charged, so never "Distributed"

            var second = EnrollChild(db, "STU-2");
            var charged = (await admin.AssignBundleBatchAsync(bundle.Id)).Single(a => a.StudentId == second);
            Assert.Single(await admin.UndistributedPaidAsync(bundle.Id));
            await admin.HandOutAsync(session.Id, charged.Id, line.Id, small.Id, 1, true);
            await admin.HandOutAsync(session.Id, charged.Id, line.Id, medium.Id, 1, true);

            Assert.Equal(BundleAssignmentStatus.Distributed, db.BundleAssignments.Single(a => a.Id == charged.Id).Status);
            Assert.Empty(await admin.UndistributedPaidAsync(bundle.Id));
            Assert.Equal(3, await admin.StockLevelAsync(small.Id));
        }

        [Fact]
        [BusinessRule("BR-STO-007")]
        public async Task An_undistributed_paid_bundle_is_credited_at_withdrawal()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (shirt, _, _) = await ShirtAsync(admin);
            var bundle = await admin.DefineBundleAsync("طقم", "Uniform Kit G3", _profileId, _uniformCategoryId, 180m, BundleChargeMode.AtRegistration, new[] { new BundleLineInput(shirt.Id, 1) });
            var assignment = (await admin.AssignBundleBatchAsync(bundle.Id)).Single();

            await admin.ResolveUndistributedAtWithdrawalAsync(assignment.Id);

            Assert.Equal(BundleAssignmentStatus.Credited, db.BundleAssignments.Single().Status);
            Assert.Equal(180m, db.CreditNotes.Single().Amount);
        }

        [Fact]
        [BusinessRule("BR-STO-006")]
        public async Task The_reorder_report_lists_variants_at_or_below_threshold()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (_, small, medium) = await ShirtAsync(admin, stock: 2);   // threshold 2
            await admin.ReceiveStockAsync(medium.Id, 3);

            var report = await admin.ReorderReportAsync();

            Assert.Equal("SH-S", Assert.Single(report).Sku);
        }
    }
}
