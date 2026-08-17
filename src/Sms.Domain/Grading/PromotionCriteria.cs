using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Grading
{
    /// <summary>
    /// core.PromotionCriteria (doc/Modules/17 §7, BR-GRA-006): pass/promotion
    /// rules per grade-year, feeding BR-AYR-008's rollover step 3 (the
    /// actual rollover consumption isn't wired — same cross-module
    /// deferral precedent as E-103's PromotionPathValidator). Per-subject
    /// minimums and makeup-exam gates (BR-EXM-008) are doc-listed but not
    /// modeled here — this slice covers the overall pass mark + max
    /// failed-subjects gate only.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class PromotionCriteria : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int GradeYearProfileId { get; set; }

        [RequiresAuditReason]
        public decimal OverallPassMark { get; set; }

        [RequiresAuditReason]
        public int MaxFailedSubjectsForPromotion { get; set; }
    }
}
