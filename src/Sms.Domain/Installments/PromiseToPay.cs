using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Installments
{
    /// <summary>ppl.PromiseToPay (doc/Modules/20 §7, BR-INS-006): a dated commitment against an overdue installment; a break (date passed, still unpaid) escalates the ladder.</summary>
    [Audited(AuditTier.T2)]
    public class PromiseToPay : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int InstallmentId { get; set; }

        public int RecordedByUserId { get; set; }

        public DateTime PromisedDate { get; set; }

        public decimal Amount { get; set; }

        public PromiseStatus Status { get; set; } = PromiseStatus.Open;

        public DateTime? ResolvedAtUtc { get; set; }
    }
}
