using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Payments
{
    /// <summary>
    /// ppl.TillSession (doc/Modules/21 §7, BR-PAY-001): cashier x till x
    /// day. Every counter receipt must belong to an open session; closing
    /// records the counted total against the system total and any
    /// variance reason. Only the till-level close is modeled here — the
    /// finance-level daily close + bank reconciliation (BR-PAY-006's
    /// other half) are deferred entirely.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class TillSession : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int CashierUserId { get; set; }

        public string TillCode { get; set; } = string.Empty;

        public decimal FloatAmount { get; set; }

        public DateTime OpenedAtUtc { get; set; }

        public DateTime? ClosedAtUtc { get; set; }

        /// <summary>Sum of this session's Posted receipts, frozen at close time (so a later Void doesn't retroactively shift a closed session's recorded variance).</summary>
        public decimal? SystemTotal { get; set; }

        public decimal? CountedTotal { get; set; }

        public string? VarianceReason { get; set; }

        public TillSessionStatus Status { get; set; } = TillSessionStatus.Open;
    }
}
