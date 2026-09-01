using System;
using System.Collections.Generic;
using Sms.Web.Models;

namespace Sms.Web.Api.Models
{
    /// <summary>A fee category (doc/Modules/19 §8).</summary>
    public sealed class ApiFeeCategory
    {
        public int FeeCategoryId { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        /// <summary>Null means the category carries no VAT at all — different from a rate of zero.</summary>
        public decimal? VatRate { get; set; }

        public bool IsMandatory { get; set; }

        public bool IsRefundable { get; set; }

        public bool IsServiceLinked { get; set; }

        public bool IsActive { get; set; }
    }

    /// <summary>One line of a grade's fee structure for a year.</summary>
    public sealed class ApiFeeStructureLine
    {
        public int FeeStructureLineId { get; set; }

        public int AcademicYearId { get; set; }

        public int GradeYearProfileId { get; set; }

        public string? GradeCode { get; set; }

        public string? GradeName { get; set; }

        public int FeeCategoryId { get; set; }

        public string CategoryNameAr { get; set; } = string.Empty;

        public string CategoryNameEn { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string Currency { get; set; } = string.Empty;

        /// <summary>Draft / Approved / Withdrawn. Only an approved line may be charged from.</summary>
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>One posted charge on a student.</summary>
    public sealed class ApiCharge
    {
        public int ChargeId { get; set; }

        public string ChargeNo { get; set; } = string.Empty;

        public int StudentId { get; set; }

        public int PayerId { get; set; }

        public int FeeCategoryId { get; set; }

        public string CategoryNameAr { get; set; } = string.Empty;

        public string CategoryNameEn { get; set; } = string.Empty;

        /// <summary>Net, VAT and gross apart — a school that reports one number cannot file a return.</summary>
        public decimal NetAmount { get; set; }

        public decimal VatAmount { get; set; }

        public decimal GrossAmount { get; set; }

        public decimal? VatRateSnapshot { get; set; }

        public string Currency { get; set; } = string.Empty;

        /// <summary>Posted / Void.</summary>
        public string Status { get; set; } = string.Empty;

        public DateTime PostedAtUtc { get; set; }
    }

    /// <summary>
    /// A statement of account (doc/Modules/19 §8.7, BR-DIS-010). Gross,
    /// discounts and net are separate figures and are never netted invisibly.
    /// </summary>
    public sealed class ApiStatement
    {
        /// <summary>Whichever of the two this statement was asked for.</summary>
        public int? PayerId { get; set; }

        public int? StudentId { get; set; }

        public DateTime AsOfUtc { get; set; }

        public string Currency { get; set; } = string.Empty;

        public decimal GrossCharges { get; set; }

        public decimal Discounts { get; set; }

        public decimal CreditNotes { get; set; }

        public decimal Payments { get; set; }

        public decimal Refunds { get; set; }

        /// <summary>Gross − discounts − credit notes.</summary>
        public decimal NetCharges { get; set; }

        /// <summary>Positive means owed.</summary>
        public decimal ClosingBalance { get; set; }

        public IReadOnlyList<ApiStatementLine> Lines { get; set; } = Array.Empty<ApiStatementLine>();
    }

    /// <summary>One document on a statement. Debits raise what is owed, credits lower it.</summary>
    public sealed class ApiStatementLine
    {
        public DateTime DateUtc { get; set; }

        /// <summary>Charge / CreditNote / Discount / Payment / Refund.</summary>
        public string Kind { get; set; } = string.Empty;

        public string DocumentNo { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Debit { get; set; }

        public decimal Credit { get; set; }

        public decimal RunningBalance { get; set; }
    }

    /// <summary>One instalment on a student's plan.</summary>
    public sealed class ApiInstallment
    {
        public int InstallmentId { get; set; }

        public int SequenceNumber { get; set; }

        public DateTime DueDate { get; set; }

        public decimal Amount { get; set; }

        public decimal Paid { get; set; }

        public decimal Outstanding { get; set; }

        /// <summary>Open / PartiallyPaid / Paid / Overdue / WrittenOff — the deriver's own vocabulary.</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>True when a post-dated cheque is lodged against it.</summary>
        public bool IsPdcCovered { get; set; }

        public string Currency { get; set; } = string.Empty;
    }

    /// <summary>A payment as it was taken.</summary>
    public sealed class ApiReceipt
    {
        public int ReceiptId { get; set; }

        public string ReceiptNo { get; set; } = string.Empty;

        public int PayerId { get; set; }

        /// <summary>Cash / Card / BankTransfer / Cheque / Pdc.</summary>
        public string Method { get; set; } = string.Empty;

        public string? MethodRefNo { get; set; }

        public decimal Amount { get; set; }

        public string Currency { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTime IssuedAtUtc { get; set; }
    }

    /// <summary>
    /// Take a payment. BR-PAY-003: the receipt is numbered on this call's own
    /// commit and auto-allocated oldest-due-first across the payer's open
    /// charges; anything left over becomes advance balance.
    /// </summary>
    public sealed class ApiCaptureReceiptRequest
    {
        public int PayerId { get; set; }

        /// <summary>Cash / Card / BankTransfer / Cheque / Pdc.</summary>
        [RequiredField("payment method", "طريقة الدفع")]
        public string Method { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        /// <summary>The open till session, when the money crossed a counter.</summary>
        public int? TillSessionId { get; set; }

        /// <summary>Card slip, transfer reference, cheque number — whatever identifies it outside this system.</summary>
        public string? MethodRefNo { get; set; }
    }

    /// <summary>Post a charge onto a student from an approved fee structure line.</summary>
    public sealed class ApiPostChargeRequest
    {
        public int StudentId { get; set; }

        public int FeeStructureLineId { get; set; }

        /// <summary>
        /// Registration / ReRegistration / ServiceAssignment. Anything else —
        /// including nothing — is treated as Registration, which is what the
        /// counter screen does: a manual charge is a different endpoint with a
        /// mandatory reason, and an opening balance belongs to the rollover.
        /// </summary>
        public string? SourceType { get; set; }
    }

    /// <summary>
    /// BR-DIS-010 / BR-FEE: reduce a posted charge with a credit note rather
    /// than editing it. The reason is mandatory — a charge that shrank without
    /// one is unanswerable at the counter.
    /// </summary>
    public sealed class ApiCreditNoteRequest
    {
        public decimal Amount { get; set; }

        [RequiredField("reason", "السبب")]
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>Open a till session for a cashier.</summary>
    public sealed class ApiOpenTillRequest
    {
        /// <summary>
        /// Optional, and normally omitted: leave it out and the server assigns the drawer
        /// (BR-PAY-001) — the cashier's own till, or the next unused one. Send a code only to put a
        /// cashier on a specific drawer, which is refused if it is already open elsewhere.
        /// </summary>
        public string? TillCode { get; set; }

        public decimal FloatAmount { get; set; }

        /// <summary>Defaults to the caller — a cashier opens their own drawer.</summary>
        public int? CashierUserId { get; set; }
    }

    /// <summary>Close a till. The system total is this session's receipts; the counted total is what was in the drawer.</summary>
    public sealed class ApiCloseTillRequest
    {
        public decimal CountedTotal { get; set; }

        /// <summary>Required in practice whenever the two totals disagree.</summary>
        public string? VarianceReason { get; set; }
    }
}
