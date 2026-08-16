using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Payments
{
    /// <summary>ppl.PaymentAllocation (doc/Modules/21 §7, BR-PAY-003): one receipt-to-charge allocation line. Re-openable only via reversal (a new negative-offsetting row), never edited in place.</summary>
    [Audited(AuditTier.T2)]
    public class PaymentAllocation : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int ReceiptId { get; set; }

        public int ChargeId { get; set; }

        public decimal AllocatedAmount { get; set; }
    }
}
