using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ERP2028.Common.Results;
using ERP2028.Modules.Accounting.Contracts.FiscalCalendar;
using ERP2028.Modules.Accounting.Contracts.Posting;
using ERP2028.Modules.Accounting.Domain.Exceptions;
using Sms.Domain.GlExport;
using Sms.Erp.Bridge.GlPosting;
using Sms.TestSupport;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The far side of the same question <c>GlPostingHandoffTests</c> asks: the school hands a batch
    /// to <c>IGlPostingPort</c>, and this is what the embedded ledger actually receives when that
    /// port is the ERP. Nothing exercised <see cref="ErpGlPostingAdapter"/> before — the one class
    /// standing between a family's payment and a journal entry in ERP 2028.
    /// <para>
    /// Every assertion here is one of the mandatory adapter rules in
    /// docs/Integration/01-Embedded-Accounting-Plan.md §8.2, which were derived by reading the
    /// posting engine: the upper-cased source module (1), the batch number as the idempotency key
    /// (2), the reversal as a second posting under its own document type (3), one line per account
    /// code (4), the length limits (5), domain exceptions caught rather than escaping as a 500 (6),
    /// the fiscal-calendar pre-check (8), and no party dimension on a summary line (9).
    /// </para>
    /// <para>
    /// Rule 10 — a <c>BranchCode</c> naming the school's branch — is <b>not</b> implemented by the
    /// adapter and is therefore not asserted here. See
    /// <see cref="No_branch_dimension_is_sent_which_is_a_stated_deviation_from_rule_10"/>, which
    /// pins the behaviour that exists so the gap is visible rather than assumed closed.
    /// </para>
    /// </summary>
    public class ErpGlPostingAdapterTests
    {
        private sealed class RecordingPostingService : IPostingService
        {
            public List<PostingRequest> Requests { get; } = new();

            public PostingResult Answer { get; set; } = PostingResult.Ok(1, "SY-2026-000042");

            /// <summary>What the engine throws instead of answering — its aggregates still raise for invariants it does not pre-check.</summary>
            public Exception? Throws { get; set; }

            public Task<PostingResult> PostAsync(PostingRequest request, CancellationToken cancellationToken = default)
            {
                Requests.Add(request);
                if (Throws != null)
                {
                    throw Throws;
                }

                return Task.FromResult(Answer);
            }
        }

        private sealed class FakeCalendar : IFiscalCalendarDirectory
        {
            public FiscalYearStatusInfo? Year { get; set; }
                = new("FY2026", "2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), IsClosed: false);

            public Task<FiscalYearStatusInfo?> FindByDateAsync(DateTime date, CancellationToken cancellationToken = default)
                => Task.FromResult(Year);

            public Task<FiscalYearStatusInfo?> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
                => Task.FromResult(Year);
        }

        private readonly RecordingPostingService _engine = new();
        private readonly FakeCalendar _calendar = new();

        private ErpGlPostingAdapter Adapter() => new(_engine, _calendar);

        private static GlJournalLine Row(int seq, string key, string code, string description, decimal debit, decimal credit)
            => new()
            {
                SequenceNumber = seq, AccountKey = key, AccountCode = code, Description = description,
                Debit = debit, Credit = credit, SourceDocumentCount = 1,
            };

        /// <summary>
        /// September, as a school actually closes it: a 1,000 tuition charge with 150 of VAT, and
        /// the family paying the whole 1,150 in cash inside the same month. Seven lines, because the
        /// school's own journal keeps the receipt and its allocation apart — exactly the batch
        /// <c>GlExportService</c> stores.
        /// </summary>
        private static GlExportBatch PaidTuitionSeptember() => new()
        {
            BatchNo = "GLX-0001",
            PeriodFromUtc = new DateTime(2026, 9, 1),
            PeriodToUtc = new DateTime(2026, 9, 30, 23, 59, 59),
            TotalDebit = 3450m,
            TotalCredit = 3450m,
            SourceDocumentCount = 3,
            Lines =
            {
                Row(1, "4100", "4100", "Fee revenue", 0m, 1000m),
                Row(2, "Cash:Cash", "1000", "Receipts (Cash)", 1150m, 0m),
                Row(3, "Receivables", "1200", "Charges posted", 1150m, 0m),
                Row(4, "Receivables", "1200", "Receipts applied to charges", 0m, 1150m),
                Row(5, "AdvancesReceived", "2400", "Receipts applied to charges", 1150m, 0m),
                Row(6, "AdvancesReceived", "2400", "Receipts taken", 0m, 1150m),
                Row(7, "VatOutput", "2300", "VAT on charges", 0m, 150m),
            },
        };

        private static PostingLine Account(PostingRequest request, string code)
            => request.Lines.Single(l => l.AccountCode == code);

        /// <summary>
        /// The payment reaches the general ledger as one balanced entry, keyed so the engine can
        /// refuse a repeat of it. What survives netting is the true economic picture of the month:
        /// cash came in, revenue and its tax were earned. The receivable and the advance were both
        /// raised and cleared inside the period, so neither belongs on the entry at all — and the
        /// engine would refuse a two-sided line for one account anyway.
        /// </summary>
        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task A_paid_tuition_period_reaches_the_ledger_as_one_balanced_keyed_entry()
        {
            var outcome = await Adapter().PostBatchAsync(PaidTuitionSeptember());

            var request = Assert.Single(_engine.Requests);
            Assert.Equal("SMS", request.SourceModule);
            Assert.Equal("GlExportBatch", request.SourceDocumentType);
            Assert.Equal("GLX-0001", request.SourceDocumentId);
            Assert.Equal(new DateTime(2026, 9, 30), request.Date);

            Assert.Equal(1150m, Account(request, "1000").Debit);
            Assert.Equal(1000m, Account(request, "4100").Credit);
            Assert.Equal(150m, Account(request, "2300").Credit);
            Assert.DoesNotContain(request.Lines, l => l.AccountCode == "1200");
            Assert.DoesNotContain(request.Lines, l => l.AccountCode == "2400");
            Assert.Equal(request.Lines.Sum(l => l.Debit), request.Lines.Sum(l => l.Credit));

            // Rule 9: a summary line belongs to no single payer, and the engine refuses half a party.
            Assert.All(request.Lines, l => Assert.Null(l.PartyType));
            Assert.All(request.Lines, l => Assert.Null(l.PartyCode));

            Assert.True(outcome.Success);
            Assert.Equal("SY-2026-000042", outcome.DocumentNumber);
        }

        /// <summary>
        /// Rule 4. Two fee categories pointing at one revenue account is the normal starting
        /// configuration, and the engine refuses a request naming one account twice with the same
        /// dimensions — so the adapter has to combine them before sending, not after being rejected.
        /// </summary>
        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task Two_fee_categories_mapped_to_one_revenue_account_arrive_as_a_single_line()
        {
            var batch = new GlExportBatch
            {
                BatchNo = "GLX-0002",
                PeriodFromUtc = new DateTime(2026, 10, 1),
                PeriodToUtc = new DateTime(2026, 10, 31),
                Lines =
                {
                    Row(1, "4100", "4000", "Fee revenue", 0m, 1000m),
                    Row(2, "4200", "4000", "Fee revenue", 0m, 500m),
                    Row(3, "Cash:Cash", "1000", "Receipts (Cash)", 1500m, 0m),
                },
            };

            await Adapter().PostBatchAsync(batch);

            var request = Assert.Single(_engine.Requests);
            Assert.Equal(2, request.Lines.Count);
            Assert.Equal(1500m, Account(request, "4000").Credit);
            Assert.Equal(0m, Account(request, "4000").Debit);
        }

        /// <summary>
        /// Rule 3. The engine publishes no reverse operation, so a correction is a second, mirrored
        /// posting — and it needs its own document type or the idempotency guard rejects it as a
        /// duplicate of the entry it is trying to undo.
        /// </summary>
        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task A_reversal_is_a_second_posting_with_every_side_swapped()
        {
            _engine.Answer = PostingResult.Ok(2, "SY-2026-000043");

            var outcome = await Adapter().ReverseBatchAsync(PaidTuitionSeptember(), "September reposted after corrections");

            var request = Assert.Single(_engine.Requests);
            Assert.Equal("GlExportBatchReversal", request.SourceDocumentType);
            Assert.Equal("GLX-0001", request.SourceDocumentId);
            Assert.Contains("September reposted after corrections", request.Description);

            Assert.Equal(1150m, Account(request, "1000").Credit);
            Assert.Equal(0m, Account(request, "1000").Debit);
            Assert.Equal(1000m, Account(request, "4100").Debit);
            Assert.Equal(150m, Account(request, "2300").Debit);
            Assert.Equal("SY-2026-000043", outcome.DocumentNumber);
        }

        /// <summary>
        /// Rule 5. A void reason is free text a user typed, and the engine raises on an over-long
        /// description rather than returning a refusal — so the truncation is what keeps a long
        /// explanation from turning a correction into a 500.
        /// </summary>
        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task A_long_void_reason_is_truncated_to_what_the_engine_accepts()
        {
            await Adapter().ReverseBatchAsync(PaidTuitionSeptember(), new string('x', 900));

            var request = Assert.Single(_engine.Requests);
            Assert.Equal(500, request.Description.Length);
            Assert.NotNull(request.Reference);
            Assert.True(request.Reference!.Length <= 50);
        }

        /// <summary>
        /// Rule 8, and the reason the pre-check exists at all. The engine refuses a closed period on
        /// its own, but it answers about the period; this answers about the year, and it names the
        /// year — which is the difference between an accountant knowing what to reopen and reading a
        /// rejected posting.
        /// </summary>
        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task A_closed_fiscal_year_is_refused_before_anything_is_sent()
        {
            _calendar.Year = new FiscalYearStatusInfo("FY2026", "2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), IsClosed: true);

            var outcome = await Adapter().PostBatchAsync(PaidTuitionSeptember());

            Assert.False(outcome.Success);
            Assert.Equal("Sms.Gl.FiscalYearClosed", outcome.ErrorCode);
            Assert.Contains("FY2026", outcome.ErrorMessage!);
            Assert.Empty(_engine.Requests);
        }

        /// <summary>A period no fiscal year covers is named as such, rather than reaching the engine to be refused for a reason nobody can act on.</summary>
        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task A_period_outside_every_fiscal_year_is_named_rather_than_sent()
        {
            _calendar.Year = null;

            var outcome = await Adapter().PostBatchAsync(PaidTuitionSeptember());

            Assert.False(outcome.Success);
            Assert.Equal("Sms.Gl.NoFiscalYear", outcome.ErrorCode);
            Assert.Contains("2026-09-30", outcome.ErrorMessage!);
            Assert.Empty(_engine.Requests);
        }

        /// <summary>
        /// The engine's own refusal travels back with its code intact, because the caller has a real
        /// choice to make with it: most of these are configuration to fix and retry, not faults.
        /// </summary>
        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task The_engines_refusal_is_reported_with_its_own_code()
        {
            _engine.Answer = PostingResult.Fail(new Error("Accounting.Account.NotPostable", "Account 4100 is a header."));

            var outcome = await Adapter().PostBatchAsync(PaidTuitionSeptember());

            Assert.False(outcome.Success);
            Assert.Equal("Accounting.Account.NotPostable", outcome.ErrorCode);
            Assert.Equal("Account 4100 is a header.", outcome.ErrorMessage!);
            Assert.Null(outcome.DocumentNumber);
        }

        /// <summary>
        /// Rule 6. <c>PostingService</c> reports expected failures as a result but does not wrap its
        /// aggregates, so an invariant it never pre-checked arrives as an exception. Letting it
        /// escape would turn a fixable configuration problem into a 500 on the GL export screen.
        /// </summary>
        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task An_accounting_domain_exception_comes_back_as_a_refusal_not_a_fault()
        {
            _engine.Throws = new AccountingDomainException("Entry date 2026-09-30 is outside period 2026-08.");

            var outcome = await Adapter().PostBatchAsync(PaidTuitionSeptember());

            Assert.False(outcome.Success);
            Assert.Equal("Sms.Gl.LedgerRefused", outcome.ErrorCode);
            Assert.Contains("outside period", outcome.ErrorMessage!);
        }

        /// <summary>
        /// A fault that is not the ledger's is not the ledger's to answer for. Anything outside
        /// <c>ERP2028.</c> keeps travelling, because swallowing it here would report a broken
        /// deployment as a rejected posting an accountant would go looking for in the chart.
        /// </summary>
        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task A_fault_that_is_not_the_ledgers_still_escapes()
        {
            _engine.Throws = new InvalidOperationException("the connection was closed");

            await Assert.ThrowsAsync<InvalidOperationException>(() => Adapter().PostBatchAsync(PaidTuitionSeptember()));
        }

        /// <summary>
        /// A month in which nothing happened produces a batch with no lines, and an entry with no
        /// lines is not something to send: the engine rejects it, and the school would read that as
        /// its ledger refusing a period rather than as a quiet month.
        /// </summary>
        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task An_empty_batch_never_reaches_the_engine()
        {
            var outcome = await Adapter().PostBatchAsync(new GlExportBatch { BatchNo = "GLX-0003", PeriodToUtc = new DateTime(2026, 9, 30) });

            Assert.False(outcome.Success);
            Assert.Equal("Sms.Gl.EmptyBatch", outcome.ErrorCode);
            Assert.Empty(_engine.Requests);
        }

        /// <summary>
        /// <b>A stated deviation, pinned rather than endorsed.</b>
        /// docs/Integration/01-Embedded-Accounting-Plan.md §8.2 rule 10 requires
        /// <c>BranchCode</c> = the ERP branch corresponding to the school; the adapter sends none.
        /// A null branch is accepted by the engine — it validates a code only when one is given — so
        /// posting works today and this test passes; what it does not do is produce branch-analysed
        /// figures for a ledger configured per branch.
        /// <para>
        /// It is asserted deliberately: an untested null reads as an oversight and gets "fixed" by
        /// someone guessing a code, while a tested one says the school has no branch mapping yet and
        /// names the doc rule that will close it.
        /// </para>
        /// </summary>
        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task No_branch_dimension_is_sent_which_is_a_stated_deviation_from_rule_10()
        {
            await Adapter().PostBatchAsync(PaidTuitionSeptember());

            var request = Assert.Single(_engine.Requests);
            Assert.All(request.Lines, l => Assert.Null(l.BranchCode));
            Assert.All(request.Lines, l => Assert.Null(l.CostCentreCode));
            Assert.All(request.Lines, l => Assert.Null(l.ProjectCode));
        }
    }
}
