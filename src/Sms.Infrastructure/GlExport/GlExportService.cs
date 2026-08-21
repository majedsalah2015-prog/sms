using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.GlExport;
using Sms.Application.Numbering;
using Sms.Domain.Fees;
using Sms.Domain.GlExport;
using Sms.Domain.Payments;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.GlExport
{
    /// <summary>Standalone — saves itself. Reads Module 19/21/22 documents by their own dates; RefundVoucher has no paid-at column, so its last-modified stamp (the Paid transition) is used.</summary>
    public class GlExportService : IGlExportService
    {
        public const string BatchSeriesCode = "GLX";

        private readonly AppDbContext _db;
        private readonly INumberIssuer _numberIssuer;
        private readonly IClock _clock;
        private readonly IAuditContext _audit;
        private readonly IGlPostingPort? _posting;

        /// <summary>
        /// <paramref name="posting"/> is optional on purpose: a deployment with no
        /// ledger attached registers none, and this service behaves exactly as it
        /// did before — generate, balance, number, render CSV. That is the O3
        /// fallback, not a degraded path.
        /// </summary>
        public GlExportService(AppDbContext db, INumberIssuer numberIssuer, IClock clock, IAuditContext audit, IGlPostingPort? posting = null)
        {
            _db = db;
            _numberIssuer = numberIssuer;
            _clock = clock;
            _audit = audit;
            _posting = posting;
        }

        public async Task<GlAccountMapping> DefineMappingAsync(string key, string accountCode, string accountNameAr, string accountNameEn, CancellationToken cancellationToken = default)
        {
            var existing = await _db.GlAccountMappings.SingleOrDefaultAsync(m => m.Key == key, cancellationToken);
            if (existing != null)
            {
                existing.AccountCode = accountCode;
                existing.AccountNameAr = accountNameAr;
                existing.AccountNameEn = accountNameEn;
                await _db.SaveChangesAsync(cancellationToken);
                return existing;
            }

            var mapping = new GlAccountMapping { Key = key, AccountCode = accountCode, AccountNameAr = accountNameAr, AccountNameEn = accountNameEn };
            _db.GlAccountMappings.Add(mapping);
            await _db.SaveChangesAsync(cancellationToken);
            return mapping;
        }

        public async Task<GlExportBatch> GenerateAsync(DateTime periodFromUtc, DateTime periodToUtc, int generatedByUserId, CancellationToken cancellationToken = default)
        {
            var overlap = await _db.GlExportBatches
                .Where(b => b.Status == GlExportBatchStatus.Generated && b.PeriodFromUtc <= periodToUtc && b.PeriodToUtc >= periodFromUtc)
                .Select(b => b.BatchNo).FirstOrDefaultAsync(cancellationToken);
            if (overlap != null)
            {
                throw new GlPeriodOverlapException(overlap);
            }

            // IgnoreQueryFilters, and it is the whole fix for a period that could not be exported at all.
            // FeeCategory is soft-active filtered, so a deactivated category dropped out of this
            // dictionary and its charges fell back to the "Revenue:{id}" key — while the mapping table
            // held the category's own GlExportCode. The keys no longer matched, GenerateAsync refused
            // the whole period, and the only cause was that somebody deactivated a category months
            // after its charges were posted. A category is deactivated to stop new charges, never to
            // strand the ones already in the ledger.
            var categories = await _db.FeeCategories.IgnoreQueryFilters()
                .Where(c => c.SchoolId == _db.CurrentSchoolId)
                .ToDictionaryAsync(c => c.Id, c => c.GlExportCode, cancellationToken);

            // S8/E-801 BR-AYR-009: opening-balance charges and their carry-forward credit notes are a receivable→receivable
            // transfer (Dr/Cr Receivables, nil) — journaling them as revenue + VAT would misstate both, so both halves are skipped.
            var charges = await _db.Charges
                .Where(c => c.Status == ChargeStatus.Posted && c.SourceType != ChargeSourceType.OpeningBalance
                    && c.PostedAtUtc >= periodFromUtc && c.PostedAtUtc <= periodToUtc)
                .Select(c => new { c.FeeCategoryId, c.NetAmount, c.VatAmount, c.GrossAmount }).ToListAsync(cancellationToken);
            var creditNotes = await (
                from n in _db.CreditNotes
                join c in _db.Charges on n.ChargeId equals c.Id
                where !n.IsCarryForward && !n.IsWriteOff && n.IssuedAtUtc >= periodFromUtc && n.IssuedAtUtc <= periodToUtc
                select new { c.FeeCategoryId, n.Amount, VatRate = c.VatRateSnapshot ?? 0m }).ToListAsync(cancellationToken);
            // G-11: joined to the charge for the rate it froze at posting, exactly as credit notes are.
            // A discount reduces a receivable that included VAT, so the VAT has to come back with it.
            var discounts = await (
                from d in _db.DiscountDocuments
                join c in _db.Charges on d.ChargeId equals c.Id
                where d.IssuedAtUtc >= periodFromUtc && d.IssuedAtUtc <= periodToUtc
                select new { d.Amount, VatRate = c.VatRateSnapshot ?? 0m }).ToListAsync(cancellationToken);
            var receipts = await _db.Receipts
                .Where(r => r.Status == ReceiptStatus.Posted && r.Purpose == ReceiptPurpose.FeePayment && r.IssuedAtUtc >= periodFromUtc && r.IssuedAtUtc <= periodToUtc)
                .Select(r => new { r.Id, r.Method, r.Amount }).ToListAsync(cancellationToken);
            // G-10: scoped by when the allocation was made, not by when its receipt was issued. The
            // old query summed every allocation belonging to a receipt of the period, so an October
            // allocation against a September receipt silently changed what September would regenerate
            // as — and never appeared in October at all.
            var allocations = await _db.PaymentAllocations
                .Where(a => a.CreatedAtUtc >= periodFromUtc && a.CreatedAtUtc <= periodToUtc && a.AllocatedAmount != 0m)
                .Select(a => a.AllocatedAmount)
                .ToListAsync(cancellationToken);

            var receiptIds = receipts.Select(r => r.Id).ToList();
            var allocatedByReceipt = (await _db.PaymentAllocations.Where(a => receiptIds.Contains(a.ReceiptId)).Select(a => new { a.ReceiptId, a.AllocatedAmount }).ToListAsync(cancellationToken))
                .GroupBy(a => a.ReceiptId).ToDictionary(g => g.Key, g => g.Sum(x => x.AllocatedAmount));
            var refunds = await _db.RefundVouchers
                .Where(v => v.Status == RefundVoucherStatus.Paid && (v.ModifiedAtUtc ?? v.CreatedAtUtc) >= periodFromUtc && (v.ModifiedAtUtc ?? v.CreatedAtUtc) <= periodToUtc)
                .Where(v => !_db.WalletLedgerEntries.Any(e => e.RefundVoucherId == v.Id))   // wallet refunds journal against WalletLiability, below
                .Select(v => new { v.Method, v.Amount }).ToListAsync(cancellationToken);

            // S6/E-605 BR-CAF-007: wallet money (top-ups, refunds) and cafeteria sales journal separately - wallet liability, cafeteria revenue.
            var walletTopUps = await _db.Receipts
                .Where(r => r.Status == ReceiptStatus.Posted && r.Purpose == ReceiptPurpose.WalletTopUp && r.IssuedAtUtc >= periodFromUtc && r.IssuedAtUtc <= periodToUtc)
                .Select(r => new { r.Method, r.Amount }).ToListAsync(cancellationToken);
            var walletRefunds = await (
                from e in _db.WalletLedgerEntries
                join v in _db.RefundVouchers on e.RefundVoucherId equals v.Id
                where e.Kind == Sms.Domain.Cafeteria.WalletLedgerKind.Refund && v.Status == RefundVoucherStatus.Paid
                      && (v.ModifiedAtUtc ?? v.CreatedAtUtc) >= periodFromUtc && (v.ModifiedAtUtc ?? v.CreatedAtUtc) <= periodToUtc
                select new { v.Method, e.Amount }).ToListAsync(cancellationToken);
            // G-1: wallet-tendered store sales, which reached the ledger nowhere before this. Cash, card
            // and account-charge sales are excluded because they already produce a Charge and a Receipt
            // and would otherwise be counted twice; a wallet sale produces neither.
            var storeWalletSales = await _db.StoreSales
                .Where(s => s.Status == Sms.Domain.Store.StoreSaleStatus.Posted && s.Tender == Sms.Domain.Store.StoreTender.Wallet
                    && s.AtUtc >= periodFromUtc && s.AtUtc <= periodToUtc)
                .Select(s => s.Total).ToListAsync(cancellationToken);

            var cafeteriaSales = await _db.Sales
                .Where(s => s.Status == Sms.Domain.Cafeteria.SaleStatus.Posted && s.Tender != Sms.Domain.Cafeteria.SaleTender.MealPlan && s.AtUtc >= periodFromUtc && s.AtUtc <= periodToUtc)
                .Select(s => new { s.Tender, s.Total }).ToListAsync(cancellationToken);

            // Closed sessions only, and dated by the close rather than the open: the variance does
            // not exist until the drawer is counted, so a session opened in September and closed in
            // October belongs to October (gap G-5).
            var tillVariances = await _db.TillSessions
                .Where(t => t.Status == Sms.Domain.Payments.TillSessionStatus.Closed
                    && t.ClosedAtUtc >= periodFromUtc && t.ClosedAtUtc <= periodToUtc
                    && t.CountedTotal != null && t.SystemTotal != null
                    && t.CountedTotal != t.SystemTotal)
                .Select(t => t.CountedTotal!.Value - t.SystemTotal!.Value)
                .ToListAsync(cancellationToken);

            // G-6: the same document, booked the other way. A write-off says the charge was right
            // and the money is not coming, so revenue stays where it is and the loss lands on bad
            // debt — while an ordinary credit note above says the charge was wrong and takes the
            // revenue and its VAT back out.
            var writeOffs = await _db.CreditNotes
                .Where(n => n.IsWriteOff && n.IssuedAtUtc >= periodFromUtc && n.IssuedAtUtc <= periodToUtc)
                .Select(n => n.Amount)
                .ToListAsync(cancellationToken);

            // G-3: a correction with a mandatory reason that moved a liability and produced no
            // entry. No cash changes hands, so the other side is an adjustments account — and one
            // somebody reads, since every row in it is a balance changed by hand.
            var walletAdjustments = await _db.WalletLedgerEntries
                .Where(e => e.Kind == Sms.Domain.Cafeteria.WalletLedgerKind.Adjustment
                    && e.AtUtc >= periodFromUtc && e.AtUtc <= periodToUtc && e.Amount != 0m)
                .Select(e => e.Amount)
                .ToListAsync(cancellationToken);

            var journal = JournalSummaryBuilder.Build(new JournalSummaryBuilder.PeriodDocuments
            {
                Charges = charges.Select(c => new JournalSummaryBuilder.ChargeDoc(c.FeeCategoryId, categories.TryGetValue(c.FeeCategoryId, out var code) ? code : null, c.NetAmount, c.VatAmount, c.GrossAmount)).ToList(),
                CreditNotes = creditNotes.Select(n => new JournalSummaryBuilder.CreditNoteDoc(n.FeeCategoryId, categories.TryGetValue(n.FeeCategoryId, out var code) ? code : null, n.Amount, n.VatRate)).ToList(),
                Discounts = discounts.Select(d => new JournalSummaryBuilder.DiscountDoc(d.Amount, d.VatRate)).ToList(),
                Receipts = receipts.Select(r => new JournalSummaryBuilder.ReceiptDoc(r.Method.ToString(), r.Amount)).ToList(),
                Allocations = allocations.Select(a => new JournalSummaryBuilder.AllocationDoc(a)).ToList(),
                Refunds = refunds.Select(f => new JournalSummaryBuilder.RefundDoc(f.Method.ToString(), f.Amount)).ToList(),
                WalletTopUps = walletTopUps.Select(w => new JournalSummaryBuilder.WalletTopUpDoc(w.Method.ToString(), w.Amount))
                    .Concat(walletRefunds.Select(w => new JournalSummaryBuilder.WalletTopUpDoc(w.Method.ToString(), w.Amount))).ToList(),
                WalletAdjustments = walletAdjustments.Select(a => new JournalSummaryBuilder.WalletAdjustmentDoc(a)).ToList(),
                CafeteriaSales = cafeteriaSales.Select(s => new JournalSummaryBuilder.CafeteriaSaleDoc(s.Tender == Sms.Domain.Cafeteria.SaleTender.Wallet, s.Total)).ToList(),
                StoreWalletSales = storeWalletSales.Select(t => new JournalSummaryBuilder.StoreWalletSaleDoc(t)).ToList(),
                TillVariances = tillVariances.Select(v => new JournalSummaryBuilder.TillVarianceDoc(v)).ToList(),
                WriteOffs = writeOffs.Select(a => new JournalSummaryBuilder.WriteOffDoc(a)).ToList(),
            });

            var keys = journal.Lines.Select(l => l.AccountKey).Distinct().ToList();
            var mappings = await _db.GlAccountMappings.Where(m => keys.Contains(m.Key)).ToDictionaryAsync(m => m.Key, m => m.AccountCode, cancellationToken);
            var missing = keys.Where(k => !mappings.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
            if (missing.Count > 0)
            {
                throw new GlMappingMissingException(missing);
            }

            var batch = new GlExportBatch
            {
                BatchNo = await _numberIssuer.IssueAsync(BatchSeriesCode, cancellationToken),
                PeriodFromUtc = periodFromUtc, PeriodToUtc = periodToUtc, GeneratedAtUtc = _clock.UtcNow, GeneratedByUserId = generatedByUserId,
                TotalDebit = journal.TotalDebit, TotalCredit = journal.TotalCredit, SourceDocumentCount = journal.SourceDocumentCount,
            };
            var seq = 1;
            foreach (var line in journal.Lines)
            {
                batch.Lines.Add(new GlJournalLine
                {
                    SequenceNumber = seq++, AccountKey = line.AccountKey, AccountCode = mappings[line.AccountKey], Description = line.Description,
                    Debit = line.Debit, Credit = line.Credit, SourceDocumentCount = line.SourceDocumentCount,
                });
            }

            batch.ContentHash = Hash(Render(batch));
            _db.GlExportBatches.Add(batch);
            await _db.SaveChangesAsync(cancellationToken);

            // Saved before posting, deliberately. If the ledger refuses — a closed period, an account
            // that is not postable — the batch still exists, still holds the period against a second
            // attempt, and can be voided and regenerated once the configuration is fixed. Posting first
            // and saving after would leave a ledger entry with no batch behind it, which is the one
            // outcome that cannot be reconciled afterwards.
            if (_posting != null)
            {
                var outcome = await _posting.PostBatchAsync(batch, cancellationToken);
                if (!outcome.Success)
                {
                    throw new GlPostingRejectedException(batch.BatchNo, outcome.ErrorCode!, outcome.ErrorMessage!);
                }

                batch.PostedJournalNo = outcome.DocumentNumber;
                await _db.SaveChangesAsync(cancellationToken);
            }

            return batch;
        }

        public async Task<string> RenderCsvAsync(int glExportBatchId, CancellationToken cancellationToken = default)
        {
            var batch = await _db.GlExportBatches.Include(b => b.Lines).SingleAsync(b => b.Id == glExportBatchId, cancellationToken);
            return Render(batch);
        }

        public async Task VoidAsync(int glExportBatchId, string reason, CancellationToken cancellationToken = default)
        {
            var batch = await _db.GlExportBatches.SingleAsync(b => b.Id == glExportBatchId, cancellationToken);
            if (batch.Status != GlExportBatchStatus.Generated)
            {
                throw new GlBatchNotGeneratedException(glExportBatchId);
            }

            // A batch that reached the ledger cannot simply be marked void here: the entry it produced
            // is immutable and still in the trial balance. The reversing entry has to land first, and
            // if the ledger refuses it the batch stays Generated — because a voided batch frees its
            // period for regeneration, and freeing it while the original entry still stands would
            // double every figure in it.
            if (_posting != null && batch.PostedJournalNo != null && batch.ReversalJournalNo == null)
            {
                await _db.Entry(batch).Collection(b => b.Lines).LoadAsync(cancellationToken);
                var outcome = await _posting.ReverseBatchAsync(batch, reason, cancellationToken);
                if (!outcome.Success)
                {
                    throw new GlPostingRejectedException(batch.BatchNo, outcome.ErrorCode!, outcome.ErrorMessage!);
                }

                batch.ReversalJournalNo = outcome.DocumentNumber;
            }

            _audit.Reason = reason;
            batch.Status = GlExportBatchStatus.Voided;
            batch.VoidReason = reason;
            await _db.SaveChangesAsync(cancellationToken);
        }

        private static string Render(GlExportBatch batch) => CsvJournalWriter.Render(
            batch.BatchNo, batch.PeriodFromUtc, batch.PeriodToUtc,
            batch.Lines.OrderBy(l => l.SequenceNumber).Select(l => new CsvJournalWriter.Row(l.SequenceNumber, l.AccountCode, l.AccountKey, l.Description, l.Debit, l.Credit, l.SourceDocumentCount)));

        public static string Hash(string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }
}
