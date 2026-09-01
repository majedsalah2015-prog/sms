using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Fees;
using Sms.Application.Installments;
using Sms.Domain.Common;
using Sms.Domain.Discounts;
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
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// doc/Modules/19 §8.7 from the counter's side (owner request, 2026-08-31): the fee items,
    /// the installment template and the discount chosen on the student's file and approved as
    /// one act, over a real Sqlite-backed AppDbContext.
    /// <para>
    /// The service owns no pricing, scheduling or discount rule — those are tested in their own
    /// modules. What is tested here is the part only this service can get wrong: the order the
    /// three run in, the transaction that makes them one gesture, and the arithmetic behind
    /// "edit" and "remove", which are credit notes rather than the deletions the screen calls
    /// them (BR-GLB-005).
    /// </para>
    /// </summary>
    public sealed class StudentFeeFileServiceTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 9, 15, 8, 0, 0, DateTimeKind.Utc);
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

        private readonly int _yearId;
        private readonly int _studentId;
        private readonly int _parentId;
        private readonly int _profileId;
        private readonly int _tuitionId;
        private readonly int _booksId;

        public StudentFeeFileServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            foreach (var (code, template) in new[] { ("INV", "INV-{SEQ:6}"), ("CRN", "CRN-{SEQ:5}"), ("DSC", "DSC-{SEQ:5}") })
            {
                db.NumberingSeries.Add(new NumberingSeries
                {
                    Code = code, EntityName = code, FormatTemplate = template,
                    ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Strict, EffectiveFromUtc = _clock.UtcNow, IsActive = true,
                });
            }

            var year = new AcademicYear
            {
                LabelAr = "عام", LabelEn = "2026-2027", HijriLabel = "1448",
                StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 6, 30), Status = AcademicYearStatus.Active,
            };
            db.AcademicYears.Add(year);
            var stage = new Stage { Name = new LocalizedName("مرحلة", "Elementary"), SequenceOrder = 1, DefaultGenderPolicy = GenderPolicy.Mixed };
            db.Stages.Add(stage);
            db.SaveChanges();
            _tenant.AcademicYearId = year.Id;
            _yearId = year.Id;

            var grade = new GradeLevel { StageId = stage.Id, Code = "G3", Name = new LocalizedName("الثالث", "Grade 3"), SequenceOrder = 3 };
            db.GradeLevels.Add(grade);
            db.SaveChanges();
            var profile = new GradeYearProfile { GradeLevelId = grade.Id, AcademicYearId = year.Id, GenderPolicy = GenderPolicy.Mixed, TargetSections = 1, TargetSectionSize = 25 };
            db.GradeYearProfiles.Add(profile);
            db.SaveChanges();
            _profileId = profile.Id;

            var student = new Student
            {
                StudentNo = "STU-000562",
                FirstNameAr = "سارة", FatherNameAr = "أب", GrandfatherNameAr = "جد", FamilyNameAr = "عائلة",
                FirstNameEn = "Sara", FatherNameEn = "Father", GrandfatherNameEn = "Grandfather", FamilyNameEn = "Family",
                Gender = Gender.Female, DateOfBirth = new DateTime(2018, 1, 1), NationalityLookupId = 1,
            };
            db.Students.Add(student);
            var parent = new Parent { ParentFileNo = "PAR-000001", NameAr = "ولي الأمر", NameEn = "Guardian", PrimaryMobile = "0500000000" };
            db.Parents.Add(parent);
            db.SaveChanges();
            _studentId = student.Id;
            _parentId = parent.Id;

            db.Enrollments.Add(new Enrollment
            {
                AcademicYearId = year.Id, StudentId = student.Id, GradeYearProfileId = profile.Id,
                EnrollmentDate = new DateTime(2026, 9, 1), SourceType = EnrollmentSourceType.Admission,
            });

            // No VAT on either category: the figures below are then the structure prices themselves,
            // and a test that fails says something about this service rather than about VatCalculator.
            var tuition = new FeeCategory { NameAr = "رسوم دراسية", NameEn = "Tuition", IsMandatory = true, IsRefundable = true };
            var books = new FeeCategory { NameAr = "كتب", NameEn = "Books", IsMandatory = true, IsRefundable = true };
            db.FeeCategories.AddRange(tuition, books);
            db.SaveChanges();
            _tuitionId = tuition.Id;
            _booksId = books.Id;

            db.FeeStructureLines.AddRange(
                new FeeStructureLine { AcademicYearId = year.Id, GradeYearProfileId = profile.Id, FeeCategoryId = tuition.Id, Amount = 12000m, Status = FeeStructureLineStatus.Approved },
                new FeeStructureLine { AcademicYearId = year.Id, GradeYearProfileId = profile.Id, FeeCategoryId = books.Id, Amount = 800m, Status = FeeStructureLineStatus.Approved });
            db.SaveChanges();
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private FeeAdmin CreateFeeAdmin(AppDbContext db) => new(db, new NumberIssuer(db, _tenant, _tenant, _clock), _clock, _audit);

        private InstallmentAdmin CreateInstallmentAdmin(AppDbContext db)
            => new(db, _clock, _audit, _tenant, new NotificationPublisher(db, new TestAddressBook()), CreateFeeAdmin(db));

        private StudentFeeFileService CreateService(AppDbContext db)
        {
            var fees = CreateFeeAdmin(db);
            var installments = CreateInstallmentAdmin(db);
            var discounts = new DiscountAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock), _clock, _audit, _tenant, fees, installments);
            return new StudentFeeFileService(db, fees, installments, discounts, _tenant, _user, _audit);
        }

        /// <summary>An approved quarterly template — a draft one cannot be assigned (BR-INS-001).</summary>
        private int AddApprovedTemplate()
        {
            using var db = CreateContext();
            var admin = CreateInstallmentAdmin(db);
            var template = admin.DefineTemplateAsync(_yearId, "ربعي", "Quarterly", new[]
            {
                new TemplateSplit(25m, new DateTime(2026, 9, 20)), new TemplateSplit(25m, new DateTime(2026, 11, 22)),
                new TemplateSplit(25m, new DateTime(2027, 1, 24)), new TemplateSplit(25m, new DateTime(2027, 3, 21)),
            }).GetAwaiter().GetResult();
            admin.ApproveTemplateAsync(template.Id).GetAwaiter().GetResult();
            return template.Id;
        }

        private int AddDraftTemplate()
        {
            using var db = CreateContext();
            var template = CreateInstallmentAdmin(db).DefineTemplateAsync(_yearId, "مسودة", "Draft", new[]
            {
                new TemplateSplit(100m, new DateTime(2026, 10, 1)),
            }).GetAwaiter().GetResult();
            return template.Id;
        }

        private int AddDiscountType(decimal? cap = null)
        {
            using var db = CreateContext();
            var type = new DiscountType
            {
                NameAr = "أشقاء", NameEn = "Sibling", Basis = DiscountBasis.Percentage,
                ComputationStage = DiscountComputationStage.BeforeVat, CapAmountPerStudent = cap,
                EligibilityMode = DiscountEligibilityMode.Manual, IsStackable = true, MaxCombinedPercent = 100m,
            };
            db.DiscountTypes.Add(type);
            db.SaveChanges();
            return type.Id;
        }

        private StudentFeeFileCommit Basket(
            IReadOnlyList<int>? items = null, ManualFeeItem? extra = null, int? templateId = null, DiscountRequest? discount = null, int? studentId = null)
            => new(studentId ?? _studentId, _parentId, items ?? Array.Empty<int>(), extra, templateId, discount, KsaWeekend);

        // ================================================================== the basket

        [Fact]
        [BusinessRule("BR-FEE-003")]
        [BusinessRule("BR-INS-002")]
        [BusinessRule("BR-DIS-005")]
        public async Task Commit_bills_the_items_schedules_them_and_applies_the_discount_in_one_act()
        {
            var templateId = AddApprovedTemplate();
            var typeId = AddDiscountType();

            using var db = CreateContext();
            var result = await CreateService(db).CommitAsync(Basket(
                items: new[] { _tuitionId, _booksId },
                templateId: templateId,
                discount: new DiscountRequest(typeId, 10m, "أخ في المدرسة")));

            Assert.Equal(2, result.ItemCount);
            Assert.Equal(12800m, result.PostedGross);
            Assert.NotNull(result.PlanAssignmentId);
            Assert.Equal(4, result.InstallmentCount);
            Assert.NotNull(result.DiscountGrantId);

            // 10 % of 12,800 — the discount is applied by BR-DIS-005's documents, not by this
            // service, so the figure proves the grant was approved and not merely proposed.
            Assert.Equal(1280m, result.DiscountApplied);

            using var read = CreateContext();
            Assert.Equal(2, await read.Charges.CountAsync(c => c.StudentId == _studentId && c.Status == ChargeStatus.Posted));
            Assert.Equal(DiscountGrantStatus.Approved, (await read.DiscountGrants.SingleAsync(g => g.StudentId == _studentId)).Status);
            Assert.Equal(4, await read.Installments.CountAsync());
        }

        /// <summary>
        /// The reason the whole thing runs in one transaction. The template is a draft, so the
        /// plan refuses — and the two invoices posted moments earlier must not survive it. Before
        /// the transaction was added this left the family billed for a schedule they never got.
        /// </summary>
        [Fact]
        [BusinessRule("BR-INS-001")]
        public async Task Commit_that_cannot_schedule_leaves_no_charge_behind()
        {
            var draftTemplateId = AddDraftTemplate();

            using var db = CreateContext();
            await Assert.ThrowsAsync<PlanTemplateNotApprovedException>(() =>
                CreateService(db).CommitAsync(Basket(items: new[] { _tuitionId, _booksId }, templateId: draftTemplateId)));

            using var read = CreateContext();
            Assert.Empty(await read.Charges.Where(c => c.StudentId == _studentId).ToListAsync());
            Assert.Empty(await read.PlanAssignments.ToListAsync());
        }

        /// <summary>Same guarantee from the discount end — the last step failing must unwind the first two.</summary>
        [Fact]
        [BusinessRule("BR-DIS-005")]
        public async Task Commit_that_cannot_apply_the_discount_leaves_no_charge_or_schedule_behind()
        {
            var templateId = AddApprovedTemplate();

            using var db = CreateContext();
            await Assert.ThrowsAnyAsync<Exception>(() => CreateService(db).CommitAsync(Basket(
                items: new[] { _tuitionId },
                templateId: templateId,
                discount: new DiscountRequest(DiscountTypeId: 9999, 10m, "نوع غير موجود"))));

            using var read = CreateContext();
            Assert.Empty(await read.Charges.Where(c => c.StudentId == _studentId).ToListAsync());
            Assert.Empty(await read.PlanAssignments.ToListAsync());
            Assert.Empty(await read.Installments.ToListAsync());
        }

        [Fact]
        public async Task Commit_refuses_an_empty_basket()
        {
            using var db = CreateContext();
            await Assert.ThrowsAsync<EmptyFeeFileCommitException>(() => CreateService(db).CommitAsync(Basket()));
        }

        [Fact]
        [BusinessRule("BR-FEE-002")]
        public async Task Commit_refuses_an_item_already_billed_rather_than_billing_it_twice()
        {
            using (var first = CreateContext())
            {
                await CreateService(first).CommitAsync(Basket(items: new[] { _tuitionId }));
            }

            using var db = CreateContext();
            await Assert.ThrowsAsync<FeeItemAlreadyBilledException>(() =>
                CreateService(db).CommitAsync(Basket(items: new[] { _tuitionId })));

            using var read = CreateContext();
            Assert.Equal(1, await read.Charges.CountAsync(c => c.StudentId == _studentId && c.FeeCategoryId == _tuitionId));
        }

        [Fact]
        [BusinessRule("BR-FEE-002")]
        public async Task Commit_refuses_a_student_with_no_enrollment_in_the_working_year()
        {
            int strangerId;
            using (var seed = CreateContext())
            {
                var stranger = new Student
                {
                    StudentNo = "STU-NOENROL",
                    FirstNameAr = "غير", FatherNameAr = "أب", GrandfatherNameAr = "جد", FamilyNameAr = "مسجّل",
                    FirstNameEn = "Not", FatherNameEn = "Father", GrandfatherNameEn = "Grandfather", FamilyNameEn = "Enrolled",
                    Gender = Gender.Male, DateOfBirth = new DateTime(2018, 1, 1), NationalityLookupId = 1,
                };
                seed.Students.Add(stranger);
                seed.SaveChanges();
                strangerId = stranger.Id;
            }

            using var db = CreateContext();
            await Assert.ThrowsAsync<StudentNotEnrolledForFeeFileException>(() =>
                CreateService(db).CommitAsync(Basket(items: new[] { _tuitionId }, studentId: strangerId)));
        }

        /// <summary>doc/Modules/19 §8.4 on this screen: a service the grade's price list does not carry.</summary>
        [Fact]
        [BusinessRule("BR-FEE-003")]
        public async Task Commit_bills_an_off_list_item_at_the_amount_given()
        {
            using var db = CreateContext();
            var result = await CreateService(db).CommitAsync(Basket(
                extra: new ManualFeeItem(_booksId, 250m, "نسخة بديلة عن كتاب مفقود")));

            Assert.Equal(1, result.ItemCount);
            Assert.Equal(250m, result.PostedGross);

            using var read = CreateContext();
            var charge = await read.Charges.SingleAsync(c => c.StudentId == _studentId);
            Assert.Equal(ChargeSourceType.Manual, charge.SourceType);
        }

        // ================================================================== edit and remove

        [Fact]
        [BusinessRule("BR-GLB-062")]
        public async Task Adjusting_an_item_credits_only_the_difference()
        {
            int chargeId;
            using (var seed = CreateContext())
            {
                var result = await CreateService(seed).CommitAsync(Basket(items: new[] { _tuitionId }));
                chargeId = result.PostedChargeIds.Single();
            }

            using var db = CreateContext();
            var note = await CreateService(db).AdjustItemAsync(chargeId, 9000m, "خطأ في التسعير");

            Assert.Equal(3000m, note.Amount);

            using var read = CreateContext();
            var charge = await read.Charges.SingleAsync(c => c.Id == chargeId);
            // The invoice itself is untouched — BR-GLB-062 changes the figure by a document, not an UPDATE.
            Assert.Equal(12000m, charge.GrossAmount);
            Assert.Equal(ChargeStatus.Posted, charge.Status);
        }

        [Fact]
        [BusinessRule("BR-GLB-062")]
        public async Task Adjusting_an_item_upward_is_refused()
        {
            int chargeId;
            using (var seed = CreateContext())
            {
                chargeId = (await CreateService(seed).CommitAsync(Basket(items: new[] { _tuitionId }))).PostedChargeIds.Single();
            }

            using var db = CreateContext();
            await Assert.ThrowsAsync<FeeItemAdjustmentNotLowerException>(() =>
                CreateService(db).AdjustItemAsync(chargeId, 15000m, "زيادة"));
        }

        [Fact]
        [BusinessRule("BR-GLB-005")]
        public async Task Removing_an_item_credits_its_whole_standing_value()
        {
            int chargeId;
            using (var seed = CreateContext())
            {
                chargeId = (await CreateService(seed).CommitAsync(Basket(items: new[] { _booksId }))).PostedChargeIds.Single();
            }

            using var db = CreateContext();
            var note = await CreateService(db).RemoveItemAsync(chargeId, "انسحب الطالب قبل استلام الكتب");

            Assert.Equal(800m, note.Amount);

            using var read = CreateContext();
            // The invoice stays readable — removal here is a credit note, never a deletion.
            Assert.Equal(ChargeStatus.Posted, (await read.Charges.SingleAsync(c => c.Id == chargeId)).Status);
        }

        /// <summary>
        /// The case a plain "credit the gross" would get wrong. A discount document has already
        /// taken 10 % off this item; crediting the full 12,000 would relieve it twice and leave
        /// the family 1,200 in credit for an item that was only ever worth 10,800 to them.
        /// </summary>
        [Fact]
        [BusinessRule("BR-DIS-005")]
        public async Task Removing_a_discounted_item_credits_it_net_of_the_discount()
        {
            var typeId = AddDiscountType();
            int chargeId;
            using (var seed = CreateContext())
            {
                var result = await CreateService(seed).CommitAsync(Basket(
                    items: new[] { _tuitionId },
                    discount: new DiscountRequest(typeId, 10m, "أخ في المدرسة")));
                chargeId = result.PostedChargeIds.Single();
                Assert.Equal(1200m, result.DiscountApplied);
            }

            using var db = CreateContext();
            var note = await CreateService(db).RemoveItemAsync(chargeId, "انسحاب");

            Assert.Equal(10800m, note.Amount);

            // Gross, less the discount, less the credit note = nothing standing. Relieved once.
            using var read = CreateContext();
            var charge = await read.Charges.SingleAsync(c => c.Id == chargeId);
            var credited = (await read.CreditNotes.Where(n => n.ChargeId == chargeId).Select(n => n.Amount).ToListAsync()).Sum();
            var discounted = (await read.DiscountDocuments.Where(d => d.ChargeId == chargeId).Select(d => d.Amount).ToListAsync()).Sum();
            Assert.Equal(0m, charge.GrossAmount - credited - discounted);
        }

        [Fact]
        public async Task Removing_an_item_twice_is_refused()
        {
            int chargeId;
            using (var seed = CreateContext())
            {
                chargeId = (await CreateService(seed).CommitAsync(Basket(items: new[] { _booksId }))).PostedChargeIds.Single();
            }

            using (var first = CreateContext())
            {
                await CreateService(first).RemoveItemAsync(chargeId, "انسحاب");
            }

            using var db = CreateContext();
            await Assert.ThrowsAsync<ChargeAlreadyFullyRelievedException>(() =>
                CreateService(db).RemoveItemAsync(chargeId, "مرة أخرى"));
        }
    }
}
