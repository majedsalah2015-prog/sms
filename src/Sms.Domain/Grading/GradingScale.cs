using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Grading
{
    /// <summary>
    /// core.GradingScale (doc/Modules/17 §7, BR-GRA-001): band table per
    /// stage (+ optional curriculum narrowing), year-versioned, locks once
    /// a published TermResult references it. **Percentage-band type only**
    /// in this slice — GPA-only/IGCSE-letter/KG-rubric scale variants the
    /// doc also lists are deferred (ScaleBand's GpaPoints field is generic
    /// enough that a percentage scale can still carry GPA points, so the
    /// common case isn't blocked).
    /// </summary>
    [Audited(AuditTier.T1)]
    public class GradingScale : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int StageId { get; set; }

        /// <summary>BR-GRA-001: optional narrowing — core.LookupValue, category "Curriculum" (E-103's GradeYearProfile.CurriculumLookupValueId reused).</summary>
        public int? CurriculumLookupValueId { get; set; }

        [RequiresAuditReason]
        public string NameAr { get; set; } = string.Empty;

        [RequiresAuditReason]
        public string NameEn { get; set; } = string.Empty;

        public bool IsLocked { get; set; }
    }
}
