using System;
using System.Collections.Generic;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Cafeteria
{
    /// <summary>BR-CAF-008 traffic-light nutrition classes per pack; Banned items can never enter a student-sale menu.</summary>
    public enum NutritionClass : short
    {
        Green = 1,
        Amber = 2,
        Red = 3,
        Banned = 4,
    }

    public enum WalletHolderKind : short
    {
        Student = 1,
        Employee = 2,
    }

    /// <summary>BR-CAF-001/007/009 ledger kinds — balance is always the ledger sum, never a mutable field.</summary>
    public enum WalletLedgerKind : short
    {
        TopUp = 1,
        Sale = 2,
        Adjustment = 3,
        Refund = 4,
        SaleVoid = 5,
    }

    /// <summary>BR-CAF-003/004 tenders: plan-first, wallet fallback, or cash.</summary>
    public enum SaleTender : short
    {
        Wallet = 1,
        Cash = 2,
        MealPlan = 3,
    }

    public enum SaleStatus : short
    {
        Posted = 1,
        Voided = 2,
    }

    /// <summary>BR-CAF-004 unredeemed-day policy per school.</summary>
    public enum UnredeemedDayPolicy : short
    {
        Forfeit = 1,
        Credit = 2,
    }

    public enum StockMovementKind : short
    {
        Receive = 1,
        Deduct = 2,
        Waste = 3,
        Count = 4,
    }

    /// <summary>svc.CafeteriaItem (doc/Modules/27 §7, BR-CAF-002/005/008): allergen tags + nutrition class; T2 so price changes are logged (the doc's "price versions").</summary>
    [Audited(AuditTier.T2)]
    public class CafeteriaItem : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        /// <summary>Category code (e.g. "drinks", "snacks") — the unit of parent blocking (BR-CAF-002).</summary>
        public string Category { get; set; } = string.Empty;

        public decimal Price { get; set; }

        /// <summary>Comma-separated allergen tags (e.g. "peanuts,milk").</summary>
        public string? AllergenTags { get; set; }

        public NutritionClass NutritionClass { get; set; } = NutritionClass.Green;

        /// <summary>BR-CAF-008: banned-class items may still exist for staff sale.</summary>
        public bool IsStaffOnly { get; set; }

        public bool IsActive { get; set; } = true;
    }

    /// <summary>svc.Menu (BR-CAF-005): per day, published to the portal with allergen/nutrition display.</summary>
    [Audited(AuditTier.T3)]
    public class Menu : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public DateTime Date { get; set; }

        public bool IsPublished { get; set; }

        public List<MenuLine> Lines { get; set; } = new();
    }

    public class MenuLine : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int MenuId { get; set; }

        public int CafeteriaItemId { get; set; }
    }

    /// <summary>svc.Wallet (BR-CAF-001): one per holder; balance derived from the ledger. OverdraftAllowance is the config'd small cap (0 = none).</summary>
    [Audited(AuditTier.T1)]
    public class Wallet : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public WalletHolderKind HolderKind { get; set; }

        public int HolderId { get; set; }

        public decimal OverdraftAllowance { get; set; }

        public bool IsActive { get; set; } = true;
    }

    /// <summary>svc.WalletLedger: append-only; TopUps reference the Module 21 receipt, Refunds the voucher, Adjustments carry their documented reason (BR-CAF-009).</summary>
    public class WalletLedgerEntry : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int WalletId { get; set; }

        public WalletLedgerKind Kind { get; set; }

        /// <summary>Signed: top-ups/credits positive, sales/refunds negative.</summary>
        public decimal Amount { get; set; }

        public int? ReceiptId { get; set; }

        public int? SaleId { get; set; }

        public int? RefundVoucherId { get; set; }

        public string? Reason { get; set; }

        public DateTime AtUtc { get; set; }
    }

    /// <summary>svc.SpendControl (BR-CAF-002): parent-set per student.</summary>
    [Audited(AuditTier.T2)]
    public class SpendControl : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int StudentId { get; set; }

        public decimal? DailyLimit { get; set; }

        /// <summary>Comma-separated blocked item categories.</summary>
        public string? BlockedCategories { get; set; }

        /// <summary>BR-CAF-002 / doc Q2: allergy match blocks the sale (parent opt-in) instead of warning.</summary>
        public bool AllergyHardBlock { get; set; }
    }

    /// <summary>svc.Sale (BR-CAF-003/009): every sale logged — holder, lines, tender, operator, till; voids same-session (P2) with reason. T1 so VoidReason is reason-required.</summary>
    [Audited(AuditTier.T1)]
    public class Sale : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public WalletHolderKind HolderKind { get; set; }

        public int HolderId { get; set; }

        public int? TillSessionId { get; set; }

        public int OperatorUserId { get; set; }

        public DateTime AtUtc { get; set; }

        public SaleTender Tender { get; set; }

        public decimal Total { get; set; }

        public SaleStatus Status { get; set; } = SaleStatus.Posted;

        [RequiresAuditReason]
        public string? VoidReason { get; set; }

        /// <summary>BR-CAF-003 offline-tolerant queue: the client-side capture time when synced later (null = captured live).</summary>
        public DateTime? CapturedOfflineAtUtc { get; set; }

        public List<SaleLine> Lines { get; set; } = new();
    }

    public class SaleLine : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int SaleId { get; set; }

        public int CafeteriaItemId { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal LineTotal { get; set; }

        /// <summary>BR-CAF-002: the allergy warning fired for this line (sold on operator confirmation).</summary>
        public bool AllergyWarned { get; set; }
    }

    /// <summary>svc.MealPlan (BR-CAF-004): subscription product charged via Module 19 (service-linked category); one redemption per day up to the value cap.</summary>
    [Audited(AuditTier.T2)]
    public class MealPlan : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public int FeeCategoryId { get; set; }

        public decimal Price { get; set; }

        public decimal DailyValueCap { get; set; }

        public UnredeemedDayPolicy UnredeemedDayPolicy { get; set; } = UnredeemedDayPolicy.Forfeit;

        public bool IsActive { get; set; } = true;
    }

    [Audited(AuditTier.T1)]
    public class MealPlanSubscription : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int MealPlanId { get; set; }

        public int StudentId { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public int? ChargeId { get; set; }
    }

    /// <summary>svc.Redemption (BR-CAF-004): one per subscription per day.</summary>
    public class Redemption : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int MealPlanSubscriptionId { get; set; }

        public DateTime Date { get; set; }

        public int SaleId { get; set; }
    }

    /// <summary>svc.StockMovement (BR-CAF-006 light stock): level derived as the signed sum.</summary>
    public class StockMovement : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int CafeteriaItemId { get; set; }

        public StockMovementKind Kind { get; set; }

        /// <summary>Signed quantity: receive/count-up positive; deduct/waste negative.</summary>
        public int Quantity { get; set; }

        public string? Reason { get; set; }

        public DateTime AtUtc { get; set; }
    }
}
