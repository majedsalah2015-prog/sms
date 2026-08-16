using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Fees
{
    /// <summary>
    /// ppl.CreditNote (doc/Modules/19 §7, BR-FEE-003): the only correction
    /// path against an immutable posted Charge (BR-GLB-062) — doc 08's
    /// strict "CRN" series (already seeded by E-010).
    /// </summary>
    [Audited(AuditTier.T1)]
    public class CreditNote : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int ChargeId { get; set; }

        /// <summary>doc 08 CRN series.</summary>
        public string CreditNoteNo { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string Reason { get; set; } = string.Empty;

        public DateTime IssuedAtUtc { get; set; }
    }
}
