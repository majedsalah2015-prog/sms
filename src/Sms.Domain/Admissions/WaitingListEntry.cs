using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Admissions
{
    /// <summary>
    /// ppl.WaitingListEntry (doc/Modules/09 §7, BR-ADM-006). Ordering policy
    /// in v1 is simple submission-order (OrderRank assigned sequentially at
    /// creation) — sibling-priority and assessment-ranked ordering are
    /// deferred (need Module 22 sibling-discount linkage / configurable
    /// policy, doc's own open question #2).
    /// </summary>
    [Audited(AuditTier.T2)]
    public class WaitingListEntry : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int ApplicationId { get; set; }

        public int GradeYearProfileId { get; set; }

        public int OrderRank { get; set; }

        public DateTime? OfferedAtUtc { get; set; }

        public DateTime? OfferExpiresAtUtc { get; set; }

        public bool? IsOfferAccepted { get; set; }
    }
}
