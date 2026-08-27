using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.GlExport;
using Sms.Domain.Common;
using Sms.Domain.Fees;
using Sms.Domain.GlExport;
using Sms.Domain.Grades;
using Sms.Domain.Numbering;
using Sms.Domain.Payments;
using Sms.Domain.Schools;
using Sms.Domain.Students;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Fees;
using Sms.Infrastructure.GlExport;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Payments;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// A student's payment has to end up in the general ledger, and until this file existed nothing
    /// proved that it did. <c>GlExportServiceTests</c> covers the journal a period composes — the
    /// debits, the credits, the balance — but it constructs the service with no
    /// <see cref="IGlPostingPort"/> at all, which is the deployment that posts nothing and exports
    /// CSV. Every assertion there therefore stops one step short of the ledger.
    /// <para>
    /// These tests wire the port and follow one real receipt the rest of the way: captured through
    /// <c>PaymentAdmin</c>, summarised into a batch, handed to a ledger, and the ledger's own
    /// document number written back against the batch that produced it
    /// (docs/Integration/01-Embedded-Accounting-Plan.md §8.1, BR-FEE-008 "single money truth").
    /// </para>
    /// <para>
    /// The refusal paths matter more than the happy one. A batch that says it posted when it did not
    /// frees its period for regeneration, and the same payment then reaches the ledger twice — so
    /// both directions are asserted from a <b>second</b> context, against what was actually
    /// committed rather than what is still tracked in memory.
    /// </para>
    /// </summary>
    public sealed class GlPostingHandoffTests : IDisposable
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

        /// <summary>
        /// A ledger that answers however the test needs and remembers what it was asked. The lines
        /// are copied at the moment of the call rather than read afterwards: the service goes on to
        /// mutate and re-save the batch, so a test reading the collection later would be describing
        /// the end state instead of what the ledger was actually handed.
        /// </summary>
        private sealed class RecordingLedger : IGlPostingPort
        {
            public GlPostingOutcome PostAnswer { get; set; } = GlPostingOutcome.Ok("SY-2026-000042");

            public GlPostingOutcome ReverseAnswer { get; set; } = GlPostingOutcome.Ok("SY-2026-000043");

            public List<IReadOnlyList<GlJournalLine>> Posted { get; } = new();

            public List<(string BatchNo, string Reason, IReadOnlyList<GlJournalLine> Lines)> Reversed { get; } = new();

            public Task<GlPostingOutcome> PostBatchAsync(GlExportBatch batch, CancellationToken cancellationToken = default)
            {
                Posted.Add(batch.Lines.ToList());
                return Task.FromResult(PostAnswer);
            }

            public Task<GlPostingOutcome> ReverseBatchAsync(GlExportBatch batch, string reason, CancellationToken cancellationToken = default)
            {
                Reversed.Add((batch.BatchNo, reason, batch.Lines.ToList()));
                return Task.FromResult(ReverseAnswer);
            }
        }

        private static readonly DateTime SeptemberFrom = new(2026, 9, 1);
        private static readonly DateTime SeptemberTo = new(2026, 9, 30, 23, 59, 59);

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();
        private readonly RecordingLedger _ledger = new();
        private readonly int _studentId;
        private readonly int _payerId;
        private readonly int _tuitionId;

        public GlPostingHandoffTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            foreach (var (code, template) in new[] { ("INV", "INV-{SEQ:6}"), ("RCP", "RCP-{SEQ:6}"), ("CRN", "CRN-{SEQ:5}"), ("GLX", "GLX-{SEQ:4}") })
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
            var student = new Student
            {
                StudentNo = "STU-1", FirstNameAr = "S", FatherNameAr = "F", GrandfatherNameAr = "G", FamilyNameAr = "Fam",
                FirstNameEn = "S", FatherNameEn = "F", GrandfatherNameEn = "G", FamilyNameEn = "Fam",
                Gender = Gender.Male, DateOfBirth = new DateTime(2018, 1, 1), NationalityLookupId = 1,
            };
            db.Students.Add(student);
            db.SaveChanges();
            db.Enrollments.Add(new Enrollment
            {
                AcademicYearId = year.Id, StudentId = student.Id, GradeYearProfileId = profile.Id,
                EnrollmentDate = new DateTime(2026, 9, 1), SourceType = EnrollmentSourceType.Admission,
            });
            var payer = new Payer { Type = PayerType.Parent };
            db.Payers.Add(payer);
            var tuition = new FeeCategory { NameAr = "Tuition", NameEn = "Tuition", IsMandatory = true, IsRefundable = true, VatRate = 0.15m, GlExportCode = "4100" };
            db.FeeCategories.Add(tuition);
            db.SaveChanges();

            _studentId = student.Id;
            _payerId = payer.Id;
            _tuitionId = tuition.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private NumberIssuer Issuer(AppDbContext db) => new(db, _tenant, _tenant, _clock);

        /// <summary>The school that has an accounting system attached — the arrangement this file is about.</summary>
        private GlExportService WithLedger(AppDbContext db) => new(db, Issuer(db), _clock, _audit, _ledger);

        /// <summary>The school that has none. The O3 fallback, and still a supported deployment.</summary>
        private GlExportService WithoutLedger(AppDbContext db) => new(db, Issuer(db), _clock, _audit);

        /// <summary>
        /// One tuition charge of 1,000 (1,150 with VAT) and the family paying it in full, in cash,
        /// on the 15th. The receipt allocates itself against the charge, so the period holds all
        /// three documents a fee cycle produces: the charge, the receipt, and the allocation.
        /// </summary>
        private async Task SeedAPaidTuitionChargeAsync(AppDbContext db)
        {
            var fees = new FeeAdmin(db, Issuer(db), _clock);
            await fees.PostManualChargeAsync(_studentId, _payerId, _tuitionId, 1000m);
            await new PaymentAdmin(db, Issuer(db), _clock).CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 1150m);
        }

        private static async Task SeedMappingsAsync(GlExportService service)
        {
            await service.DefineMappingAsync(GlAccountKeys.Receivables, "1200", "ذمم مدينة", "Receivables");
            await service.DefineMappingAsync(GlAccountKeys.VatOutput, "2300", "ضريبة المخرجات", "VAT output");
            await service.DefineMappingAsync(GlAccountKeys.AdvancesReceived, "2400", "دفعات مقدمة", "Advances received");
            await service.DefineMappingAsync(GlAccountKeys.Cash("Cash"), "1000", "النقد", "Cash");
            await service.DefineMappingAsync("4100", "4100", "إيراد رسوم", "Tuition revenue");
        }

        private static GlJournalLine Line(IReadOnlyList<GlJournalLine> lines, string key, bool debit)
            => lines.Single(l => l.AccountKey == key && (debit ? l.Debit > 0m : l.Credit > 0m));

        /// <summary>
        /// The whole point of the file: money a family handed over at the counter arrives in the
        /// ledger, on the accounts the mapping table names, and the entry the ledger created is
        /// findable from the batch afterwards.
        /// </summary>
        [Fact]
        [BusinessRule("BR-PAY-003")]
        public async Task A_student_payment_reaches_the_ledger_and_the_entry_is_recorded_against_the_batch()
        {
            using var db = CreateContext();
            var service = WithLedger(db);
            await SeedAPaidTuitionChargeAsync(db);
            await SeedMappingsAsync(service);

            var batch = await service.GenerateAsync(SeptemberFrom, SeptemberTo, generatedByUserId: 1);

            var posted = Assert.Single(_ledger.Posted);
            Assert.Equal(batch.TotalDebit, batch.TotalCredit);

            // The receipt: cash in the drawer against money the school now holds for the family.
            Assert.Equal(1150m, Line(posted, GlAccountKeys.Cash("Cash"), debit: true).Debit);
            Assert.Equal("1000", Line(posted, GlAccountKeys.Cash("Cash"), debit: true).AccountCode);
            Assert.Equal(1150m, Line(posted, GlAccountKeys.AdvancesReceived, debit: false).Credit);

            // The allocation: that money applied to what the family owed, which is the half that
            // clears the receivable. A receipt with no allocation line leaves the debt standing.
            Assert.Equal(1150m, Line(posted, GlAccountKeys.AdvancesReceived, debit: true).Debit);
            Assert.Equal("2400", Line(posted, GlAccountKeys.AdvancesReceived, debit: true).AccountCode);
            Assert.Equal(1150m, Line(posted, GlAccountKeys.Receivables, debit: false).Credit);

            // And the charge the payment settled, so the period recognises the revenue it earned.
            Assert.Equal(1000m, Line(posted, "4100", debit: false).Credit);
            Assert.Equal(150m, Line(posted, GlAccountKeys.VatOutput, debit: false).Credit);

            using var reread = CreateContext();
            Assert.Equal("SY-2026-000042", reread.GlExportBatches.Single(b => b.Id == batch.Id).PostedJournalNo);
        }

        /// <summary>
        /// The refusal that matters. A ledger says no for reasons that are configuration — a closed
        /// period, an account that is not postable — and the batch must not come out of it looking
        /// posted. It stays <c>Generated</c> with no journal number, which both tells the truth and
        /// keeps the period claimed so the same receipt cannot be exported a second time.
        /// </summary>
        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task A_ledger_refusal_leaves_the_batch_unposted_and_still_holding_its_period()
        {
            using var db = CreateContext();
            var service = WithLedger(db);
            await SeedAPaidTuitionChargeAsync(db);
            await SeedMappingsAsync(service);
            _ledger.PostAnswer = GlPostingOutcome.Failed("Accounting.Period.Closed", "Period 2026-09 is closed.");

            var refusal = await Assert.ThrowsAsync<GlPostingRejectedException>(
                () => service.GenerateAsync(SeptemberFrom, SeptemberTo, 1));

            Assert.Equal("Accounting.Period.Closed", refusal.ErrorCode);
            Assert.Equal("GLX-0001", refusal.BatchNo);

            using var reread = CreateContext();
            var saved = reread.GlExportBatches.Single();
            Assert.Null(saved.PostedJournalNo);
            Assert.Equal(GlExportBatchStatus.Generated, saved.Status);

            // Held, not lost: a second attempt at the same period is refused rather than quietly
            // producing a second batch over the same receipt.
            using var second = CreateContext();
            await Assert.ThrowsAsync<GlPeriodOverlapException>(
                () => WithLedger(second).GenerateAsync(SeptemberFrom, SeptemberTo, 1));
        }

        /// <summary>
        /// Voiding a batch frees its period, so the entry it already put in the ledger has to come
        /// back out first — carrying the lines it was posted with, or the reversal is an empty entry
        /// that reverses nothing.
        /// </summary>
        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task Voiding_a_posted_batch_reverses_the_entry_before_the_period_is_freed()
        {
            using var db = CreateContext();
            var service = WithLedger(db);
            await SeedAPaidTuitionChargeAsync(db);
            await SeedMappingsAsync(service);
            var batch = await service.GenerateAsync(SeptemberFrom, SeptemberTo, 1);

            // Voided through a second context, the way the screen does it. In the context that
            // generated the batch its lines are already tracked, so the reversal would carry them
            // whether or not VoidAsync loads them — and the assertion below would prove nothing.
            using var voiding = CreateContext();
            await WithLedger(voiding).VoidAsync(batch.Id, "September reposted after corrections");

            var reversal = Assert.Single(_ledger.Reversed);
            Assert.Equal(batch.BatchNo, reversal.BatchNo);
            Assert.Equal("September reposted after corrections", reversal.Reason);
            Assert.Equal(1150m, Line(reversal.Lines, GlAccountKeys.Cash("Cash"), debit: true).Debit);

            using var reread = CreateContext();
            var saved = reread.GlExportBatches.Single(b => b.Id == batch.Id);
            Assert.Equal(GlExportBatchStatus.Voided, saved.Status);
            Assert.Equal("SY-2026-000043", saved.ReversalJournalNo);
            Assert.Equal("SY-2026-000042", saved.PostedJournalNo);
        }

        /// <summary>
        /// The double-count guard, and the reason the reversal is attempted before the status
        /// changes. If the ledger will not take the reversing entry, the original is still standing
        /// in the trial balance; freeing the period anyway would let the same payment be generated
        /// and posted again on top of it.
        /// </summary>
        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task A_refused_reversal_keeps_the_batch_generated_so_the_period_cannot_be_reused()
        {
            using var db = CreateContext();
            var service = WithLedger(db);
            await SeedAPaidTuitionChargeAsync(db);
            await SeedMappingsAsync(service);
            var batch = await service.GenerateAsync(SeptemberFrom, SeptemberTo, 1);
            _ledger.ReverseAnswer = GlPostingOutcome.Failed("Accounting.Period.Closed", "Period 2026-09 is closed.");

            using var voiding = CreateContext();
            await Assert.ThrowsAsync<GlPostingRejectedException>(() => WithLedger(voiding).VoidAsync(batch.Id, "attempted"));

            using var reread = CreateContext();
            var saved = reread.GlExportBatches.Single(b => b.Id == batch.Id);
            Assert.Equal(GlExportBatchStatus.Generated, saved.Status);
            Assert.Null(saved.ReversalJournalNo);
            Assert.Null(saved.VoidReason);

            using var second = CreateContext();
            await Assert.ThrowsAsync<GlPeriodOverlapException>(
                () => WithLedger(second).GenerateAsync(SeptemberFrom, SeptemberTo, 1));
        }

        /// <summary>
        /// One receipt, one entry. The overlap guard is what stops a period being handed over twice;
        /// after a clean void the regenerated batch is posted as its own document — a second posting,
        /// not a repeat of the first, which is what makes the ledger's own idempotency key (the batch
        /// number) meaningful.
        /// </summary>
        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task An_overlapping_period_is_never_handed_over_and_a_regenerated_one_posts_under_a_new_number()
        {
            using var db = CreateContext();
            var service = WithLedger(db);
            await SeedAPaidTuitionChargeAsync(db);
            await SeedMappingsAsync(service);
            var first = await service.GenerateAsync(SeptemberFrom, SeptemberTo, 1);

            await Assert.ThrowsAsync<GlPeriodOverlapException>(
                () => service.GenerateAsync(new DateTime(2026, 9, 15), new DateTime(2026, 10, 15), 1));
            Assert.Single(_ledger.Posted);

            await service.VoidAsync(first.Id, "corrections posted");
            var second = await service.GenerateAsync(new DateTime(2026, 9, 15), new DateTime(2026, 10, 15), 1);

            Assert.Equal(2, _ledger.Posted.Count);
            Assert.Single(_ledger.Reversed);
            Assert.NotEqual(first.BatchNo, second.BatchNo);
        }

        /// <summary>
        /// The school with no accounting system attached is not a broken one. Nothing is posted, no
        /// journal number is claimed, and the batch is still generated, balanced and renderable —
        /// the O3 fallback the optional port exists to preserve.
        /// </summary>
        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task A_school_with_no_ledger_attached_still_gets_its_batch_and_claims_no_entry()
        {
            using var db = CreateContext();
            var service = WithoutLedger(db);
            await SeedAPaidTuitionChargeAsync(db);
            await SeedMappingsAsync(service);

            var batch = await service.GenerateAsync(SeptemberFrom, SeptemberTo, 1);

            Assert.Empty(_ledger.Posted);
            Assert.Null(batch.PostedJournalNo);
            Assert.Equal(batch.TotalDebit, batch.TotalCredit);
            Assert.Equal(1150m, db.GlJournalLines.Single(l => l.GlExportBatchId == batch.Id && l.AccountKey == GlAccountKeys.Cash("Cash")).Debit);
        }
    }
}
