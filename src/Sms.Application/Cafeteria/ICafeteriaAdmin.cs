using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Cafeteria;
using Sms.Domain.Payments;

namespace Sms.Application.Cafeteria
{
    public sealed record BasketLine(int CafeteriaItemId, int Quantity);

    /// <summary>BR-CAF-007 daily summary journal feed: totals by tender for a day.</summary>
    public sealed record DailySalesSummary(DateTime Date, decimal WalletSales, decimal CashSales, decimal MealPlanRedemptions, int SaleCount, int VoidCount);

    /// <summary>
    /// doc/Modules/27 §8 POS / Wallet desk / Menu & items / Meal plans /
    /// Stock / Day close screens backing (screens deferred, operations are
    /// core). Money integrity (BR-CAF-007): top-ups are Module 21 receipts
    /// with Purpose = WalletTopUp, cash sales ride Module 21 till sessions,
    /// wallet refunds are Module 21 refund vouchers, sales settle
    /// internally against the wallet ledger, and the GL export journals
    /// wallet liability + cafeteria revenue.
    /// </summary>
    public interface ICafeteriaAdmin
    {
        Task<CafeteriaItem> DefineItemAsync(string nameAr, string nameEn, string category, decimal price, NutritionClass nutritionClass, string? allergenTags = null, bool isStaffOnly = false, CancellationToken cancellationToken = default);

        /// <summary>BR-CAF-005/008: banned-class items are refused on a student menu (<see cref="Common.Exceptions.BannedItemOnMenuException"/>).</summary>
        Task<Menu> DefineMenuAsync(DateTime date, IReadOnlyList<int> itemIds, bool publish = true, CancellationToken cancellationToken = default);

        Task<Wallet> EnsureWalletAsync(WalletHolderKind holderKind, int holderId, decimal overdraftAllowance = 0m, CancellationToken cancellationToken = default);

        Task<decimal> BalanceAsync(int walletId, CancellationToken cancellationToken = default);

        /// <summary>BR-CAF-001/007: numbered Module 21 receipt (Purpose WalletTopUp) + ledger credit; cash requires an open till session.</summary>
        Task<Receipt> TopUpAsync(int walletId, int payerId, PaymentMethod method, decimal amount, int? tillSessionId = null, CancellationToken cancellationToken = default);

        /// <summary>BR-CAF-009: adjustments only via documented corrections — reason mandatory, audit event logged.</summary>
        Task AdjustAsync(int walletId, decimal signedAmount, string reason, int actorUserId, CancellationToken cancellationToken = default);

        /// <summary>BR-CAF-001: refund the balance on withdrawal/closure — a Module 21 refund voucher (WF-05 flow continues in PaymentAdmin) + ledger debit.</summary>
        Task<RefundVoucher> RefundBalanceAsync(int walletId, int payerId, PaymentMethod method, string reason, CancellationToken cancellationToken = default);

        Task<SpendControl> SetSpendControlAsync(int studentId, decimal? dailyLimit, string? blockedCategories, bool allergyHardBlock, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-CAF-002/003/004: real-time controls (daily limit, blocked categories — hard; allergy — warn unless hard-block opt-in;
        /// <see cref="Common.Exceptions.SaleBlockedException"/>), stock deduct guard, tender: MealPlan (one redemption/day within cap) →
        /// Wallet (balance + overdraft) → Cash (open till session). Allergy warnings need <paramref name="operatorConfirmedAllergyWarning"/>.
        /// </summary>
        Task<Sale> RecordSaleAsync(
            WalletHolderKind holderKind, int holderId, IReadOnlyList<BasketLine> basket, SaleTender tender, int operatorUserId,
            int? tillSessionId = null, bool operatorConfirmedAllergyWarning = false, DateTime? capturedOfflineAtUtc = null, CancellationToken cancellationToken = default);

        /// <summary>BR-CAF-009: same-session void with reason (T1); reverses wallet ledger, stock and plan redemption.</summary>
        Task VoidSaleAsync(int saleId, string reason, CancellationToken cancellationToken = default);

        Task<MealPlan> DefineMealPlanAsync(string nameAr, string nameEn, int feeCategoryId, decimal price, decimal dailyValueCap, UnredeemedDayPolicy unredeemedDayPolicy = UnredeemedDayPolicy.Forfeit, CancellationToken cancellationToken = default);

        /// <summary>BR-CAF-004: charges the plan price via Module 19 (service-linked category); pro-ration is Module 19's policy (not applied).</summary>
        Task<MealPlanSubscription> SubscribeMealPlanAsync(int studentId, int payerId, int mealPlanId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        Task ReceiveStockAsync(int itemId, int quantity, CancellationToken cancellationToken = default);

        Task RecordWasteAsync(int itemId, int quantity, string reason, CancellationToken cancellationToken = default);

        Task<int> StockLevelAsync(int itemId, CancellationToken cancellationToken = default);

        /// <summary>BR-CAF-007: the daily summary that feeds finance.</summary>
        Task<DailySalesSummary> DailySummaryAsync(DateTime date, CancellationToken cancellationToken = default);
    }
}
