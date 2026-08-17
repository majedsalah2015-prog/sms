using System;
using System.Collections.Generic;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Installments
{
    /// <summary>
    /// ppl.Installment (doc/Modules/20 §7): one dated amount of a
    /// schedule. Deliberately has NO status column — BR-INS-007 says
    /// status derives from Module 21 allocations and dates and is never
    /// set manually; the only stored lifecycle facts are the two terminal
    /// flags that can't be derived (superseded by a reschedule, written
    /// off under WF-06). Paid amount is derived by walking the linked
    /// charges' allocations through the schedule in due-date order
    /// (InstallmentPaymentWaterfall) so Module 21 stays the single source
    /// of payment truth. T1 per BR-INS-010; WriteOffReason is
    /// reason-required (WF-06 write-off is a T1 money event) — the flag
    /// only ever flips on an existing row.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class Installment : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int PlanAssignmentId { get; set; }

        public int SequenceNumber { get; set; }

        /// <summary>BR-INS-004: already shifted to a working day at generation.</summary>
        public DateTime DueDate { get; set; }

        public decimal Amount { get; set; }

        /// <summary>BR-INS-005: superseded by an approved reschedule — kept in history, derives to Rescheduled.</summary>
        public bool IsSuperseded { get; set; }

        /// <summary>WF-06 write-off — derives to WrittenOff.</summary>
        [RequiresAuditReason]
        public bool IsWrittenOff { get; set; }

        public string? WriteOffReason { get; set; }

        /// <summary>BR-INS-009: covered by a lodged PDC (Module 21 registry) — not Paid, but suppresses dunning; cleared on bounce.</summary>
        public int? CoveringPdcId { get; set; }

        public List<InstallmentChargeLine> ChargeLines { get; set; } = new();
    }
}
