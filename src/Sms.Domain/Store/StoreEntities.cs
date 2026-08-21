using System;
using System.Collections.Generic;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Store
{
    /// <summary>BR-STO-001 categories; VAT class comes from the linked FeeCategory (BR-FEE-001 mapping).</summary>
    public enum StoreItemCategory : short
    {
        Uniform = 1,
        Book = 2,
        Stationery = 3,
        Other = 4,
    }

    /// <summary>BR-STO-003 tenders — every sale is receipted or account-charged.</summary>
    public enum StoreTender : short
    {
        Cash = 1,
        Card = 2,
        Wallet = 3,
        AccountCharge = 4,
    }

    /// <summary>BR-STO-002 bundle charge modes per school config.</summary>
    public enum BundleChargeMode : short
    {
        AtRegistration = 1,
        OptIn = 2,
        AtHandout = 3,
    }

    public enum BundleAssignmentStatus : short
    {
        Assigned = 1,
        Charged = 2,
        Distributed = 3,
        Credited = 4,
    }

    public enum StoreStockKind : short
    {
        Receive = 1,
        Sell = 2,
        Adjust = 3,
        Waste = 4,
        Count = 5,
        ReturnIn = 6,
        ExchangeOut = 7,
        Handout = 8,
    }

    public enum ReturnKind : short
    {
        Return = 1,
        Exchange = 2,
    }

    public enum StoreSaleStatus : short
    {
        Posted = 1,
        Voided = 2,
    }

    /// <summary>svc.StoreItem (doc/Modules/28 §7, BR-STO-001): bilingual, categorized, VAT class via FeeCategory; variants carry SKU/barcode/stock.</summary>
    [Audited(AuditTier.T2)]
    public class StoreItem : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public StoreItemCategory Category { get; set; }

        /// <summary>BR-STO-001/003: the fee category whose VAT treatment and revenue mapping this item's charges carry.</summary>
        public int FeeCategoryId { get; set; }

        public bool IsActive { get; set; } = true;

        public List<StoreVariant> Variants { get; set; } = new();
    }

    public class StoreVariant : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int StoreItemId { get; set; }

        public string Sku { get; set; } = string.Empty;

        public string? Barcode { get; set; }

        public string? Size { get; set; }

        public string? Color { get; set; }

        /// <summary>BR-STO-006: drives the reorder (want-list) report.</summary>
        public int LowStockThreshold { get; set; }

        public bool IsActive { get; set; } = true;
    }

    /// <summary>svc.PriceList (BR-STO-001/008): versioned; price changes only via a new version (T2) — no POS overrides.</summary>
    [Audited(AuditTier.T2)]
    public class PriceList : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int Version { get; set; }

        public DateTime EffectiveFrom { get; set; }

        public bool IsActive { get; set; } = true;

        public List<PriceListLine> Lines { get; set; } = new();
    }

    public class PriceListLine : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int PriceListId { get; set; }

        public int StoreItemId { get; set; }

        public decimal Price { get; set; }
    }

    /// <summary>svc.Bundle (BR-STO-002): named kit per grade-year, bundle price (≠ sum allowed), variant-agnostic lines resolved at handout.</summary>
    [Audited(AuditTier.T2)]
    public class Bundle : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public int GradeYearProfileId { get; set; }

        public int FeeCategoryId { get; set; }

        public decimal Price { get; set; }

        public BundleChargeMode ChargeMode { get; set; } = BundleChargeMode.AtRegistration;

        public bool IsActive { get; set; } = true;

        public List<BundleLine> Lines { get; set; } = new();
    }

    public class BundleLine : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int BundleId { get; set; }

        public int StoreItemId { get; set; }

        public int Quantity { get; set; } = 1;
    }

    /// <summary>svc.BundleAssignment: student × bundle; ChargeId once charged (Module 19); Distributed once every line is handed out; Credited when resolved at withdrawal (BR-STO-007).</summary>
    [Audited(AuditTier.T1)]
    public class BundleAssignment : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int BundleId { get; set; }

        public int StudentId { get; set; }

        public int PayerId { get; set; }

        public BundleAssignmentStatus Status { get; set; } = BundleAssignmentStatus.Assigned;

        public int? ChargeId { get; set; }

        public int? CreditNoteId { get; set; }
    }

    /// <summary>svc.DistributionSession + HandoutRecord (BR-STO-004): per-student checklist with sizes chosen and e-ack.</summary>
    [Audited(AuditTier.T2)]
    public class DistributionSession : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int BundleId { get; set; }

        public DateTime Date { get; set; }

        public bool IsClosed { get; set; }
    }

    public class HandoutRecord : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int DistributionSessionId { get; set; }

        public int BundleAssignmentId { get; set; }

        public int BundleLineId { get; set; }

        public int StoreVariantId { get; set; }

        public int Quantity { get; set; }

        public DateTime ReceivedAtUtc { get; set; }

        public bool Acknowledged { get; set; }
    }

    /// <summary>svc.StoreSale (BR-STO-003/008): POS or account-charge; the money document is a Module 19 charge (+ Module 21 receipt for cash/card, wallet ledger for wallet).</summary>
    [Audited(AuditTier.T1)]
    public class StoreSale : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int? StudentId { get; set; }

        public int PayerId { get; set; }

        public StoreTender Tender { get; set; }

        public int? TillSessionId { get; set; }

        public int OperatorUserId { get; set; }

        public DateTime AtUtc { get; set; }

        public decimal Total { get; set; }

        public int? ChargeId { get; set; }

        public int? ReceiptId { get; set; }

        public StoreSaleStatus Status { get; set; } = StoreSaleStatus.Posted;

        /// <summary>
        /// When the document was voided, or null while it stands (gap G-4). Kept
        /// as its own column rather than read off ModifiedAtUtc: that stamp moves
        /// for any change at all, and the ledger needs the date the reversal
        /// belongs to, not the date somebody last touched the row.
        /// </summary>
        public DateTime? VoidedAtUtc { get; set; }

        [RequiresAuditReason]
        public string? VoidReason { get; set; }

        /// <summary>BR-STO-003: account-charge beyond the cap needed Finance (P2) — the override reason.</summary>
        public string? FinanceOverrideReason { get; set; }

        public List<StoreSaleLine> Lines { get; set; } = new();
    }

    public class StoreSaleLine : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int StoreSaleId { get; set; }

        public int StoreVariantId { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal LineTotal { get; set; }
    }

    /// <summary>svc.ReturnExchange (BR-STO-005): size exchanges free within window (stock swap); returns per category policy → credit note on the sale's charge (+ WF-05 refund when money returns).</summary>
    [Audited(AuditTier.T1)]
    public class ReturnExchange : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int StoreSaleLineId { get; set; }

        public ReturnKind Kind { get; set; }

        public int Quantity { get; set; }

        public int? NewStoreVariantId { get; set; }

        public bool IsSealed { get; set; }

        public int? CreditNoteId { get; set; }

        public DateTime AtUtc { get; set; }
    }

    /// <summary>BR-STO-005 return policy per category: window days + sealed-only (books).</summary>
    public class ReturnPolicy : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public StoreItemCategory Category { get; set; }

        public int WindowDays { get; set; } = 14;

        public bool SealedOnly { get; set; }
    }

    /// <summary>BR-STO-003 account-charge policy per category: allowed + cap per sale (beyond → P2 Finance override).</summary>
    public class AccountChargePolicy : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public StoreItemCategory Category { get; set; }

        public bool IsAllowed { get; set; }

        public decimal? CapPerSale { get; set; }
    }

    /// <summary>svc.StockMovement (store) — perpetual per variant; level derived.</summary>
    public class StoreStockMovement : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int StoreVariantId { get; set; }

        public StoreStockKind Kind { get; set; }

        public int Quantity { get; set; }

        public string? Reason { get; set; }

        public DateTime AtUtc { get; set; }
    }
}
