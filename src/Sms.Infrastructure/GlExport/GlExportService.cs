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

        public GlExportService(AppDbContext db, INumberIssuer numberIssuer, IClock clock, IAuditContext audit)
        {
            _db = db;
            _numberIssuer = numberIssuer;
            _clock = clock;
            _audit = audit;
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

            // Active categories only (ISoftActiveFiltered) - a deactivated category's charges fall back to the "Revenue:{id}" key,
            // which the mapping table can still name explicitly.
            var categories = await _db.FeeCategories.ToDictionaryAsync(c => c.Id, c => c.GlExportCode, cancellationToken);

            var charges = await _db.Charges
                .Where(c => c.Status == ChargeStatus.Posted && c.PostedAtUtc >= periodFromUtc && c.PostedAtUtc <= periodToUtc)
                .Select(c => new { c.FeeCategoryId, c.NetAmount, c.VatAmount, c.GrossAmount }).ToListAsync(cancellationToken);
            var creditNotes = await (
                from n in _db.CreditNotes
                join c in _db.Charges on n.ChargeId equals c.Id
                where n.IssuedAtUtc >= periodFromUtc && n.IssuedAtUtc <= periodToUtc
                select new { c.FeeCategoryId, n.Amount, VatRate = c.VatRateSnapshot ?? 0m }).ToListAsync(cancellationToken);
            var discounts = await _db.DiscountDocuments
                .Where(d => d.IssuedAtUtc >= periodFromUtc && d.IssuedAtUtc <= periodToUtc)
                .Select(d => d.Amount).ToListAsync(cancellationToken);
            var receipts = await _db.Receipts
                .Where(r => r.Status == ReceiptStatus.Posted && r.Purpose == ReceiptPurpose.FeePayment && r.IssuedAtUtc >= periodFromUtc && r.IssuedAtUtc <= periodToUtc)
                .Select(r => new { r.Id, r.Method, r.Amount }).ToListAsync(cancellationToken);
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
            var cafeteriaSales = await _db.Sales
                .Where(s => s.Status == Sms.Domain.Cafeteria.SaleStatus.Posted && s.Tender != Sms.Domain.Cafeteria.SaleTender.MealPlan && s.AtUtc >= periodFromUtc && s.AtUtc <= periodToUtc)
                .Select(s => new { s.Tender, s.Total }).ToListAsync(cancellationToken);

            var journal = JournalSummaryBuilder.Build(
                charges.Select(c => new JournalSummaryBuilder.ChargeDoc(c.FeeCategoryId, categories.TryGetValue(c.FeeCategoryId, out var code) ? code : null, c.NetAmount, c.VatAmount, c.GrossAmount)).ToList(),
                creditNotes.Select(n => new JournalSummaryBuilder.CreditNoteDoc(n.FeeCategoryId, categories.TryGetValue(n.FeeCategoryId, out var code) ? code : null, n.Amount, n.VatRate)).ToList(),
                discounts.Select(d => new JournalSummaryBuilder.DiscountDoc(d)).ToList(),
                receipts.Select(r => new JournalSummaryBuilder.ReceiptDoc(r.Method.ToString(), r.Amount, allocatedByReceipt.TryGetValue(r.Id, out var a) ? a : 0m)).ToList(),
                refunds.Select(f => new JournalSummaryBuilder.RefundDoc(f.Method.ToString(), f.Amount)).ToList(),
                walletTopUps.Select(w => new JournalSummaryBuilder.WalletTopUpDoc(w.Method.ToString(), w.Amount))
                    .Concat(walletRefunds.Select(w => new JournalSummaryBuilder.WalletTopUpDoc(w.Method.ToString(), w.Amount))).ToList(),
                cafeteriaSales.Select(s => new JournalSummaryBuilder.CafeteriaSaleDoc(s.Tender == Sms.Domain.Cafeteria.SaleTender.Wallet, s.Total)).ToList());

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
