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

        /// <summary>
        /// BR-AYR-009 (S8/E-801): true when this note closes a source-year
        /// charge's remaining receivable so it can be re-posted as an
        /// OpeningBalance charge in the next year. Position readers treat it
        /// like any credit note (the money moves, it isn't forgiven); the GL
        /// export skips carry-forward pairs entirely — receivable→receivable
        /// is a nil journal, and treating it as a revenue/VAT reversal would
        /// misstate both.
        /// </summary>
        public bool IsCarryForward { get; set; }

        /// <summary>
        /// BR-INS-010 (gap G-6): true when this note relieves a receivable the
        /// school has given up collecting, rather than one it is reversing.
        /// <para>
        /// The distinction is the whole entry. An ordinary credit note says the
        /// charge was wrong, so revenue and its VAT come back out. A write-off
        /// says the charge was right and the money is not coming — revenue stays
        /// recognised and the loss is a bad-debt expense. Same document, opposite
        /// stories, and posting one as the other overstates or understates revenue
        /// every time it happens.
        /// </para>
        /// <para>
        /// Position readers treat it like any other credit note: the receivable is
        /// gone either way, and a family should not be chased for it.
        /// </para>
        /// </summary>
        public bool IsWriteOff { get; set; }
    }
}
