using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Cafeteria;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Fees;
using Sms.Application.Health;
using Sms.Application.Numbering;
using Sms.Domain.Audit;
using Sms.Domain.Cafeteria;
using Sms.Domain.Payments;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Cafeteria
{
    /// <summary>Standalone — saves itself. POS is the P1 speed path: RecordSaleAsync is one unit of work with real-time controls.</summary>
    public class CafeteriaAdmin : ICafeteriaAdmin
    {
        private readonly AppDbContext _db;
        private readonly INumberIssuer _numberIssuer;
        private readonly IClock _clock;
        private readonly IAuditContext _audit;
        private readonly IAuditEventWriter _auditEvents;
        private readonly IFeeAdmin _feeAdmin;
        private readonly IHealthAdmin _health;

        public CafeteriaAdmin(AppDbContext db, INumberIssuer numberIssuer, IClock clock, IAuditContext audit, IAuditEventWriter auditEvents, IFeeAdmin feeAdmin, IHealthAdmin health)
        {
            _db = db;
            _numberIssuer = numberIssuer;
            _clock = clock;
            _audit = audit;
            _auditEvents = auditEvents;
            _feeAdmin = feeAdmin;
            _health = health;
        }

        // ------------------------------------------------------------------ items + menus

        public async Task<CafeteriaItem> DefineItemAsync(string nameAr, string nameEn, string category, decimal price, NutritionClass nutritionClass, string? allergenTags = null, bool isStaffOnly = false, CancellationToken cancellationToken = default)
        {
            var item = new CafeteriaItem { NameAr = nameAr, NameEn = nameEn, Category = category, Price = price, NutritionClass = nutritionClass, AllergenTags = allergenTags, IsStaffOnly = isStaffOnly };
            _db.CafeteriaItems.Add(item);
            await _db.SaveChangesAsync(cancellationToken);
            return item;
        }

        public async Task<Menu> DefineMenuAsync(DateTime date, IReadOnlyList<int> itemIds, bool publish = true, CancellationToken cancellationToken = default)
        {
            var items = await _db.CafeteriaItems.Where(i => itemIds.Contains(i.Id)).ToListAsync(cancellationToken);
            foreach (var item in items.Where(i => !NutritionPolicy.AllowedOnStudentMenu(i.NutritionClass, i.IsStaffOnly)))
            {
                throw new BannedItemOnMenuException(item.Id);
            }

            var menu = await _db.Menus.Include(m => m.Lines).SingleOrDefaultAsync(m => m.Date == date.Date, cancellationToken);
            if (menu == null)
            {
                menu = new Menu { Date = date.Date };
                _db.Menus.Add(menu);
            }
            else
            {
                _db.MenuLines.RemoveRange(menu.Lines);
                menu.Lines.Clear();
            }

            foreach (var id in itemIds.Distinct())
            {
                menu.Lines.Add(new MenuLine { CafeteriaItemId = id });
            }

            menu.IsPublished = publish;
            await _db.SaveChangesAsync(cancellationToken);
            return menu;
        }

        // ------------------------------------------------------------------ wallets

        public async Task<Wallet> EnsureWalletAsync(WalletHolderKind holderKind, int holderId, decimal overdraftAllowance = 0m, CancellationToken cancellationToken = default)
        {
            var wallet = await _db.Wallets.SingleOrDefaultAsync(w => w.HolderKind == holderKind && w.HolderId == holderId, cancellationToken);
            if (wallet != null)
            {
                return wallet;
            }

            wallet = new Wallet { HolderKind = holderKind, HolderId = holderId, OverdraftAllowance = overdraftAllowance };
            _db.Wallets.Add(wallet);
            await _db.SaveChangesAsync(cancellationToken);
            return wallet;
        }

        public async Task<decimal> BalanceAsync(int walletId, CancellationToken cancellationToken = default)
            => WalletBalanceCalculator.Balance(await _db.WalletLedgerEntries.Where(e => e.WalletId == walletId).Select(e => e.Amount).ToListAsync(cancellationToken));

        public async Task<Receipt> TopUpAsync(int walletId, int payerId, PaymentMethod method, decimal amount, int? tillSessionId = null, CancellationToken cancellationToken = default)
        {
            if (tillSessionId.HasValue)
            {
                var session = await _db.TillSessions.SingleAsync(s => s.Id == tillSessionId.Value, cancellationToken);
                if (session.Status != TillSessionStatus.Open)
                {
                    throw new TillSessionNotOpenException(tillSessionId.Value);
                }
            }

            // BR-CAF-001/007: top-ups only via numbered Module 21 receipts - Purpose keeps them out of fee allocation and the fee advance.
            var receipt = new Receipt
            {
                PayerId = payerId, TillSessionId = tillSessionId, ReceiptNo = await _numberIssuer.IssueAsync("RCP", cancellationToken), Method = method,
                MethodRefNo = $"WALLET:{walletId}", Amount = amount, Status = ReceiptStatus.Posted, IssuedAtUtc = _clock.UtcNow, Purpose = ReceiptPurpose.WalletTopUp,
            };
            _db.Receipts.Add(receipt);
            await _db.SaveChangesAsync(cancellationToken);
            _db.WalletLedgerEntries.Add(new WalletLedgerEntry { WalletId = walletId, Kind = WalletLedgerKind.TopUp, Amount = amount, ReceiptId = receipt.Id, AtUtc = _clock.UtcNow });
            await _db.SaveChangesAsync(cancellationToken);
            return receipt;
        }

        public async Task AdjustAsync(int walletId, decimal signedAmount, string reason, int actorUserId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new WalletAdjustmentReasonRequiredException(walletId);
            }

            _db.WalletLedgerEntries.Add(new WalletLedgerEntry { WalletId = walletId, Kind = WalletLedgerKind.Adjustment, Amount = signedAmount, Reason = reason, AtUtc = _clock.UtcNow });
            _auditEvents.Log(AuditAction.Update, nameof(Wallet), walletId, reason: reason);   // BR-CAF-009: documented correction, T1 event
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<RefundVoucher> RefundBalanceAsync(int walletId, int payerId, PaymentMethod method, string reason, CancellationToken cancellationToken = default)
        {
            var balance = await BalanceAsync(walletId, cancellationToken);
            if (balance <= 0m)
            {
                throw new WalletBalanceNotRefundableException(walletId);
            }

            var voucher = new RefundVoucher { PayerId = payerId, VoucherNo = await _numberIssuer.IssueAsync("RFD", cancellationToken), Amount = balance, Method = method, Reason = $"cafeteria wallet {walletId}: {reason}" };
            _db.RefundVouchers.Add(voucher);
            await _db.SaveChangesAsync(cancellationToken);
            _db.WalletLedgerEntries.Add(new WalletLedgerEntry { WalletId = walletId, Kind = WalletLedgerKind.Refund, Amount = -balance, RefundVoucherId = voucher.Id, Reason = reason, AtUtc = _clock.UtcNow });
            await _db.SaveChangesAsync(cancellationToken);
            return voucher;
        }

        public async Task<SpendControl> SetSpendControlAsync(int studentId, decimal? dailyLimit, string? blockedCategories, bool allergyHardBlock, CancellationToken cancellationToken = default)
        {
            var control = await _db.SpendControls.SingleOrDefaultAsync(c => c.StudentId == studentId, cancellationToken) ?? _db.SpendControls.Add(new SpendControl { StudentId = studentId }).Entity;
            control.DailyLimit = dailyLimit;
            control.BlockedCategories = blockedCategories;
            control.AllergyHardBlock = allergyHardBlock;
            await _db.SaveChangesAsync(cancellationToken);
            return control;
        }

        // ------------------------------------------------------------------ POS

        public async Task<Sale> RecordSaleAsync(
            WalletHolderKind holderKind, int holderId, IReadOnlyList<BasketLine> basket, SaleTender tender, int operatorUserId,
            int? tillSessionId = null, bool operatorConfirmedAllergyWarning = false, DateTime? capturedOfflineAtUtc = null, CancellationToken cancellationToken = default)
        {
            var itemIds = basket.Select(b => b.CafeteriaItemId).ToList();
            var items = await _db.CafeteriaItems.Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
            var lines = basket.Select(b =>
            {
                var item = items[b.CafeteriaItemId];
                return new SaleLine { CafeteriaItemId = item.Id, Quantity = b.Quantity, UnitPrice = item.Price, LineTotal = item.Price * b.Quantity };
            }).ToList();
            var total = lines.Sum(l => l.LineTotal);
            var now = _clock.UtcNow;

            if (holderKind == WalletHolderKind.Student)
            {
                foreach (var item in items.Values.Where(i => !NutritionPolicy.SellableToStudent(i.NutritionClass, i.IsStaffOnly)))
                {
                    throw new SaleBlockedException($"item {item.Id} is not sellable to students (BR-CAF-008)");
                }

                // BR-CAF-002 real-time controls: parent limits + allergy feed from Module 24's emergency banner (severe allergies).
                var control = await _db.SpendControls.SingleOrDefaultAsync(c => c.StudentId == holderId, cancellationToken);
                var spentToday = (await _db.Sales.Where(s => s.HolderKind == holderKind && s.HolderId == holderId && s.Status == SaleStatus.Posted && s.AtUtc >= now.Date && s.AtUtc < now.Date.AddDays(1)).Select(s => s.Total).ToListAsync(cancellationToken)).Sum();
                var banner = await _health.GetEmergencyBannerAsync(holderId, cancellationToken);
                var verdict = SpendControlEvaluator.Evaluate(
                    lines.Select(l => new SpendControlEvaluator.LineInput(items[l.CafeteriaItemId].Category, SpendControlEvaluator.SplitTags(items[l.CafeteriaItemId].AllergenTags), l.LineTotal)).ToList(),
                    spentToday, control?.DailyLimit, SpendControlEvaluator.SplitTags(control?.BlockedCategories), banner?.SevereAllergies ?? Array.Empty<string>(), control?.AllergyHardBlock ?? false);
                if (verdict.OverDailyLimit)
                {
                    throw new SaleBlockedException("daily spend limit exceeded");
                }

                if (verdict.BlockedCategoriesHit.Count > 0)
                {
                    throw new SaleBlockedException($"blocked categories: {string.Join(", ", verdict.BlockedCategoriesHit)}");
                }

                if (verdict.AllergyBlocks)
                {
                    throw new SaleBlockedException($"allergy hard-block: {string.Join(", ", verdict.AllergyMatches)}");
                }

                if (verdict.AllergyMatches.Count > 0)
                {
                    if (!operatorConfirmedAllergyWarning)
                    {
                        throw new AllergyWarningUnconfirmedException(string.Join(", ", verdict.AllergyMatches));
                    }

                    foreach (var line in lines.Where(l => SpendControlEvaluator.SplitTags(items[l.CafeteriaItemId].AllergenTags).Any(t => verdict.AllergyMatches.Contains(t, StringComparer.OrdinalIgnoreCase))))
                    {
                        line.AllergyWarned = true;
                    }
                }
            }

            // Stock deduct guard (BR-CAF-006 / doc §9).
            foreach (var line in lines)
            {
                var level = StockLevelCalculator.Level(await _db.StockMovements.Where(m => m.CafeteriaItemId == line.CafeteriaItemId).Select(m => m.Quantity).ToListAsync(cancellationToken));
                if (!StockLevelCalculator.CanDeduct(level, line.Quantity))
                {
                    throw new SaleBlockedException($"insufficient stock for item {line.CafeteriaItemId}");
                }
            }

            // Tender (BR-CAF-004 plan-first, wallet fallback, cash).
            MealPlanSubscription? subscription = null;
            Wallet? wallet = null;
            switch (tender)
            {
                case SaleTender.MealPlan:
                    subscription = await _db.MealPlanSubscriptions.Where(s => s.StudentId == holderId && s.StartDate <= now.Date && s.EndDate >= now.Date).OrderByDescending(s => s.Id).FirstOrDefaultAsync(cancellationToken)
                                   ?? throw new SaleBlockedException("no active meal plan");
                    var plan = await _db.MealPlans.SingleAsync(p => p.Id == subscription.MealPlanId, cancellationToken);
                    var redeemedToday = await _db.Redemptions.AnyAsync(r => r.MealPlanSubscriptionId == subscription.Id && r.Date == now.Date, cancellationToken);
                    if (!MealPlanRedemptionPolicy.CanRedeem(now, subscription.StartDate, subscription.EndDate, redeemedToday, total, plan.DailyValueCap))
                    {
                        throw new SaleBlockedException("meal plan already redeemed today or basket exceeds the daily entitlement");
                    }

                    break;
                case SaleTender.Wallet:
                    wallet = await _db.Wallets.SingleOrDefaultAsync(w => w.HolderKind == holderKind && w.HolderId == holderId, cancellationToken) ?? throw new SaleBlockedException("no wallet");
                    if (!WalletBalanceCalculator.CanAfford(await BalanceAsync(wallet.Id, cancellationToken), wallet.OverdraftAllowance, total))
                    {
                        throw new SaleBlockedException("insufficient wallet balance");
                    }

                    break;
                case SaleTender.Cash:
                    if (!tillSessionId.HasValue || (await _db.TillSessions.SingleAsync(s => s.Id == tillSessionId.Value, cancellationToken)).Status != TillSessionStatus.Open)
                    {
                        throw new SaleBlockedException("cash sales need an open till session (BR-CAF-007)");
                    }

                    break;
            }

            var sale = new Sale { HolderKind = holderKind, HolderId = holderId, TillSessionId = tillSessionId, OperatorUserId = operatorUserId, AtUtc = now, Tender = tender, Total = total, CapturedOfflineAtUtc = capturedOfflineAtUtc, Lines = lines };
            _db.Sales.Add(sale);
            await _db.SaveChangesAsync(cancellationToken);

            foreach (var line in lines)
            {
                _db.StockMovements.Add(new StockMovement { CafeteriaItemId = line.CafeteriaItemId, Kind = StockMovementKind.Deduct, Quantity = -line.Quantity, AtUtc = now });
            }

            if (wallet != null)
            {
                _db.WalletLedgerEntries.Add(new WalletLedgerEntry { WalletId = wallet.Id, Kind = WalletLedgerKind.Sale, Amount = -total, SaleId = sale.Id, AtUtc = now });
            }

            if (subscription != null)
            {
                _db.Redemptions.Add(new Redemption { MealPlanSubscriptionId = subscription.Id, Date = now.Date, SaleId = sale.Id });
            }

            await _db.SaveChangesAsync(cancellationToken);
            return sale;
        }

        public async Task VoidSaleAsync(int saleId, string reason, CancellationToken cancellationToken = default)
        {
            var sale = await _db.Sales.Include(s => s.Lines).SingleAsync(s => s.Id == saleId, cancellationToken);
            var tillOpen = sale.TillSessionId.HasValue && (await _db.TillSessions.SingleAsync(s => s.Id == sale.TillSessionId.Value, cancellationToken)).Status == TillSessionStatus.Open;
            if (!SaleVoidPolicy.CanVoid(sale.TillSessionId, tillOpen, sale.Status))
            {
                throw new SaleNotVoidableException(saleId);
            }

            _audit.Reason = reason;
            sale.Status = SaleStatus.Voided;
            sale.VoidReason = reason;
            var now = _clock.UtcNow;
            foreach (var line in sale.Lines)
            {
                _db.StockMovements.Add(new StockMovement { CafeteriaItemId = line.CafeteriaItemId, Kind = StockMovementKind.Receive, Quantity = line.Quantity, Reason = $"void of sale {saleId}", AtUtc = now });
            }

            if (sale.Tender == SaleTender.Wallet)
            {
                var wallet = await _db.Wallets.SingleAsync(w => w.HolderKind == sale.HolderKind && w.HolderId == sale.HolderId, cancellationToken);
                _db.WalletLedgerEntries.Add(new WalletLedgerEntry { WalletId = wallet.Id, Kind = WalletLedgerKind.SaleVoid, Amount = sale.Total, SaleId = sale.Id, Reason = reason, AtUtc = now });
            }

            if (sale.Tender == SaleTender.MealPlan)
            {
                var redemption = await _db.Redemptions.SingleOrDefaultAsync(r => r.SaleId == saleId, cancellationToken);
                if (redemption != null)
                {
                    _db.Redemptions.Remove(redemption);
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        // ------------------------------------------------------------------ meal plans + stock + summary

        public async Task<MealPlan> DefineMealPlanAsync(string nameAr, string nameEn, int feeCategoryId, decimal price, decimal dailyValueCap, UnredeemedDayPolicy unredeemedDayPolicy = UnredeemedDayPolicy.Forfeit, CancellationToken cancellationToken = default)
        {
            var plan = new MealPlan { NameAr = nameAr, NameEn = nameEn, FeeCategoryId = feeCategoryId, Price = price, DailyValueCap = dailyValueCap, UnredeemedDayPolicy = unredeemedDayPolicy };
            _db.MealPlans.Add(plan);
            await _db.SaveChangesAsync(cancellationToken);
            return plan;
        }

        public async Task<MealPlanSubscription> SubscribeMealPlanAsync(int studentId, int payerId, int mealPlanId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            var plan = await _db.MealPlans.SingleAsync(p => p.Id == mealPlanId, cancellationToken);
            var charge = await _feeAdmin.PostManualChargeAsync(studentId, payerId, plan.FeeCategoryId, plan.Price, cancellationToken);
            var subscription = new MealPlanSubscription { MealPlanId = mealPlanId, StudentId = studentId, StartDate = startDate.Date, EndDate = endDate.Date, ChargeId = charge.Id };
            _db.MealPlanSubscriptions.Add(subscription);
            await _db.SaveChangesAsync(cancellationToken);
            return subscription;
        }

        public async Task ReceiveStockAsync(int itemId, int quantity, CancellationToken cancellationToken = default)
        {
            _db.StockMovements.Add(new StockMovement { CafeteriaItemId = itemId, Kind = StockMovementKind.Receive, Quantity = quantity, AtUtc = _clock.UtcNow });
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task RecordWasteAsync(int itemId, int quantity, string reason, CancellationToken cancellationToken = default)
        {
            _db.StockMovements.Add(new StockMovement { CafeteriaItemId = itemId, Kind = StockMovementKind.Waste, Quantity = -quantity, Reason = reason, AtUtc = _clock.UtcNow });
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<int> StockLevelAsync(int itemId, CancellationToken cancellationToken = default)
            => StockLevelCalculator.Level(await _db.StockMovements.Where(m => m.CafeteriaItemId == itemId).Select(m => m.Quantity).ToListAsync(cancellationToken));

        public async Task<DailySalesSummary> DailySummaryAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            var from = date.Date;
            var to = from.AddDays(1);
            var sales = await _db.Sales.Where(s => s.AtUtc >= from && s.AtUtc < to).Select(s => new { s.Tender, s.Total, s.Status }).ToListAsync(cancellationToken);
            var posted = sales.Where(s => s.Status == SaleStatus.Posted).ToList();
            return new DailySalesSummary(
                from,
                posted.Where(s => s.Tender == SaleTender.Wallet).Sum(s => s.Total),
                posted.Where(s => s.Tender == SaleTender.Cash).Sum(s => s.Total),
                posted.Where(s => s.Tender == SaleTender.MealPlan).Sum(s => s.Total),
                posted.Count,
                sales.Count(s => s.Status == SaleStatus.Voided));
        }
    }
}
