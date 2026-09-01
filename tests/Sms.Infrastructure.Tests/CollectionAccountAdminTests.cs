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
using Sms.Domain.Payments;
using Sms.Domain.Schools;
using Sms.Domain.Students;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Fees;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Payments;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// doc/Modules/21 §3 BR-PAY-002 — the catalogue of accounts student money
    /// is collected into, and the capture that has to name one of them.
    /// <para>
    /// Over a real Sqlite-backed <c>AppDbContext</c>, because half of what is
    /// being asserted is the soft-active filter's behaviour and a fake list
    /// would not have one.
    /// </para>
    /// </summary>
    public sealed class CollectionAccountAdminTests : IDisposable
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
        private readonly int _studentId;
        private readonly int _payerId;
        private readonly int _categoryId;

        public CollectionAccountAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            db.NumberingSeries.Add(new NumberingSeries
            {
                Code = "INV", EntityName = "INV", FormatTemplate = "INV-{SEQ:6}",
                ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Strict, EffectiveFromUtc = _clock.UtcNow, IsActive = true,
            });
            db.NumberingSeries.Add(new NumberingSeries
            {
                Code = "RCP", EntityName = "RCP", FormatTemplate = "RCP-{SEQ:6}",
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
                StudentNo = "STU-ACC-1",
                FirstNameAr = "طالب", FatherNameAr = "أب", GrandfatherNameAr = "جد", FamilyNameAr = "عائلة",
                FirstNameEn = "Student", FatherNameEn = "Father", GrandfatherNameEn = "Grandfather", FamilyNameEn = "Family",
                Gender = Gender.Male, DateOfBirth = new DateTime(2018, 1, 1), NationalityLookupId = 1,
            };
            db.Students.Add(student);
            db.SaveChanges();

            // PostManualChargeAsync reads the year off the student's latest enrollment, so a charge
            // has nothing to hang on without one.
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

            var category = new FeeCategory { NameAr = "رسوم دراسية", NameEn = "Tuition", IsMandatory = true, IsRefundable = true };
            db.FeeCategories.Add(category);
            db.SaveChanges();

            _studentId = student.Id;
            _payerId = payer.Id;
            _categoryId = category.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private CollectionAccountAdmin CreateAccountAdmin(AppDbContext db) => new(db);

        private PaymentAdmin CreatePaymentAdmin(AppDbContext db) => new(db, new NumberIssuer(db, _tenant, _tenant, _clock), _clock);

        private Task<Charge> PostCharge(AppDbContext db, decimal amount = 1000m)
            => new FeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock), _clock)
                .PostManualChargeAsync(_studentId, _payerId, _categoryId, amount);

        private Task<CollectionAccount> DefineBank(AppDbContext db, string code = "BANK-01", bool isDefault = false)
            => CreateAccountAdmin(db).DefineAsync(code, "الحساب البنكي", "Bank account", CollectionAccountKind.Bank,
                bankLookupId: null, bankName: "Al Rajhi", accountNo: "123456789", iban: "SA0380000000608010167519", isDefault: isDefault);

        private Task<CollectionAccount> DefineSafe(AppDbContext db, string code = "SAFE-01", bool isDefault = false)
            => CreateAccountAdmin(db).DefineAsync(code, "الصندوق الرئيسي", "Main cash box", CollectionAccountKind.CashBox, isDefault: isDefault);

        // --- the catalogue ----------------------------------------------------------

        [Fact]
        [BusinessRule("BR-PAY-002")]
        public async Task A_bank_account_with_neither_a_number_nor_an_iban_is_refused()
        {
            using var db = CreateContext();

            await Assert.ThrowsAsync<BankCollectionAccountNeedsNumberException>(() =>
                CreateAccountAdmin(db).DefineAsync("BANK-02", "بنك", "Bank", CollectionAccountKind.Bank, bankName: "Some bank"));
        }

        /// <summary>A cash box has no account number, and whatever was typed into one is dropped rather than stored and shown.</summary>
        [Fact]
        public async Task A_cash_box_keeps_no_bank_detail()
        {
            using var db = CreateContext();

            var safe = await CreateAccountAdmin(db).DefineAsync(
                "SAFE-02", "صندوق", "Safe", CollectionAccountKind.CashBox,
                bankLookupId: 7, bankName: "Al Rajhi", accountNo: "999", iban: "SA99");

            Assert.Null(safe.AccountNo);
            Assert.Null(safe.Iban);
            Assert.Null(safe.BankName);
            Assert.Null(safe.BankLookupId);
        }

        [Fact]
        public async Task Two_accounts_cannot_share_a_code()
        {
            using var db = CreateContext();
            await DefineBank(db);

            var refusal = await Assert.ThrowsAsync<DuplicateCollectionAccountCodeException>(() => DefineBank(db, "BANK-01"));
            Assert.Equal("BANK-01", refusal.Code);
        }

        /// <summary>A retired account still owns its code, or reactivating it would collide with whatever took the name.</summary>
        [Fact]
        public async Task A_retired_account_keeps_its_code_reserved()
        {
            using var db = CreateContext();
            var bank = await DefineBank(db);
            await CreateAccountAdmin(db).DeactivateAsync(bank.Id);

            await Assert.ThrowsAsync<DuplicateCollectionAccountCodeException>(() => DefineBank(db, "BANK-01"));
        }

        [Fact]
        public async Task Setting_a_default_clears_the_previous_one_of_the_same_kind_only()
        {
            using var db = CreateContext();
            var first = await DefineBank(db, "BANK-01", isDefault: true);
            var safe = await DefineSafe(db, "SAFE-01", isDefault: true);

            var second = await DefineBank(db, "BANK-02", isDefault: true);

            db.ChangeTracker.Clear();
            Assert.False(db.CollectionAccounts.Single(a => a.Id == first.Id).IsDefault);
            Assert.True(db.CollectionAccounts.Single(a => a.Id == second.Id).IsDefault);
            Assert.True(db.CollectionAccounts.Single(a => a.Id == safe.Id).IsDefault);
        }

        /// <summary>
        /// Unlike the fee-category catalogue, an account in use is still
        /// retireable: it is retired precisely because it holds history, and
        /// refusing would mean a school could never close a bank account it had
        /// ever collected into.
        /// </summary>
        [Fact]
        [BusinessRule("BR-GLB-005")]
        public async Task An_account_with_receipts_can_still_be_retired_and_reopened()
        {
            using var db = CreateContext();
            await PostCharge(db);
            var bank = await DefineBank(db);
            await CreatePaymentAdmin(db).CaptureReceiptAsync(_payerId, PaymentMethod.BankTransfer, 100m, null, "TRF-1", bank.Id);

            await CreateAccountAdmin(db).DeactivateAsync(bank.Id);

            db.ChangeTracker.Clear();
            Assert.Empty(db.CollectionAccounts.ToList());
            var retired = db.CollectionAccounts.IgnoreQueryFilters().Single(a => a.Id == bank.Id);
            Assert.False(retired.IsActive);
            Assert.Single(db.Receipts.Where(r => r.CollectionAccountId == bank.Id).ToList());

            await CreateAccountAdmin(db).ReactivateAsync(bank.Id);
            db.ChangeTracker.Clear();
            Assert.Single(db.CollectionAccounts.ToList());
        }

        // --- the capture ------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-PAY-002")]
        public async Task A_receipt_records_the_account_the_money_arrived_in()
        {
            using var db = CreateContext();
            await PostCharge(db);
            var bank = await DefineBank(db);

            var receipt = await CreatePaymentAdmin(db).CaptureReceiptAsync(_payerId, PaymentMethod.BankTransfer, 250m, null, "TRF-9", bank.Id);

            db.ChangeTracker.Clear();
            Assert.Equal(bank.Id, db.Receipts.Single(r => r.Id == receipt.Id).CollectionAccountId);
        }

        [Fact]
        [BusinessRule("BR-PAY-002")]
        public async Task A_transfer_is_refused_once_a_bank_account_exists_and_none_is_named()
        {
            using var db = CreateContext();
            await PostCharge(db);
            await DefineBank(db);

            await Assert.ThrowsAsync<CollectionAccountRequiredException>(() =>
                CreatePaymentAdmin(db).CaptureReceiptAsync(_payerId, PaymentMethod.BankTransfer, 100m, null, "TRF-2"));
        }

        /// <summary>
        /// The other half of the conditional rule, and the one that keeps a
        /// school's first morning working: with nothing defined, the receipt is
        /// still issued.
        /// </summary>
        [Fact]
        [BusinessRule("BR-PAY-002")]
        public async Task A_school_that_has_defined_nothing_can_still_take_money()
        {
            using var db = CreateContext();
            await PostCharge(db);

            var receipt = await CreatePaymentAdmin(db).CaptureReceiptAsync(_payerId, PaymentMethod.BankTransfer, 100m, null, "TRF-3");

            Assert.Null(receipt.CollectionAccountId);
        }

        /// <summary>Kinds are counted separately: a cash box does not make a transfer's destination mandatory.</summary>
        [Fact]
        [BusinessRule("BR-PAY-002")]
        public async Task A_cash_box_alone_does_not_make_a_transfer_name_an_account()
        {
            using var db = CreateContext();
            await PostCharge(db);
            await DefineSafe(db);

            var receipt = await CreatePaymentAdmin(db).CaptureReceiptAsync(_payerId, PaymentMethod.BankTransfer, 100m, null, "TRF-4");

            Assert.Null(receipt.CollectionAccountId);
        }

        [Fact]
        [BusinessRule("BR-PAY-002")]
        public async Task Cash_pointed_at_a_bank_account_is_refused()
        {
            using var db = CreateContext();
            await PostCharge(db);
            var bank = await DefineBank(db);
            var session = await CreatePaymentAdmin(db).OpenTillSessionAsync(cashierUserId: 1, "T1", floatAmount: 0m);

            await Assert.ThrowsAsync<CollectionAccountMethodMismatchException>(() =>
                CreatePaymentAdmin(db).CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 100m, session.Id, null, bank.Id));
        }

        [Fact]
        [BusinessRule("BR-PAY-002")]
        public async Task A_retired_account_is_refused_by_name()
        {
            using var db = CreateContext();
            await PostCharge(db);
            var bank = await DefineBank(db);
            await CreateAccountAdmin(db).DeactivateAsync(bank.Id);

            var refusal = await Assert.ThrowsAsync<CollectionAccountInactiveException>(() =>
                CreatePaymentAdmin(db).CaptureReceiptAsync(_payerId, PaymentMethod.BankTransfer, 100m, null, "TRF-5", bank.Id));
            Assert.Equal("BANK-01", refusal.Code);
        }

        [Fact]
        public async Task An_account_this_school_does_not_have_is_refused_rather_than_ignored()
        {
            using var db = CreateContext();
            await PostCharge(db);

            await Assert.ThrowsAsync<CollectionAccountNotFoundException>(() =>
                CreatePaymentAdmin(db).CaptureReceiptAsync(_payerId, PaymentMethod.BankTransfer, 100m, null, "TRF-6", 4242));
        }

        /// <summary>
        /// The guarantee behind the unique index, verified by writing past the
        /// service that checks it — "it compiled" proves nothing about a
        /// constraint.
        /// </summary>
        [Fact]
        public async Task The_database_itself_refuses_a_duplicate_code()
        {
            using var db = CreateContext();
            await DefineBank(db);

            db.CollectionAccounts.Add(new CollectionAccount
            {
                Code = "BANK-01", NameAr = "آخر", NameEn = "Other", Kind = CollectionAccountKind.Bank, AccountNo = "1",
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }
}
