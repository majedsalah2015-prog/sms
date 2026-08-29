using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Library;
using Sms.Domain.Common;
using Sms.Domain.Fees;
using Sms.Domain.Grades;
using Sms.Domain.Library;
using Sms.Domain.Numbering;
using Sms.Domain.Parents;
using Sms.Domain.Schools;
using Sms.Domain.Students;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Fees;
using Sms.Infrastructure.Library;
using Sms.Infrastructure.Notifications;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>S6/E-604 (Library, doc/Modules/26, BR-LIB-001..009) over a real Sqlite-backed AppDbContext with E-303 charges.</summary>
    public sealed class LibraryAdminTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc);   // Monday
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
        private int _libraryCategoryId;

        public LibraryAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();
            foreach (var (code, template) in new[] { ("INV", "INV-{SEQ:6}"), ("CRN", "CRN-{SEQ:5}") })
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
            var parent = new Parent { ParentFileNo = "PAR-000001", NameAr = "Guardian", NameEn = "Guardian", PrimaryMobile = "0500000000", UserAccountId = 42 };
            db.Parents.Add(parent);
            db.SaveChanges();
            db.Payers.Add(new Payer { Type = PayerType.Parent, ParentId = parent.Id });
            var library = new FeeCategory { NameAr = "Library", NameEn = "Library misc", IsRefundable = false };
            db.FeeCategories.Add(library);
            db.SaveChanges();

            _yearId = year.Id;
            _profileId = profile.Id;
            _parentId = parent.Id;
            _libraryCategoryId = library.Id;
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

        private LibraryAdmin CreateAdmin(AppDbContext db) => new(db, _clock, _audit, _tenant, new FeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock), _clock), new NotificationPublisher(db, new TestAddressBook()));

        private async Task<Copy> CatalogAsync(LibraryAdmin admin, string barcode = "B-001", decimal? cost = 45m)
        {
            var title = await admin.AddTitleAsync("الأمير الصغير", "The Little Prince", "Saint-Exupéry", deweyClass: "843");
            return await admin.AddCopyAsync(title.Id, barcode, cost);
        }

        // --- BR-LIB-001 catalog ---------------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-LIB-001")]
        public async Task Barcodes_are_unique()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var copy = await CatalogAsync(admin);

            await Assert.ThrowsAsync<DuplicateBarcodeException>(() => admin.AddCopyAsync(copy.TitleId, "B-001"));
        }

        // --- BR-LIB-002/003 circulation ------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-LIB-003")]
        public async Task Checkout_sets_a_calendar_shifted_due_date_and_logs_the_event()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await CatalogAsync(admin);
            await admin.DefinePolicyAsync(MemberKind.Student, null, maxConcurrentLoans: 2, loanDays: 4, maxRenewals: 1, maxReservations: 2);   // 10/5 + 4 = Friday 10/9 -> Sunday 10/11

            var loan = await admin.CheckoutAsync("B-001", MemberKind.Student, _studentId, actorUserId: 3, KsaWeekend);

            Assert.Equal(new DateTime(2026, 10, 11), loan.DueDate);
            Assert.Equal(CopyStatus.Loaned, db.Copies.Single().Status);
            Assert.Equal(CirculationEventKind.Checkout, db.CirculationEvents.Single().Kind);
        }

        [Fact]
        [BusinessRule("BR-LIB-003")]
        public async Task Over_limit_is_blocked_unless_overridden_and_a_loaned_copy_is_never_lendable()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var first = await CatalogAsync(admin, "B-001");
            await admin.AddCopyAsync(first.TitleId, "B-002");
            await admin.DefinePolicyAsync(MemberKind.Student, null, maxConcurrentLoans: 1, loanDays: 14, maxRenewals: 1, maxReservations: 2);
            await admin.CheckoutAsync("B-001", MemberKind.Student, _studentId, 3, KsaWeekend);

            await Assert.ThrowsAsync<CheckoutBlockedException>(() => admin.CheckoutAsync("B-002", MemberKind.Student, _studentId, 3, KsaWeekend));
            var overridden = await admin.CheckoutAsync("B-002", MemberKind.Student, _studentId, 3, KsaWeekend, overrideReason: "reading week");
            Assert.True(overridden.WasOverrideCheckout);
            Assert.Contains(db.CirculationEvents, e => e.Kind == CirculationEventKind.OverrideCheckout);

            var other = EnrollChild(db, "STU-2");
            await Assert.ThrowsAsync<CheckoutBlockedException>(() => admin.CheckoutAsync("B-001", MemberKind.Student, other, 3, KsaWeekend, overrideReason: "no override lends a loaned copy"));
        }

        [Fact]
        [BusinessRule("BR-LIB-003")]
        public async Task Renewal_is_blocked_past_the_limit_or_when_another_member_reserved_the_title()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var copy = await CatalogAsync(admin);
            await admin.DefinePolicyAsync(MemberKind.Student, null, 2, 7, maxRenewals: 1, 2);
            var loan = await admin.CheckoutAsync("B-001", MemberKind.Student, _studentId, 3, KsaWeekend);
            var other = EnrollChild(db, "STU-2");

            await admin.RenewAsync(loan.Id, 3, KsaWeekend);
            await Assert.ThrowsAsync<RenewalNotAllowedException>(() => admin.RenewAsync(loan.Id, 3, KsaWeekend));

            await admin.DefinePolicyAsync(MemberKind.Student, null, 2, 7, maxRenewals: 5, 2);
            await admin.ReserveAsync(copy.TitleId, MemberKind.Student, other);
            await Assert.ThrowsAsync<RenewalNotAllowedException>(() => admin.RenewAsync(loan.Id, 3, KsaWeekend));
        }

        // --- BR-LIB-004 reservations ---------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-LIB-004")]
        public async Task A_returned_copy_is_offered_to_the_first_in_queue_and_the_hold_passes_on_when_it_expires()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var copy = await CatalogAsync(admin);
            await admin.DefinePolicyAsync(MemberKind.Student, null, 2, 7, 1, 2, holdWindowDays: 2);
            var second = EnrollChild(db, "STU-2");
            var third = EnrollChild(db, "STU-3");
            await admin.CheckoutAsync("B-001", MemberKind.Student, _studentId, 3, KsaWeekend);
            await admin.ReserveAsync(copy.TitleId, MemberKind.Student, second);
            _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
            await admin.ReserveAsync(copy.TitleId, MemberKind.Student, third);

            await admin.ReturnAsync("B-001", 3);

            Assert.Equal(CopyStatus.Reserved, db.Copies.Single().Status);
            var offered = db.Reservations.Single(r => r.Status == ReservationStatus.Offered);
            Assert.Equal(second, offered.MemberId);
            await Assert.ThrowsAsync<CheckoutBlockedException>(() => admin.CheckoutAsync("B-001", MemberKind.Student, third, 3, KsaWeekend));   // held for someone else

            _clock.UtcNow = _clock.UtcNow.AddDays(3);
            Assert.Equal(1, await admin.ExpireHoldsAsync());
            Assert.Equal(third, db.Reservations.Single(r => r.Status == ReservationStatus.Offered).MemberId);
            var loan = await admin.CheckoutAsync("B-001", MemberKind.Student, third, 3, KsaWeekend);
            Assert.Equal(ReservationStatus.Fulfilled, db.Reservations.Single(r => r.MemberId == third).Status);
        }

        // --- BR-LIB-005/006 fines + lost ------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-LIB-005")]
        public async Task Overdue_fines_are_proposed_once_per_loan_and_confirmed_into_misc_charges_that_then_block_checkout()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var copy = await CatalogAsync(admin);
            await admin.AddCopyAsync(copy.TitleId, "B-002");
            await admin.DefinePolicyAsync(MemberKind.Student, null, 3, loanDays: 1, 0, 2, finesEnabled: true, finePerDay: 0.5m, fineCap: 5m);
            await admin.CheckoutAsync("B-001", MemberKind.Student, _studentId, 3, KsaWeekend);
            _clock.UtcNow = _clock.UtcNow.AddDays(5);   // 4 days overdue

            var proposals = await admin.ProposeOverdueFinesAsync();
            Assert.Empty(await admin.ProposeOverdueFinesAsync());   // idempotent
            Assert.Equal(2m, Assert.Single(proposals).Amount);

            await admin.ConfirmFinesAsync(new[] { proposals[0].Id }, _libraryCategoryId);
            var charge = db.Charges.Single();
            Assert.Equal(2m, charge.GrossAmount);
            Assert.Equal(_libraryCategoryId, charge.FeeCategoryId);
            await Assert.ThrowsAsync<CheckoutBlockedException>(() => admin.CheckoutAsync("B-002", MemberKind.Student, _studentId, 3, KsaWeekend));
            Assert.Equal((1, 1), await admin.ClearanceStatusAsync(MemberKind.Student, _studentId));
        }

        [Fact]
        [BusinessRule("BR-LIB-006")]
        public async Task Lost_declares_a_replacement_charge_and_finding_it_later_reverses_with_a_credit_note()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await CatalogAsync(admin, cost: 45m);
            await admin.DefinePolicyAsync(MemberKind.Student, null, 3, 7, 0, 2);
            var loan = await admin.CheckoutAsync("B-001", MemberKind.Student, _studentId, 3, KsaWeekend);

            var proposal = await admin.DeclareLostAsync(loan.Id, 3);
            Assert.Equal(45m, proposal.Amount);
            Assert.Equal(CopyStatus.Lost, db.Copies.Single().Status);
            await admin.ConfirmFinesAsync(new[] { proposal.Id }, _libraryCategoryId);

            await admin.ReturnAsync("B-001", 3);

            Assert.Equal(CopyStatus.Available, db.Copies.Single().Status);
            Assert.Equal(45m, db.CreditNotes.Single().Amount);
            Assert.Equal(FineProposalStatus.Waived, db.FineProposals.Single().Status);
            Assert.Contains(db.CirculationEvents, e => e.Kind == CirculationEventKind.Found);
        }

        [Fact]
        [BusinessRule("BR-LIB-006")]
        public async Task A_copy_without_cost_needs_a_policy_price_to_be_declared_lost()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await CatalogAsync(admin, cost: null);
            var loan = await admin.CheckoutAsync("B-001", MemberKind.Student, _studentId, 3, KsaWeekend);

            await Assert.ThrowsAsync<ReplacementPriceUnknownException>(() => admin.DeclareLostAsync(loan.Id, 3));
            Assert.Equal(30m, (await admin.DeclareLostAsync(loan.Id, 3, policyPrice: 30m)).Amount);
        }

        // --- BR-LIB-008/009 -----------------------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-LIB-008")]
        public async Task Stocktake_finds_missing_copies_and_cannot_close_until_resolved()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var first = await CatalogAsync(admin, "B-001");
            await admin.AddCopyAsync(first.TitleId, "B-002");
            await admin.AddCopyAsync(first.TitleId, "B-003");
            await admin.CheckoutAsync("B-003", MemberKind.Student, _studentId, 3, KsaWeekend);
            var session = await admin.OpenStocktakeAsync();
            await admin.ScanAsync(session.Id, "B-001");

            var discrepancies = await admin.ReconcileStocktakeAsync(session.Id);

            var missing = Assert.Single(discrepancies);
            Assert.Equal(StocktakeFinding.Missing, missing.Finding);
            Assert.Equal("B-002", db.Copies.Single(c => c.Id == missing.CopyId).Barcode);
            await Assert.ThrowsAsync<StocktakeUnresolvedException>(() => admin.CloseStocktakeAsync(session.Id, 3));
            await admin.ResolveStocktakeLineAsync(missing.Id, "not found after search", markLost: true);
            await admin.CloseStocktakeAsync(session.Id, 3);
            Assert.Equal(CopyStatus.Lost, db.Copies.Single(c => c.Barcode == "B-002").Status);
            Assert.Equal(StocktakeStatus.Closed, db.StocktakeSessions.Single().Status);
        }

        [Fact]
        [BusinessRule("BR-LIB-009")]
        public async Task Class_visit_issues_a_batch_and_reports_per_student_failures()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var first = await CatalogAsync(admin, "B-001");
            await admin.AddCopyAsync(first.TitleId, "B-002");
            var second = EnrollChild(db, "STU-2");
            var third = EnrollChild(db, "STU-3");

            var results = await admin.ClassVisitCheckoutAsync(new[]
            {
                new ClassVisitIssue(_studentId, "B-001"), new ClassVisitIssue(second, "B-002"), new ClassVisitIssue(third, "B-001"),
            }, actorUserId: 3, KsaWeekend);

            Assert.Equal(2, results.Count(r => r.Loan != null));
            Assert.NotNull(results.Single(r => r.StudentId == third).Error);
            Assert.All(db.Loans, l => Assert.True(l.IsClassVisit));
        }
    }
}
