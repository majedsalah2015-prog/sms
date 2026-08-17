using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Fees;
using Sms.Application.Numbering;
using Sms.Application.Store;
using Sms.Domain.Cafeteria;
using Sms.Domain.Payments;
using Sms.Domain.Store;
using Sms.Domain.Students;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Store
{
    /// <summary>Standalone — saves itself. Every sale is a Module 19 charge; cash/card add a Module 21 receipt allocated to it (single money truth, BR-STO-003).</summary>
    public class StoreAdmin : IStoreAdmin
    {
        private readonly AppDbContext _db;
        private readonly INumberIssuer _numberIssuer;
        private readonly IClock _clock;
        private readonly IAuditContext _audit;
        private readonly IWorkingYearContext _workingYear;
        private readonly IFeeAdmin _feeAdmin;

        public StoreAdmin(AppDbContext db, INumberIssuer numberIssuer, IClock clock, IAuditContext audit, IWorkingYearContext workingYear, IFeeAdmin feeAdmin)
        {
            _db = db;
            _numberIssuer = numberIssuer;
            _clock = clock;
            _audit = audit;
            _workingYear = workingYear;
            _feeAdmin = feeAdmin;
        }

        // ------------------------------------------------------------------ catalog + policies

        public async Task<StoreItem> DefineItemAsync(string nameAr, string nameEn, StoreItemCategory category, int feeCategoryId, IReadOnlyList<VariantInput> variants, CancellationToken cancellationToken = default)
        {
            var item = new StoreItem { NameAr = nameAr, NameEn = nameEn, Category = category, FeeCategoryId = feeCategoryId };
            foreach (var v in variants)
            {
                item.Variants.Add(new StoreVariant { Sku = v.Sku, Barcode = v.Barcode, Size = v.Size, Color = v.Color, LowStockThreshold = v.LowStockThreshold });
            }

            _db.StoreItems.Add(item);
            await _db.SaveChangesAsync(cancellationToken);
            return item;
        }

        public async Task<PriceList> PublishPriceListAsync(DateTime effectiveFrom, IReadOnlyList<(int StoreItemId, decimal Price)> prices, CancellationToken cancellationToken = default)
        {
            var version = (await _db.PriceLists.MaxAsync(p => (int?)p.Version, cancellationToken) ?? 0) + 1;
            var list = new PriceList { Version = version, EffectiveFrom = effectiveFrom.Date };
            foreach (var (itemId, price) in prices)
            {
                list.Lines.Add(new PriceListLine { StoreItemId = itemId, Price = price });
            }

            _db.PriceLists.Add(list);
            await _db.SaveChangesAsync(cancellationToken);
            return list;
        }

        public async Task SetAccountChargePolicyAsync(StoreItemCategory category, bool isAllowed, decimal? capPerSale, CancellationToken cancellationToken = default)
        {
            var policy = await _db.AccountChargePolicies.SingleOrDefaultAsync(p => p.Category == category, cancellationToken) ?? _db.AccountChargePolicies.Add(new AccountChargePolicy { Category = category }).Entity;
            policy.IsAllowed = isAllowed;
            policy.CapPerSale = capPerSale;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task SetReturnPolicyAsync(StoreItemCategory category, int windowDays, bool sealedOnly, CancellationToken cancellationToken = default)
        {
            var policy = await _db.ReturnPolicies.SingleOrDefaultAsync(p => p.Category == category, cancellationToken) ?? _db.ReturnPolicies.Add(new ReturnPolicy { Category = category }).Entity;
            policy.WindowDays = windowDays;
            policy.SealedOnly = sealedOnly;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // ------------------------------------------------------------------ stock

        public async Task ReceiveStockAsync(int storeVariantId, int quantity, CancellationToken cancellationToken = default)
        {
            _db.StoreStockMovements.Add(new StoreStockMovement { StoreVariantId = storeVariantId, Kind = StoreStockKind.Receive, Quantity = quantity, AtUtc = _clock.UtcNow });
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<int> StockLevelAsync(int storeVariantId, CancellationToken cancellationToken = default)
            => StoreStockPolicy.Level(await _db.StoreStockMovements.Where(m => m.StoreVariantId == storeVariantId).Select(m => m.Quantity).ToListAsync(cancellationToken));

        public async Task<IReadOnlyList<ReorderLine>> ReorderReportAsync(CancellationToken cancellationToken = default)
        {
            var variants = await _db.StoreVariants.Where(v => v.IsActive).ToListAsync(cancellationToken);
            var levels = (await _db.StoreStockMovements.Select(m => new { m.StoreVariantId, m.Quantity }).ToListAsync(cancellationToken))
                .GroupBy(m => m.StoreVariantId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            return variants
                .Select(v => new ReorderLine(v.Id, v.Sku, levels.TryGetValue(v.Id, out var l) ? l : 0, v.LowStockThreshold))
                .Where(r => StoreStockPolicy.IsLow(r.Level, r.Threshold))
                .OrderBy(r => r.Sku).ToList();
        }

        private async Task DeductStockAsync(int variantId, int quantity, StoreStockKind kind, string? reason, CancellationToken cancellationToken)
        {
            if (!StoreStockPolicy.CanDeduct(await StockLevelAsync(variantId, cancellationToken), quantity))
            {
                throw new StoreStockInsufficientException(variantId);
            }

            _db.StoreStockMovements.Add(new StoreStockMovement { StoreVariantId = variantId, Kind = kind, Quantity = -quantity, Reason = reason, AtUtc = _clock.UtcNow });
        }

        // ------------------------------------------------------------------ POS

        private async Task<decimal> PriceForAsync(int storeItemId, DateTime onDate, CancellationToken cancellationToken)
        {
            var prices = await (
                from l in _db.PriceListLines
                join p in _db.PriceLists on l.PriceListId equals p.Id
                where l.StoreItemId == storeItemId && p.IsActive
                select new PriceResolver.ListPrice(p.Version, p.EffectiveFrom, l.Price)).ToListAsync(cancellationToken);
            return PriceResolver.Resolve(prices, onDate) ?? throw new StorePriceMissingException(storeItemId);
        }

        public async Task<StoreSale> RecordSaleAsync(
            int payerId, IReadOnlyList<StoreBasketLine> basket, StoreTender tender, int operatorUserId, int? studentId = null,
            int? tillSessionId = null, bool allowWalletTender = true, string? financeOverrideReason = null, CancellationToken cancellationToken = default)
        {
            var now = _clock.UtcNow;
            var variantIds = basket.Select(b => b.StoreVariantId).ToList();
            var variants = await _db.StoreVariants.Where(v => variantIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, cancellationToken);
            var itemIds = variants.Values.Select(v => v.StoreItemId).Distinct().ToList();
            var items = await _db.StoreItems.Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);

            var lines = new List<StoreSaleLine>();
            foreach (var b in basket)
            {
                var variant = variants[b.StoreVariantId];
                var price = await PriceForAsync(variant.StoreItemId, now, cancellationToken);
                lines.Add(new StoreSaleLine { StoreVariantId = variant.Id, Quantity = b.Quantity, UnitPrice = price, LineTotal = price * b.Quantity });
            }

            var total = lines.Sum(l => l.LineTotal);
            var categories = variants.Values.Select(v => items[v.StoreItemId].Category).Distinct().ToList();
            var chargeCategoryId = items[variants[basket[0].StoreVariantId].StoreItemId].FeeCategoryId;   // VAT class of the first item's category (mixed-category baskets: first wins, flagged)

            string? overrideReason = null;
            if (tender == StoreTender.AccountCharge)
            {
                foreach (var category in categories)
                {
                    var policy = await _db.AccountChargePolicies.SingleOrDefaultAsync(p => p.Category == category, cancellationToken);
                    var verdict = AccountChargeEvaluator.Evaluate(policy?.IsAllowed ?? false, policy?.CapPerSale, total);
                    if (!verdict.Allowed)
                    {
                        throw new AccountChargeNotAllowedException($"category {category} disabled");
                    }

                    if (verdict.NeedsFinanceOverride)
                    {
                        if (string.IsNullOrWhiteSpace(financeOverrideReason))
                        {
                            throw new AccountChargeNotAllowedException($"total {total} exceeds the {category} cap — Finance (P2) override required");
                        }

                        overrideReason = financeOverrideReason;
                    }
                }
            }

            if (tender is StoreTender.Cash or StoreTender.Card)
            {
                if (!tillSessionId.HasValue || (await _db.TillSessions.SingleAsync(s => s.Id == tillSessionId.Value, cancellationToken)).Status != TillSessionStatus.Open)
                {
                    throw new StoreTenderRejectedException("cash/card sales need an open till session (BR-PAY-001)");
                }
            }

            Wallet? wallet = null;
            if (tender == StoreTender.Wallet)
            {
                if (!allowWalletTender || !studentId.HasValue)
                {
                    throw new StoreTenderRejectedException("wallet tender disabled or no student");
                }

                wallet = await _db.Wallets.SingleOrDefaultAsync(w => w.HolderKind == WalletHolderKind.Student && w.HolderId == studentId.Value, cancellationToken) ?? throw new StoreTenderRejectedException("no wallet");
                var balance = (await _db.WalletLedgerEntries.Where(e => e.WalletId == wallet.Id).Select(e => e.Amount).ToListAsync(cancellationToken)).Sum();
                if (balance - total < -wallet.OverdraftAllowance)
                {
                    throw new StoreTenderRejectedException("insufficient wallet balance");
                }
            }

            if (tender != StoreTender.Wallet && !studentId.HasValue)
            {
                // Charges post against a student's payer (Module 19 model); anonymous walk-in cash needs a "walk-in" payer model - flagged, not built.
                throw new StoreTenderRejectedException("charge-backed tenders need a student");
            }

            foreach (var line in lines)
            {
                await DeductStockAsync(line.StoreVariantId, line.Quantity, StoreStockKind.Sell, null, cancellationToken);
            }

            var sale = new StoreSale { StudentId = studentId, PayerId = payerId, Tender = tender, TillSessionId = tillSessionId, OperatorUserId = operatorUserId, AtUtc = now, Total = total, FinanceOverrideReason = overrideReason, Lines = lines };
            _db.StoreSales.Add(sale);
            await _db.SaveChangesAsync(cancellationToken);

            if (tender == StoreTender.Wallet)
            {
                _db.WalletLedgerEntries.Add(new WalletLedgerEntry { WalletId = wallet!.Id, Kind = WalletLedgerKind.Sale, Amount = -total, Reason = $"store sale {sale.Id}", AtUtc = now });
            }
            else
            {
                var charge = await _feeAdmin.PostManualChargeAsync(studentId!.Value, payerId, chargeCategoryId, total, cancellationToken);
                sale.ChargeId = charge.Id;
                if (tender is StoreTender.Cash or StoreTender.Card)
                {
                    // Strict-receipted: a Module 21 receipt allocated to THIS charge (not oldest-first across the payer's fees).
                    var receipt = new Receipt
                    {
                        PayerId = payerId, TillSessionId = tillSessionId, ReceiptNo = await _numberIssuer.IssueAsync("RCP", cancellationToken),
                        Method = tender == StoreTender.Cash ? PaymentMethod.Cash : PaymentMethod.Card, MethodRefNo = $"STORE:{sale.Id}", Amount = charge.GrossAmount,
                        Status = ReceiptStatus.Posted, IssuedAtUtc = now, Purpose = ReceiptPurpose.FeePayment,
                    };
                    _db.Receipts.Add(receipt);
                    await _db.SaveChangesAsync(cancellationToken);
                    _db.PaymentAllocations.Add(new PaymentAllocation { ReceiptId = receipt.Id, ChargeId = charge.Id, AllocatedAmount = charge.GrossAmount });
                    sale.ReceiptId = receipt.Id;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            return sale;
        }

        public async Task VoidSaleAsync(int storeSaleId, string reason, CancellationToken cancellationToken = default)
        {
            var sale = await _db.StoreSales.Include(s => s.Lines).SingleAsync(s => s.Id == storeSaleId, cancellationToken);
            var tillOpen = !sale.TillSessionId.HasValue || (await _db.TillSessions.SingleAsync(s => s.Id == sale.TillSessionId.Value, cancellationToken)).Status == TillSessionStatus.Open;
            if (sale.Status != StoreSaleStatus.Posted || !tillOpen)
            {
                throw new StoreSaleNotVoidableException(storeSaleId);
            }

            _audit.Reason = reason;
            sale.Status = StoreSaleStatus.Voided;
            sale.VoidReason = reason;
            foreach (var line in sale.Lines)
            {
                _db.StoreStockMovements.Add(new StoreStockMovement { StoreVariantId = line.StoreVariantId, Kind = StoreStockKind.ReturnIn, Quantity = line.Quantity, Reason = $"void of sale {storeSaleId}", AtUtc = _clock.UtcNow });
            }

            if (sale.Tender == StoreTender.Wallet)
            {
                var wallet = await _db.Wallets.SingleAsync(w => w.HolderKind == WalletHolderKind.Student && w.HolderId == sale.StudentId!.Value, cancellationToken);
                _db.WalletLedgerEntries.Add(new WalletLedgerEntry { WalletId = wallet.Id, Kind = WalletLedgerKind.SaleVoid, Amount = sale.Total, Reason = reason, AtUtc = _clock.UtcNow });
            }
            else if (sale.ChargeId.HasValue)
            {
                var charge = await _db.Charges.SingleAsync(c => c.Id == sale.ChargeId.Value, cancellationToken);
                await _feeAdmin.IssueCreditNoteAsync(sale.ChargeId.Value, charge.GrossAmount, $"store sale {storeSaleId} voided: {reason}", cancellationToken);
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<ReturnExchange> ReturnOrExchangeAsync(int storeSaleLineId, ReturnKind kind, int quantity, bool isSealed, int? newStoreVariantId = null, CancellationToken cancellationToken = default)
        {
            var line = await _db.StoreSaleLines.SingleAsync(l => l.Id == storeSaleLineId, cancellationToken);
            var sale = await _db.StoreSales.SingleAsync(s => s.Id == line.StoreSaleId, cancellationToken);
            var variant = await _db.StoreVariants.SingleAsync(v => v.Id == line.StoreVariantId, cancellationToken);
            var item = await _db.StoreItems.SingleAsync(i => i.Id == variant.StoreItemId, cancellationToken);
            var policy = await _db.ReturnPolicies.SingleOrDefaultAsync(p => p.Category == item.Category, cancellationToken) ?? new ReturnPolicy();
            var alreadyReturned = await _db.ReturnExchanges.Where(r => r.StoreSaleLineId == storeSaleLineId).Select(r => r.Quantity).ToListAsync(cancellationToken);
            if (quantity <= 0 || alreadyReturned.Sum() + quantity > line.Quantity || !ReturnPolicyEvaluator.CanReturn(sale.AtUtc, _clock.UtcNow, policy.WindowDays, policy.SealedOnly && kind == ReturnKind.Return, isSealed))
            {
                throw new ReturnNotAllowedException(storeSaleLineId);
            }

            var now = _clock.UtcNow;
            _db.StoreStockMovements.Add(new StoreStockMovement { StoreVariantId = line.StoreVariantId, Kind = StoreStockKind.ReturnIn, Quantity = quantity, Reason = $"{kind} on sale line {storeSaleLineId}", AtUtc = now });
            var record = new ReturnExchange { StoreSaleLineId = storeSaleLineId, Kind = kind, Quantity = quantity, IsSealed = isSealed, NewStoreVariantId = newStoreVariantId, AtUtc = now };

            if (kind == ReturnKind.Exchange)
            {
                var newVariant = await _db.StoreVariants.SingleAsync(v => v.Id == newStoreVariantId!.Value, cancellationToken);
                if (newVariant.StoreItemId != variant.StoreItemId)
                {
                    throw new ReturnNotAllowedException(storeSaleLineId);   // exchanges are size/color swaps within the same item - free
                }

                await DeductStockAsync(newVariant.Id, quantity, StoreStockKind.ExchangeOut, $"exchange for sale line {storeSaleLineId}", cancellationToken);
            }
            else if (sale.ChargeId.HasValue)
            {
                // Credit the returned share of the charge at gross (the charge carries the category's VAT).
                var charge = await _db.Charges.SingleAsync(c => c.Id == sale.ChargeId.Value, cancellationToken);
                var grossShare = charge.NetAmount == 0m ? 0m : Math.Round(charge.GrossAmount * (line.UnitPrice * quantity / charge.NetAmount), 2, MidpointRounding.AwayFromZero);
                var creditNote = await _feeAdmin.IssueCreditNoteAsync(sale.ChargeId.Value, grossShare, $"store return on sale line {storeSaleLineId}", cancellationToken);
                record.CreditNoteId = creditNote.Id;
            }
            else if (sale.Tender == StoreTender.Wallet)
            {
                var wallet = await _db.Wallets.SingleAsync(w => w.HolderKind == WalletHolderKind.Student && w.HolderId == sale.StudentId!.Value, cancellationToken);
                _db.WalletLedgerEntries.Add(new WalletLedgerEntry { WalletId = wallet.Id, Kind = WalletLedgerKind.SaleVoid, Amount = line.UnitPrice * quantity, Reason = $"store return on sale line {storeSaleLineId}", AtUtc = now });
            }

            _db.ReturnExchanges.Add(record);
            await _db.SaveChangesAsync(cancellationToken);
            return record;
        }

        // ------------------------------------------------------------------ bundles + distribution

        public async Task<Bundle> DefineBundleAsync(string nameAr, string nameEn, int gradeYearProfileId, int feeCategoryId, decimal price, BundleChargeMode chargeMode, IReadOnlyList<BundleLineInput> lines, CancellationToken cancellationToken = default)
        {
            var bundle = new Bundle { NameAr = nameAr, NameEn = nameEn, GradeYearProfileId = gradeYearProfileId, FeeCategoryId = feeCategoryId, Price = price, ChargeMode = chargeMode };
            foreach (var l in lines)
            {
                bundle.Lines.Add(new BundleLine { StoreItemId = l.StoreItemId, Quantity = l.Quantity });
            }

            _db.Bundles.Add(bundle);
            await _db.SaveChangesAsync(cancellationToken);
            return bundle;
        }

        private async Task<int?> PayerForStudentAsync(int studentId, CancellationToken cancellationToken)
        {
            var parentIds = await _db.StudentGuardianLinks.Where(l => l.StudentId == studentId && l.IsFinanciallyResponsible && l.EffectiveToUtc == null).Select(l => l.ParentId).ToListAsync(cancellationToken);
            return await _db.Payers.Where(p => p.ParentId != null && parentIds.Contains(p.ParentId.Value)).OrderBy(p => p.Id).Select(p => (int?)p.Id).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<BundleAssignment>> AssignBundleBatchAsync(int bundleId, CancellationToken cancellationToken = default)
        {
            var bundle = await _db.Bundles.SingleAsync(b => b.Id == bundleId, cancellationToken);
            var studentIds = await _db.Enrollments
                .Where(e => e.GradeYearProfileId == bundle.GradeYearProfileId && e.AcademicYearId == _workingYear.AcademicYearId && e.Status == EnrollmentStatus.Active)
                .Select(e => e.StudentId).ToListAsync(cancellationToken);
            var existing = await _db.BundleAssignments.Where(a => a.BundleId == bundleId).Select(a => a.StudentId).ToListAsync(cancellationToken);

            var assignments = new List<BundleAssignment>();
            foreach (var studentId in studentIds.Except(existing).OrderBy(id => id))
            {
                var payerId = await PayerForStudentAsync(studentId, cancellationToken);
                if (payerId == null)
                {
                    continue;
                }

                var assignment = new BundleAssignment { BundleId = bundleId, StudentId = studentId, PayerId = payerId.Value };
                _db.BundleAssignments.Add(assignment);
                await _db.SaveChangesAsync(cancellationToken);
                if (bundle.ChargeMode != BundleChargeMode.AtHandout)
                {
                    await ChargeAssignmentAsync(bundle, assignment, cancellationToken);
                }

                assignments.Add(assignment);
            }

            await _db.SaveChangesAsync(cancellationToken);
            return assignments;
        }

        private async Task ChargeAssignmentAsync(Bundle bundle, BundleAssignment assignment, CancellationToken cancellationToken)
        {
            var charge = await _feeAdmin.PostManualChargeAsync(assignment.StudentId, assignment.PayerId, bundle.FeeCategoryId, bundle.Price, cancellationToken);
            assignment.ChargeId = charge.Id;
            assignment.Status = BundleAssignmentStatus.Charged;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<DistributionSession> OpenDistributionAsync(int bundleId, DateTime date, CancellationToken cancellationToken = default)
        {
            var session = new DistributionSession { BundleId = bundleId, Date = date.Date };
            _db.DistributionSessions.Add(session);
            await _db.SaveChangesAsync(cancellationToken);
            return session;
        }

        public async Task<HandoutRecord> HandOutAsync(int distributionSessionId, int bundleAssignmentId, int bundleLineId, int storeVariantId, int quantity, bool acknowledged, bool requireChargedFirst = true, CancellationToken cancellationToken = default)
        {
            var assignment = await _db.BundleAssignments.SingleAsync(a => a.Id == bundleAssignmentId, cancellationToken);
            var bundle = await _db.Bundles.Include(b => b.Lines).SingleAsync(b => b.Id == assignment.BundleId, cancellationToken);
            if (assignment.Status == BundleAssignmentStatus.Assigned)
            {
                if (bundle.ChargeMode == BundleChargeMode.AtHandout)
                {
                    await ChargeAssignmentAsync(bundle, assignment, cancellationToken);
                }
                else if (requireChargedFirst)
                {
                    throw new HandoutBeforeChargeException(bundleAssignmentId);
                }
            }

            await DeductStockAsync(storeVariantId, quantity, StoreStockKind.Handout, $"bundle {bundle.Id} handout", cancellationToken);
            var record = new HandoutRecord { DistributionSessionId = distributionSessionId, BundleAssignmentId = bundleAssignmentId, BundleLineId = bundleLineId, StoreVariantId = storeVariantId, Quantity = quantity, ReceivedAtUtc = _clock.UtcNow, Acknowledged = acknowledged };
            _db.HandoutRecords.Add(record);
            await _db.SaveChangesAsync(cancellationToken);

            var handed = await _db.HandoutRecords.Where(h => h.BundleAssignmentId == bundleAssignmentId).Select(h => new { h.BundleLineId, h.Quantity }).ToListAsync(cancellationToken);
            var progress = bundle.Lines.Select(l => new HandoutCompletionEvaluator.LineProgress(l.Id, l.Quantity, handed.Where(h => h.BundleLineId == l.Id).Sum(h => h.Quantity))).ToList();
            if (HandoutCompletionEvaluator.IsComplete(progress) && assignment.Status == BundleAssignmentStatus.Charged)
            {
                assignment.Status = BundleAssignmentStatus.Distributed;
                await _db.SaveChangesAsync(cancellationToken);
            }

            return record;
        }

        public async Task<IReadOnlyList<BundleAssignment>> UndistributedPaidAsync(int bundleId, CancellationToken cancellationToken = default)
            => await _db.BundleAssignments.Where(a => a.BundleId == bundleId && a.Status == BundleAssignmentStatus.Charged).OrderBy(a => a.StudentId).ToListAsync(cancellationToken);

        public async Task ResolveUndistributedAtWithdrawalAsync(int bundleAssignmentId, CancellationToken cancellationToken = default)
        {
            var assignment = await _db.BundleAssignments.SingleAsync(a => a.Id == bundleAssignmentId, cancellationToken);
            if (assignment.Status == BundleAssignmentStatus.Charged && assignment.ChargeId.HasValue)
            {
                var bundle = await _db.Bundles.SingleAsync(b => b.Id == assignment.BundleId, cancellationToken);
                var creditNote = await _feeAdmin.IssueCreditNoteAsync(assignment.ChargeId.Value, bundle.Price, "undistributed bundle credited at withdrawal (BR-STO-007)", cancellationToken);
                assignment.CreditNoteId = creditNote.Id;
            }

            assignment.Status = BundleAssignmentStatus.Credited;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
