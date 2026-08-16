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
    /// S3/E-303 (slice: Payments, doc/Modules/21, BR-PAY-001..005) over a
    /// real Sqlite-backed AppDbContext, including E-006's real
    /// INumberIssuer (the "RCP"/"RFD" series). Reuses FeeAdmin to post
    /// real charges so allocation has something meaningful to cover.
    /// </summary>
    public sealed class PaymentAdminTests : IDisposable
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
        private int _categoryId;

        public PaymentAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            foreach (var (code, template) in new[] { ("INV", "INV-{SEQ:6}"), ("RCP", "RCP-{SEQ:6}"), ("RFD", "RFD-{SEQ:5}") })
            {
                db.NumberingSeries.Add(new NumberingSeries
                {
                    Code = code, EntityName = code, FormatTemplate = template,
                    ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Strict, EffectiveFromUtc = _clock.UtcNow, IsActive = true,
                });
            }

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

            var category = new FeeCategory { NameAr = "رسوم دراسية", NameEn = "Tuition", IsMandatory = true, IsRefundable = true };
            db.FeeCategories.Add(category);
            db.SaveChanges();

            var line = new FeeStructureLine
            {
                AcademicYearId = year.Id, GradeYearProfileId = profile.Id, FeeCategoryId = category.Id,
                Amount = 1000m, Status = FeeStructureLineStatus.Approved,
            };
            db.FeeStructureLines.Add(line);
            db.SaveChanges();

            _profileId = profile.Id;
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

        private PaymentAdmin CreatePaymentAdmin(AppDbContext db) => new(db, new NumberIssuer(db, _tenant, _tenant, _clock), _clock);

        private FeeAdmin CreateFeeAdmin(AppDbContext db) => new(db, new NumberIssuer(db, _tenant, _tenant, _clock), _clock);

        private Task<Charge> PostCharge(AppDbContext db, decimal amount = 1000m)
            => CreateFeeAdmin(db).PostManualChargeAsync(_studentId, _payerId, _categoryId, amount);

        // --- BR-PAY-001 till sessions -----------------------------------------------

        [Fact]
        [BusinessRule("BR-PAY-001")]
        public async Task Closing_a_session_freezes_the_system_total_from_its_posted_receipts()
        {
            using var db = CreateContext();
            var payments = CreatePaymentAdmin(db);
            await PostCharge(db, 1000m);
            var session = await payments.OpenTillSessionAsync(cashierUserId: 1, "T1", floatAmount: 200m);
            await payments.CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 300m, session.Id);

            await payments.CloseTillSessionAsync(session.Id, countedTotal: 300m);

            var stored = db.TillSessions.Single(s => s.Id == session.Id);
            Assert.Equal(300m, stored.SystemTotal);
            Assert.Equal(TillSessionStatus.Closed, stored.Status);
        }

        [Fact]
        [BusinessRule("BR-PAY-001")]
        public async Task Capturing_against_a_closed_session_is_rejected()
        {
            using var db = CreateContext();
            var payments = CreatePaymentAdmin(db);
            var session = await payments.OpenTillSessionAsync(1, "T1", 200m);
            await payments.CloseTillSessionAsync(session.Id, 200m);

            await Assert.ThrowsAsync<TillSessionNotOpenException>(() =>
                payments.CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 100m, session.Id));
        }

        // --- BR-PAY-002/003 receipts + allocation -----------------------------------

        [Fact]
        [BusinessRule("BR-PAY-002")]
        public async Task Capturing_a_receipt_issues_a_real_receipt_number()
        {
            using var db = CreateContext();
            var payments = CreatePaymentAdmin(db);

            var receipt = await payments.CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 500m);

            Assert.Equal("RCP-000001", receipt.ReceiptNo);
        }

        [Fact]
        [BusinessRule("BR-PAY-003")]
        public async Task A_partial_payment_allocates_against_the_open_charge()
        {
            using var db = CreateContext();
            var payments = CreatePaymentAdmin(db);
            var charge = await PostCharge(db, 1000m);

            await payments.CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 400m);

            var allocated = db.PaymentAllocations.Where(a => a.ChargeId == charge.Id).ToList().Sum(a => a.AllocatedAmount);
            Assert.Equal(400m, allocated);
        }

        [Fact]
        [BusinessRule("BR-PAY-003")]
        public async Task A_payment_beyond_all_open_charges_becomes_an_advance_balance()
        {
            using var db = CreateContext();
            var payments = CreatePaymentAdmin(db);
            var charge = await PostCharge(db, 400m);

            await payments.CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 1000m);

            var allocated = db.PaymentAllocations.Where(a => a.ChargeId == charge.Id).ToList().Sum(a => a.AllocatedAmount);
            Assert.Equal(400m, allocated); // only the open charge's balance is allocated; 600 sits as advance
        }

        // --- BR-PAY-004 PDC lifecycle -------------------------------------------------

        [Fact]
        [BusinessRule("BR-PAY-004")]
        public async Task Clearing_a_pdc_issues_a_real_receipt_and_allocates_it()
        {
            using var db = CreateContext();
            var payments = CreatePaymentAdmin(db);
            var charge = await PostCharge(db, 1000m);
            var pdc = await payments.LodgePdcAsync(_payerId, "Al Rajhi Bank", "CHQ-001", new DateTime(2027, 3, 15), 1000m);
            await payments.ChangePdcStatusAsync(pdc.Id, PdcStatus.Due, new DateTime(2027, 3, 13));
            await payments.ChangePdcStatusAsync(pdc.Id, PdcStatus.Deposited, new DateTime(2027, 3, 15));

            await payments.ChangePdcStatusAsync(pdc.Id, PdcStatus.Cleared, new DateTime(2027, 3, 16));

            var stored = db.Pdcs.Single(p => p.Id == pdc.Id);
            Assert.NotNull(stored.ClearedReceiptId);
            var allocated = db.PaymentAllocations.Where(a => a.ChargeId == charge.Id).ToList().Sum(a => a.AllocatedAmount);
            Assert.Equal(1000m, allocated);
        }

        [Fact]
        [BusinessRule("BR-PAY-004")]
        public async Task Clearing_a_pdc_that_was_never_deposited_is_rejected()
        {
            using var db = CreateContext();
            var payments = CreatePaymentAdmin(db);
            var pdc = await payments.LodgePdcAsync(_payerId, "Al Rajhi Bank", "CHQ-002", new DateTime(2027, 3, 15), 500m);

            await Assert.ThrowsAsync<InvalidPdcStatusTransitionException>(() =>
                payments.ChangePdcStatusAsync(pdc.Id, PdcStatus.Cleared, new DateTime(2027, 3, 16)));
        }

        // --- BR-PAY-005 refunds ---------------------------------------------------------

        [Fact]
        [BusinessRule("BR-PAY-005")]
        public async Task Requesting_a_refund_beyond_the_advance_balance_is_rejected()
        {
            using var db = CreateContext();
            var payments = CreatePaymentAdmin(db);
            await PostCharge(db, 1000m);
            await payments.CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 400m); // fully allocated, no advance

            await Assert.ThrowsAsync<RefundExceedsPositionException>(() =>
                payments.RequestRefundAsync(_payerId, 100m, PaymentMethod.Cash, "Overpayment"));
        }

        [Fact]
        [BusinessRule("BR-PAY-005")]
        public async Task A_refund_within_the_advance_balance_moves_through_its_lifecycle()
        {
            using var db = CreateContext();
            var payments = CreatePaymentAdmin(db);
            await PostCharge(db, 400m);
            await payments.CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 1000m); // 600 advance

            var voucher = await payments.RequestRefundAsync(_payerId, 500m, PaymentMethod.Cash, "Withdrawal refund");
            await payments.ChangeRefundVoucherStatusAsync(voucher.Id, RefundVoucherStatus.Approved);
            await payments.ChangeRefundVoucherStatusAsync(voucher.Id, RefundVoucherStatus.Paid);

            Assert.Equal(RefundVoucherStatus.Paid, db.RefundVouchers.Single(v => v.Id == voucher.Id).Status);
            Assert.StartsWith("RFD-", voucher.VoucherNo);
        }
    }
}
