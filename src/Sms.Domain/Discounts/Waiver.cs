using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Discounts
{
    /// <summary>
    /// ppl.Waiver (doc/Modules/22 §7, BR-DIS-006): operational forgiveness
    /// of a late fee / bounce fee / misc charge — WF-06 family, P2 or P4
    /// by amount. Kept in its own register, separate from discounts. On
    /// approval it materializes as a real E-303 CreditNote against the
    /// charge (CreditNoteId) so every position reader already honours it.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class Waiver : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int ChargeId { get; set; }

        public WaiverKind Kind { get; set; }

        public decimal Amount { get; set; }

        public string Reason { get; set; } = string.Empty;

        public ApprovalTier RequiredTier { get; set; }

        public WaiverStatus Status { get; set; } = WaiverStatus.Proposed;

        public int ProposedByUserId { get; set; }

        public int? DecidedByUserId { get; set; }

        public DateTime? DecidedAtUtc { get; set; }

        public int? CreditNoteId { get; set; }
    }
}
