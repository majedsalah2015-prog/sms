using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ERP2028.Modules.Accounting.Contracts.FiscalCalendar;
using ERP2028.Modules.Accounting.Contracts.Posting;
using Sms.Application.GlExport;
using Sms.Domain.GlExport;

namespace Sms.Erp.Bridge.GlPosting
{
    /// <summary>
    /// Posts a school journal batch into ERP 2028's general ledger through
    /// <see cref="IPostingService"/> — the adapter the O3 deferral was waiting
    /// for (docs/Integration/00-ERP-SMS-Integration-Analysis.md §4.2).
    /// <para>
    /// It carries no accounting logic. The debits and credits were composed and
    /// balanced by <c>JournalSummaryBuilder</c> before they reached here; this
    /// class translates a batch into the shape the engine accepts, and translates
    /// the engine's answer back. Anything it decided on its own would be a second
    /// place for the school's accounting rules to live.
    /// </para>
    /// </summary>
    public sealed class ErpGlPostingAdapter : IGlPostingPort
    {
        /// <summary>
        /// Upper-cased deliberately. The ledger's uniqueness index on
        /// (SourceModule, SourceDocumentType, SourceDocumentId) matches
        /// case-insensitively under the default collation, so <c>"Sms"</c> and
        /// <c>"SMS"</c> would collide as the same document. Fixing the case here
        /// means the idempotency guard cannot be defeated by a caller that spells
        /// it differently later.
        /// </summary>
        public const string SourceModule = "SMS";

        public const string BatchDocumentType = "GlExportBatch";

        /// <summary>
        /// A reversal is a distinct document type over the <b>same</b> id. The
        /// engine publishes no reverse operation — <see cref="IPostingService"/>
        /// has exactly one method — so a correction is a second, mirrored posting,
        /// and it needs its own idempotency slot or the guard would reject it as a
        /// duplicate of the original. This is the pattern the ERP's own Cash
        /// module uses for voucher reversals.
        /// </summary>
        public const string ReversalDocumentType = "GlExportBatchReversal";

        private readonly IPostingService _posting;
        private readonly IFiscalCalendarDirectory _calendar;

        public ErpGlPostingAdapter(IPostingService posting, IFiscalCalendarDirectory calendar)
        {
            _posting = posting;
            _calendar = calendar;
        }

        public Task<GlPostingOutcome> PostBatchAsync(GlExportBatch batch, GlBatchPayer payer, CancellationToken cancellationToken = default)
            => PostAsync(
                batch,
                BatchDocumentType,
                Truncate(Describe(batch, payer), 500),
                reverse: false,
                cancellationToken);

        /// <summary>
        /// The reversal carries no payer clause, and that is deliberate rather
        /// than an omission. It is written from a batch loaded back out of the
        /// database, and the payer would have to be re-derived — but an allocation
        /// made <b>after</b> the batch was posted belongs to its own later period
        /// (gap G-10), so a fresh derivation can name a student the original entry
        /// never mentioned. A correction that describes more than the thing it
        /// corrects is worse than a plain one; the batch number ties the pair
        /// together, and that is what an auditor follows.
        /// </summary>
        public Task<GlPostingOutcome> ReverseBatchAsync(GlExportBatch batch, string reason, CancellationToken cancellationToken = default)
            => PostAsync(
                batch,
                ReversalDocumentType,
                Truncate($"عكس {Describe(batch, GlBatchPayer.None)}: {reason}", 500),
                reverse: true,
                cancellationToken);

        /// <summary>
        /// What an accountant reads in the ledger's description column.
        /// <para>
        /// <b>Arabic, because the ledger is.</b> ERP 2028's own chart of accounts
        /// is Arabic throughout — <c>النقدية بالصندوق</c>, <c>أوراق القبض</c>, and
        /// the school accounts <see cref="ErpGlAccountProvisioner"/> creates
        /// beside them — so an English sentence here was the one line of the entry
        /// that did not match the book it was written in. This is a stored string,
        /// not a rendered one: it is written once at posting and read for ever
        /// after, so it cannot follow the reader's culture the way a screen does,
        /// and it has to be written in the language of the ledger that keeps it.
        /// </para>
        /// <para>
        /// The payer clause is the point of the change. A period is a summary and
        /// its <i>lines</i> stay anonymous (rule 9), but the entry as a whole
        /// usually has one family behind it, and "a payment from Ahmad" is what
        /// lets an accountant recognise it without opening the batch. Several
        /// families are counted rather than listed; none named at all leaves the
        /// plain description, which is what a period of charges and no collection
        /// honestly is.
        /// </para>
        /// </summary>
        private static string Describe(GlExportBatch batch, GlBatchPayer payer)
        {
            var clause = PayerClause(payer);
            return clause == null
                ? $"قيد رسوم مدرسية {batch.BatchNo}"
                : $"قيد رسوم مدرسية {batch.BatchNo} — {clause}";
        }

