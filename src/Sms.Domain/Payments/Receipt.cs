using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Payments
{
    /// <summary>
    /// ppl.Receipt (doc/Modules/21 §7, BR-PAY-002): strict "RCP" series
    /// (already seeded by E-010), payer-addressed, method-detailed.
    /// TillSessionId is null for non-counter methods that don't need a
    /// cashier session (bank transfer reconciled separately, PDC
    /// clearance — BR-PAY-002 only requires an open session for counter
    /// receipts).
    /// </summary>
    [Audited(AuditTier.T1)]
    public class Receipt : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int PayerId { get; set; }

        public int? TillSessionId { get; set; }

        /// <summary>doc 08 RCP series.</summary>
        public string ReceiptNo { get; set; } = string.Empty;

        public PaymentMethod Method { get; set; }

        /// <summary>Card ref / transfer ref / cheque no — method-specific detail (BR-PAY-002).</summary>
        public string? MethodRefNo { get; set; }

        /// <summary>
        /// Which of the school's own accounts the money arrived in — the bank
        /// account a transfer was addressed to, or the cash box the notes went
        /// into (<see cref="CollectionAccount"/>, BR-PAY-002 "method-detailed").
        /// <para>
        /// Nullable for two reasons, both real. Receipts issued before this
        /// catalogue existed have no answer and are not going to acquire one;
        /// and a school that has defined no account of the relevant kind can
        /// still take money, which is what keeps its first morning working.
        /// Once an account of that kind exists the capture refuses a blank —
        /// see <c>CollectionAccountSelector.Validate</c>.
        /// </para>
        /// </summary>
        public int? CollectionAccountId { get; set; }

        public decimal Amount { get; set; }

        public ReceiptStatus Status { get; set; } = ReceiptStatus.Posted;

        /// <summary>S6/E-605: fee payment (default) vs cafeteria wallet top-up - see <see cref="ReceiptPurpose"/>.</summary>
        public ReceiptPurpose Purpose { get; set; } = ReceiptPurpose.FeePayment;

        public DateTime IssuedAtUtc { get; set; }
    }
}
