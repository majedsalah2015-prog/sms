using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Discounts
{
    /// <summary>
    /// ppl.RenewalQueueItem (doc/Modules/22 §7, BR-DIS-007): a
    /// manual/scholarship grant of the closing year awaiting an
    /// approve/adjust/drop decision before anything applies in the new
    /// year — nothing carries silently (BR-GLB-023 spirit).
    /// </summary>
    [Audited(AuditTier.T2)]
    public class RenewalQueueItem : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int PriorGrantId { get; set; }

        public int NewAcademicYearId { get; set; }

        public RenewalDecision Decision { get; set; } = RenewalDecision.Pending;

        public decimal? AdjustedBasisValue { get; set; }

        public int? DecidedByUserId { get; set; }

        public DateTime? DecidedAtUtc { get; set; }

        /// <summary>The new-year grant created by an Approved/Adjusted decision.</summary>
        public int? NewGrantId { get; set; }
    }
}