        /// <summary>
        /// Arabic counts the thing it counts differently at two, at three-to-ten,
        /// and above — <c>طالبين</c>, <c>3 طلاب</c>, <c>12 طالباً</c> — and getting
        /// it wrong is the kind of mistake that tells a reader the sentence was
        /// written by a machine that does not speak their language.
        /// <para>
        /// Digits are invariant on purpose. The posting culture is whatever thread
        /// ran the generate, and a ledger reference that came out in Arabic-Indic
        /// digits one month and Western the next would not match itself.
        /// </para>
        /// </summary>
        private static string? PayerClause(GlBatchPayer payer)
        {
            var name = payer.StudentNameAr?.Trim();
            if (!string.IsNullOrEmpty(name))
            {
                return $"دفعة من الطالب {name}";
            }

            // A single payer whose name did not come through is left unsaid rather than counted as
            // "one student": the count is a fallback for the plural case, not a way to say less about
            // a case we were supposed to be able to say more about.
            var count = payer.StudentCount;
            var number = count.ToString(CultureInfo.InvariantCulture);
            return count switch
            {
                <= 1 => null,
                2 => "دفعات من طالبين",
                <= 10 => $"دفعات من {number} طلاب",
                _ => $"دفعات من {number} طالباً",
            };
        }

        private async Task<GlPostingOutcome> PostAsync(
            GlExportBatch batch, string documentType, string description, bool reverse, CancellationToken cancellationToken)
        {
            if (batch.Lines.Count == 0)
            {
                return GlPostingOutcome.Failed("Sms.Gl.EmptyBatch", $"Batch {batch.BatchNo} has no journal lines to post.");
            }

            // The batch covers a period; the entry is dated at its end, which is the day the balances
            // it summarises are true as of.
            var entryDate = batch.PeriodToUtc.Date;

            // Asked before posting purely so the failure is legible. The engine refuses a closed period
            // anyway, but it answers about the period while this answers about the year, and "no fiscal
            // year covers 2026-09-30" is a far more actionable message than a rejected posting.
            var year = await _calendar.FindByDateAsync(entryDate, cancellationToken);
            if (year == null)
            {
                return GlPostingOutcome.Failed(
                    "Sms.Gl.NoFiscalYear",
                    $"No fiscal year covers {entryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}. Create it in the ledger before posting this period.");
            }

            if (year.IsClosed)
            {
                return GlPostingOutcome.Failed(
                    "Sms.Gl.FiscalYearClosed",
                    $"Fiscal year '{year.Code}' is closed and cannot accept {batch.BatchNo}.");
            }

            var request = new PostingRequest(
                SourceModule,
                documentType,
                batch.BatchNo,
                entryDate,
                description,
                Truncate(batch.BatchNo, 50),
                BuildLines(batch, reverse));

            try
            {
                var result = await _posting.PostAsync(request, cancellationToken);
                return result.Success
                    ? GlPostingOutcome.Ok(result.Number!)
                    : GlPostingOutcome.Failed(result.Error!.Code, result.Error!.Message);
            }
            catch (Exception ex) when (IsLedgerDomainRefusal(ex))
            {
                // The engine reports expected failures as a Result, but its aggregates still throw for
                // invariants it does not pre-check — an over-long description, a date the period does
                // not cover. Those are refusals, not faults, and the caller can act on them; letting
                // them escape would turn a fixable configuration problem into a 500.
                return GlPostingOutcome.Failed("Sms.Gl.LedgerRefused", ex.Message);
            }
        }

        /// <summary>
        /// One posting line per journal line, with debit and credit swapped for a
        /// reversal.
        /// <para>
        /// Grouped by account code first: the engine refuses a request that names
        /// one account twice with the same dimensions, and two school journal keys
        /// can legitimately map to one ledger account — every fee category
        /// pointing at a single revenue account is the normal starting
        /// configuration. Without this, the mapping table would silently become
        /// un-postable the moment two keys shared a code.
        /// </para>
        /// <para>
        /// No party tag. A summary line belongs to no single payer, and the engine
        /// refuses half a party.
        /// </para>
        /// </summary>
        private static IReadOnlyList<PostingLine> BuildLines(GlExportBatch batch, bool reverse)
            => batch.Lines
                .GroupBy(l => l.AccountCode, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var debit = g.Sum(l => l.Debit);
                    var credit = g.Sum(l => l.Credit);

                    // Netting is what makes one line per account possible at all: an account that was
                    // both debited and credited within the period would otherwise be two-sided, which
                    // every ledger refuses on a single line.
                    var net = debit - credit;
                    if (reverse)
                    {
                        net = -net;
                    }

                    return new PostingLine(
                        g.Key,
                        net > 0 ? net : 0m,
                        net < 0 ? -net : 0m,
                        Truncate(g.First().Description, 200));
                })
                // A net of zero carries no information and the engine rejects an empty line.
                .Where(l => l.Debit > 0m || l.Credit > 0m)
                .ToList();

        private static bool IsLedgerDomainRefusal(Exception ex)
            => ex.GetType().FullName?.StartsWith("ERP2028.", StringComparison.Ordinal) == true;

        private static string Truncate(string value, int max)
            => value.Length <= max ? value : value.Substring(0, max);
    }
}
