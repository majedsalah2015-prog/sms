using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Cafeteria;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Cafeteria;
using Sms.Domain.Common;
using Sms.Domain.Fees;
using Sms.Domain.Grades;
using Sms.Domain.Health;
using Sms.Domain.Numbering;
using Sms.Domain.Parents;
using Sms.Domain.Payments;
using Sms.Domain.Schools;
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
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>S6/E-605 (Cafeteria, doc/Modules/27, BR-CAF-001..009) over a real Sqlite-backed AppDbContext with E-303/E-602 integrations.</summary>
    public sealed class CafeteriaAdminTests : IDisposable
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
        private int _studentId;
        private int _payerId;
        private int _mealCategoryId;

        public CafeteriaAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();
            foreach (var (code, template) in new[] { ("INV", "INV-{SEQ:6}"), ("RCP", "RCP-{SEQ:6}"), ("RFD", "RFD-{SEQ:5}") })
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
            var meals = new FeeCategory { NameAr = "Meals", NameEn = "Meal plans", IsServiceLinked = true, IsRefundable = true };
            db.FeeCategories.Add(meals);
            var student = new Student
            {
                StudentNo = "STU-1", FirstNameAr = "S", FatherNameAr = "F", GrandfatherNameAr = "G", FamilyNameAr = "Fam",
                FirstNameEn = "S", FatherNameEn = "F", GrandfatherNameEn = "G", FamilyNameEn = "Fam", Gender = Gender.Male, DateOfBirth = new DateTime(2018, 1, 1), NationalityLookupId = 1,
            };
            db.Students.Add(student);
            db.SaveChanges();
            db.Enrollments.Add(new Enrollment { AcademicYearId = year.Id, StudentId = student.Id, GradeYearProfileId = profile.Id, EnrollmentDate = new DateTime(2026, 9, 1), SourceType = EnrollmentSourceType.Admission });
            db.SaveChanges();

            _studentId = student.Id;
            _payerId = payer.Id;
            _mealCategoryId = meals.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private NumberIssuer Issuer(AppDbContext db) => new(db, _tenant, _tenant, _clock);

        private HealthAdmin CreateHealth(AppDbContext db) => new(db, Issuer(db), _clock, _tenant, new AuditEventWriter(db, _tenant, _tenant, _user, _clock, _audit), new NotificationPublisher(db, new TestAddressBook()), new AttendanceAdmin(db));

        private CafeteriaAdmin CreateAdmin(AppDbContext db) => new(db, Issuer(db), _clock, _audit, new AuditEventWriter(db, _tenant, _tenant, _user, _clock, _audit), new FeeAdmin(db, Issuer(db), _clock), CreateHealth(db));

        private PaymentAdmin CreatePayments(AppDbContext db) => new(db, Issuer(db), _clock);

        private async Task<(CafeteriaItem Sandwich, CafeteriaItem Juice)> StockedItemsAsync(CafeteriaAdmin admin)
        {
            var sandwich = await admin.DefineItemAsync("ساندويتش", "Sandwich", "food", 8m, NutritionClass.Green, allergenTags: "wheat,peanuts");
            var juice = await admin.DefineItemAsync("عصير", "Juice", "drinks", 4m, NutritionClass.Amber);
            await admin.ReceiveStockAsync(sandwich.Id, 10);
            await admin.ReceiveStockAsync(juice.Id, 10);
            return (sandwich, juice);
        }

        // --- BR-CAF-001/007 wallets --------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-CAF-001")]
        public async Task A_top_up_is_a_numbered_receipt_that_credits_the_ledger_and_stays_out_of_the_fee_advance()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var wallet = await admin.EnsureWalletAsync(WalletHolderKind.Student, _studentId);

            var receipt = await admin.TopUpAsync(wallet.Id, _payerId, PaymentMethod.Cash, 50m);

            Assert.Equal("RCP-000001", receipt.ReceiptNo);
            Assert.Equal(ReceiptPurpose.WalletTopUp, receipt.Purpose);
            Assert.Equal(50m, await admin.BalanceAsync(wallet.Id));
            // Fee-side refund logic must not see wallet money as advance.
            await Assert.ThrowsAsync<RefundExceedsPositionException>(() => CreatePayments(db).RequestRefundAsync(_payerId, 10m, PaymentMethod.Cash, "not advance"));
        }

        [Fact]
        [BusinessRule("BR-CAF-009")]
        public async Task Adjustments_need_a_reason_and_are_logged_as_audit_events()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var wallet = await admin.EnsureWalletAsync(WalletHolderKind.Student, _studentId);

            await Assert.ThrowsAsync<WalletAdjustmentReasonRequiredException>(() => admin.AdjustAsync(wallet.Id, 5m, " ", 3));
            await admin.AdjustAsync(wallet.Id, 5m, "POS double charge correction", 3);

            Assert.Equal(5m, await admin.BalanceAsync(wallet.Id));
            Assert.Contains(db.AuditEntries, e => e.EntityType == nameof(Wallet) && e.Reason == "POS double charge correction");
        }

        [Fact]
        [BusinessRule("BR-CAF-001")]
        public async Task Refunding_the_balance_issues_a_refund_voucher_and_zeroes_the_wallet()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var wallet = await admin.EnsureWalletAsync(WalletHolderKind.Student, _studentId);
            await admin.TopUpAsync(wallet.Id, _payerId, PaymentMethod.Cash, 30m);

            var voucher = await admin.RefundBalanceAsync(wallet.Id, _payerId, PaymentMethod.Cash, "withdrawal");

            Assert.Equal(30m, voucher.Amount);
            Assert.Equal(0m, await admin.BalanceAsync(wallet.Id));
            await Assert.ThrowsAsync<WalletBalanceNotRefundableException>(() => admin.RefundBalanceAsync(wallet.Id, _payerId, PaymentMethod.Cash, "again"));
        }

        // --- BR-CAF-002/003 POS --------------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-CAF-003")]
        public async Task A_wallet_sale_deducts_the_ledger_and_stock_and_is_refused_beyond_the_balance()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (sandwich, juice) = await StockedItemsAsync(admin);
            var wallet = await admin.EnsureWalletAsync(WalletHolderKind.Student, _studentId);
            await admin.TopUpAsync(wallet.Id, _payerId, PaymentMethod.Cash, 20m);

            var sale = await admin.RecordSaleAsync(WalletHolderKind.Student, _studentId, new[] { new BasketLine(sandwich.Id, 1), new BasketLine(juice.Id, 2) }, SaleTender.Wallet, operatorUserId: 3);

            Assert.Equal(16m, sale.Total);
            Assert.Equal(4m, await admin.BalanceAsync(wallet.Id));
            Assert.Equal(9, await admin.StockLevelAsync(sandwich.Id));
            Assert.Equal(8, await admin.StockLevelAsync(juice.Id));
            await Assert.ThrowsAsync<SaleBlockedException>(() => admin.RecordSaleAsync(WalletHolderKind.Student, _studentId, new[] { new BasketLine(sandwich.Id, 1) }, SaleTender.Wallet, 3));
        }

        [Fact]
        [BusinessRule("BR-CAF-002")]
        public async Task Parent_controls_block_categories_and_daily_limits_and_the_allergy_feed_warns_or_blocks()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (sandwich, juice) = await StockedItemsAsync(admin);
            var wallet = await admin.EnsureWalletAsync(WalletHolderKind.Student, _studentId);
            await admin.TopUpAsync(wallet.Id, _payerId, PaymentMethod.Cash, 100m);
            await CreateHealth(db).AddAllergyAsync(_studentId, "Peanuts", AllergySeverity.Severe);
            await admin.SetSpendControlAsync(_studentId, dailyLimit: 10m, blockedCategories: "drinks", allergyHardBlock: false);

            await Assert.ThrowsAsync<SaleBlockedException>(() => admin.RecordSaleAsync(WalletHolderKind.Student, _studentId, new[] { new BasketLine(juice.Id, 1) }, SaleTender.Wallet, 3));
            await Assert.ThrowsAsync<AllergyWarningUnconfirmedException>(() => admin.RecordSaleAsync(WalletHolderKind.Student, _studentId, new[] { new BasketLine(sandwich.Id, 1) }, SaleTender.Wallet, 3));
            var sale = await admin.RecordSaleAsync(WalletHolderKind.Student, _studentId, new[] { new BasketLine(sandwich.Id, 1) }, SaleTender.Wallet, 3, operatorConfirmedAllergyWarning: true);
            Assert.True(db.SaleLines.Single(l => l.SaleId == sale.Id).AllergyWarned);
            await Assert.ThrowsAsync<SaleBlockedException>(() => admin.RecordSaleAsync(WalletHolderKind.Student, _studentId, new[] { new BasketLine(sandwich.Id, 1) }, SaleTender.Wallet, 3, operatorConfirmedAllergyWarning: true));   // 8 + 8 > 10 daily limit

            await admin.SetSpendControlAsync(_studentId, null, null, allergyHardBlock: true);
            await Assert.ThrowsAsync<SaleBlockedException>(() => admin.RecordSaleAsync(WalletHolderKind.Student, _studentId, new[] { new BasketLine(sandwich.Id, 1) }, SaleTender.Wallet, 3, operatorConfirmedAllergyWarning: true));
        }

        [Fact]
        [BusinessRule("BR-CAF-007")]
        public async Task Cash_sales_need_an_open_till_session_and_void_within_it()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var payments = CreatePayments(db);
            var (_, juice) = await StockedItemsAsync(admin);

            await Assert.ThrowsAsync<SaleBlockedException>(() => admin.RecordSaleAsync(WalletHolderKind.Student, _studentId, new[] { new BasketLine(juice.Id, 1) }, SaleTender.Cash, 3));
            var till = await payments.OpenTillSessionAsync(3, "CAF-1", 100m);
            var sale = await admin.RecordSaleAsync(WalletHolderKind.Student, _studentId, new[] { new BasketLine(juice.Id, 1) }, SaleTender.Cash, 3, tillSessionId: till.Id);
            Assert.Equal(9, await admin.StockLevelAsync(juice.Id));

            await admin.VoidSaleAsync(sale.Id, "wrong student");
            Assert.Equal(SaleStatus.Voided, db.Sales.Single().Status);
            Assert.Equal(10, await admin.StockLevelAsync(juice.Id));
            Assert.Contains(db.AuditEntries, e => e.EntityType == nameof(Sale) && e.FieldName == nameof(Sale.VoidReason) && e.Reason == "wrong student");

            var second = await admin.RecordSaleAsync(WalletHolderKind.Student, _studentId, new[] { new BasketLine(juice.Id, 1) }, SaleTender.Cash, 3, tillSessionId: till.Id);
            await payments.CloseTillSessionAsync(till.Id, 4m);
            await Assert.ThrowsAsync<SaleNotVoidableException>(() => admin.VoidSaleAsync(second.Id, "too late"));
        }

        [Fact]
        [BusinessRule("BR-CAF-008")]
        public async Task Banned_items_cannot_be_menued_or_sold_to_students()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var energy = await admin.DefineItemAsync("مشروب طاقة", "Energy drink", "drinks", 6m, NutritionClass.Banned);
            await admin.ReceiveStockAsync(energy.Id, 5);
            var wallet = await admin.EnsureWalletAsync(WalletHolderKind.Student, _studentId);
            await admin.TopUpAsync(wallet.Id, _payerId, PaymentMethod.Cash, 20m);

            await Assert.ThrowsAsync<BannedItemOnMenuException>(() => admin.DefineMenuAsync(new DateTime(2026, 10, 6), new[] { energy.Id }));
            await Assert.ThrowsAsync<SaleBlockedException>(() => admin.RecordSaleAsync(WalletHolderKind.Student, _studentId, new[] { new BasketLine(energy.Id, 1) }, SaleTender.Wallet, 3));
        }

        // --- BR-CAF-004 meal plans -----------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-CAF-004")]
        public async Task A_meal_plan_charges_via_fees_and_redeems_once_per_day_within_the_cap()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (sandwich, juice) = await StockedItemsAsync(admin);
            var plan = await admin.DefineMealPlanAsync("خطة", "Monthly lunch", _mealCategoryId, 200m, dailyValueCap: 12m);
            var subscription = await admin.SubscribeMealPlanAsync(_studentId, _payerId, plan.Id, new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));

            Assert.Equal(200m, db.Charges.Single(c => c.Id == subscription.ChargeId).GrossAmount);
            await Assert.ThrowsAsync<SaleBlockedException>(() => admin.RecordSaleAsync(WalletHolderKind.Student, _studentId, new[] { new BasketLine(sandwich.Id, 1), new BasketLine(juice.Id, 2) }, SaleTender.MealPlan, 3));   // 16 > cap
            var lunch = await admin.RecordSaleAsync(WalletHolderKind.Student, _studentId, new[] { new BasketLine(sandwich.Id, 1), new BasketLine(juice.Id, 1) }, SaleTender.MealPlan, 3);
            Assert.Single(db.Redemptions);
            await Assert.ThrowsAsync<SaleBlockedException>(() => admin.RecordSaleAsync(WalletHolderKind.Student, _studentId, new[] { new BasketLine(juice.Id, 1) }, SaleTender.MealPlan, 3));

            var summary = await admin.DailySummaryAsync(_clock.UtcNow);
            Assert.Equal(12m, summary.MealPlanRedemptions);
            Assert.Equal(1, summary.SaleCount);
        }

        // --- BR-CAF-006 stock -----------------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-CAF-006")]
        public async Task Waste_reduces_stock_and_sales_cannot_take_stock_negative()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var juice = await admin.DefineItemAsync("عصير", "Juice", "drinks", 4m, NutritionClass.Amber);
            await admin.ReceiveStockAsync(juice.Id, 3);
            await admin.RecordWasteAsync(juice.Id, 2, "spoiled");
            var wallet = await admin.EnsureWalletAsync(WalletHolderKind.Student, _studentId);
            await admin.TopUpAsync(wallet.Id, _payerId, PaymentMethod.Cash, 50m);

            Assert.Equal(1, await admin.StockLevelAsync(juice.Id));
            await Assert.ThrowsAsync<SaleBlockedException>(() => admin.RecordSaleAsync(WalletHolderKind.Student, _studentId, new[] { new BasketLine(juice.Id, 2) }, SaleTender.Wallet, 3));
        }
    }
}
