using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Payments
{
    /// <summary>
    /// ppl.RefundVoucher (doc/Modules/21 §7, BR-PAY-005): WF-05, strict
    /// "RFD" series (already seeded by E-010). Refunds to the original
    /// payer only in this slice (BR-PAY-005's Principal-T1 exception path
    /// is not modeled).
    /// </summary>
    [Audited(AuditTier.T1)]
    public class RefundVoucher : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int PayerId { get; set; }

        /// <summary>doc 08 RFD series.</summary>
        public string VoucherNo { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public PaymentMethod Method { get; set; }

        public string Reason { get; set; } = string.Empty;

        public RefundVoucherStatus Status { get; set; } = RefundVoucherStatus.Requested;
    }
}
