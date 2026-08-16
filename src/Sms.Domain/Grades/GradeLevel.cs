using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Grades
{
    /// <summary>
    /// core.GradeLevel (doc/Modules/05 §7, BR-GRD-001/002/007): stable
    /// catalog entry — code, order, promotion target. Year-to-year
    /// variability (curriculum, gender, age rule, capacity) lives on
    /// <see cref="GradeYearProfile"/>, the year-versioning vehicle
    /// (BR-GRD-008); GradeLevel itself is deactivatable-only once any
    /// GradeYearProfile has enrollment (BR-GRD-007), never deleted.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class GradeLevel : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public int StageId { get; set; }

        public string Code { get; set; } = string.Empty;

        public LocalizedName Name { get; set; } = new();

        public int SequenceOrder { get; set; }

        /// <summary>Null only when IsGraduating — every other grade must declare its next grade (BR-GRD-002).</summary>
        public int? PromotionTargetGradeLevelId { get; set; }

        public bool IsGraduating { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
