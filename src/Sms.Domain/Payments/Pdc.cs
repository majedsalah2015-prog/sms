using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Payments
{
    /// <summary>
    /// ppl.Pdc (doc/Modules/21 §7, BR-PAY-004): post-dated cheque
    /// registry — a Gulf-market reality. Clearance converts a PDC into a
    /// real Receipt (ClearedReceiptId); bounce-fee charge generation and
    /// covered-installment un-covering (needs Module 20, doesn't exist
    /// yet) are deferred — this slice tracks the cheque lifecycle itself.
    /// PDC totals are exposure only, never counted as income until
    /// Cleared (BR-PAY-004) — enforced by callers reading Status, not by
    /// this entity.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class Pdc : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int PayerId { get; set; }

        public string BankName { get; set; } = string.Empty;

        public string ChequeNo { get; set; } = string.Empty;

        public DateTime ChequeDate { get; set; }

        public decimal Amount { get; set; }

        public PdcStatus Status { get; set; } = PdcStatus.Lodged;

        public int? ClearedReceiptId { get; set; }
    }
}
