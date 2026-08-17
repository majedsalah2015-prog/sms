using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Payments;
using Sms.Domain.Store;

namespace Sms.Application.Store
{
    public sealed record VariantInput(string Sku, string? Barcode = null, string? Size = null, string? Color = null, int LowStockThreshold = 0);

    public sealed record StoreBasketLine(int StoreVariantId, int Quantity);

    public sealed record BundleLineInput(int StoreItemId, int Quantity);

    public sealed record ReorderLine(int StoreVariantId, string Sku, int Level, int Threshold);

    /// <summary>
    /// doc/Modules/28 §8 Catalog & prices / POS / Bundle season / Distribution /
    /// Returns desk / Stock screens backing (screens deferred, operations are
    /// core). Money is Module 19/21: every sale is a Charge; cash/card add a
    /// Module 21 receipt allocated to that charge; wallet debits the cafeteria
    /// ledger; account-charge leaves the charge open on the payer.
    /// </summary>
    public interface IStoreAdmin
    {
        Task<StoreItem> DefineItemAsync(string nameAr, string nameEn, StoreItemCategory category, int feeCategoryId, IReadOnlyList<VariantInput> variants, CancellationToken cancellationToken = default);

        /// <summary>BR-STO-001/008: a new price-list version; POS never overrides.</summary>
        Task<PriceList> PublishPriceListAsync(DateTime effectiveFrom, IReadOnlyList<(int StoreItemId, decimal Price)> prices, CancellationToken cancellationToken = default);

        Task SetAccountChargePolicyAsync(StoreItemCategory category, bool isAllowed, decimal? capPerSale, CancellationToken cancellationToken = default);

        Task SetReturnPolicyAsync(StoreItemCategory category, int windowDays, bool sealedOnly, CancellationToken cancellationToken = default);

        Task ReceiveStockAsync(int storeVariantId, int quantity, CancellationToken cancellationToken = default);

        Task<int> StockLevelAsync(int storeVariantId, CancellationToken cancellationToken = default);

        /// <summary>BR-STO-006: variants at or below their low-stock threshold (the want-list export).</summary>
        Task<IReadOnlyList<ReorderLine>> ReorderReportAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-STO-003/008: prices from the active list; stock guard; tender: Cash/Card → Charge + Module 21 receipt allocated to it (open till session);
        /// Wallet → cafeteria ledger debit (<paramref name="allowWalletTender"/> config); AccountCharge → open Charge, category policy + cap
        /// (<see cref="Common.Exceptions.AccountChargeNotAllowedException"/>; beyond cap needs <paramref name="financeOverrideReason"/>).
        /// </summary>
        Task<StoreSale> RecordSaleAsync(
            int payerId, IReadOnlyList<StoreBasketLine> basket, StoreTender tender, int operatorUserId, int? studentId = null,
            int? tillSessionId = null, bool allowWalletTender = true, string? financeOverrideReason = null, CancellationToken cancellationToken = default);

        /// <summary>BR-STO-008: same-session void (BR-PAY rules), reason mandatory; restores stock and credits the charge.</summary>
        Task VoidSaleAsync(int storeSaleId, string reason, CancellationToken cancellationToken = default);

        /// <summary>BR-STO-005: exchange = free stock swap within window; return = credit note on the sale's charge (refund via WF-05 is Module 21's flow). Throws <see cref="Common.Exceptions.ReturnNotAllowedException"/>.</summary>
        Task<ReturnExchange> ReturnOrExchangeAsync(int storeSaleLineId, ReturnKind kind, int quantity, bool isSealed, int? newStoreVariantId = null, CancellationToken cancellationToken = default);

        Task<Bundle> DefineBundleAsync(string nameAr, string nameEn, int gradeYearProfileId, int feeCategoryId, decimal price, BundleChargeMode chargeMode, IReadOnlyList<BundleLineInput> lines, CancellationToken cancellationToken = default);

        /// <summary>BR-STO-002: assigns the bundle to every active enrollment of its grade-year profile and charges it (unless AtHandout). Idempotent per student.</summary>
        Task<IReadOnlyList<BundleAssignment>> AssignBundleBatchAsync(int bundleId, CancellationToken cancellationToken = default);

        Task<DistributionSession> OpenDistributionAsync(int bundleId, DateTime date, CancellationToken cancellationToken = default);

        /// <summary>BR-STO-004: handout of one bundle line to a student — variant chosen now, stock deducted, e-ack; pay-first gate per <paramref name="requireChargedFirst"/> (doc Q2). Marks the assignment Distributed when complete; AtHandout bundles charge here.</summary>
        Task<HandoutRecord> HandOutAsync(int distributionSessionId, int bundleAssignmentId, int bundleLineId, int storeVariantId, int quantity, bool acknowledged, bool requireChargedFirst = true, CancellationToken cancellationToken = default);

        /// <summary>BR-STO-004 leakage control: charged-but-not-fully-distributed assignments.</summary>
        Task<IReadOnlyList<BundleAssignment>> UndistributedPaidAsync(int bundleId, CancellationToken cancellationToken = default);

        /// <summary>BR-STO-007: at withdrawal, an undistributed paid bundle is credited (credit note on its charge).</summary>
        Task ResolveUndistributedAtWithdrawalAsync(int bundleAssignmentId, CancellationToken cancellationToken = default);
    }
}
