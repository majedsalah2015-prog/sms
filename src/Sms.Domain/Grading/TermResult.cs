using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Grading
{
    /// <summary>
    /// core.TermResult (doc/Modules/17 §7, BR-GRA-003/004): computed-persisted
    /// per student per offering per term, produced when a Marksheet
    /// publishes. CalculationSnapshotJson stores the inputs/weights/scale
    /// version used, per BR-GRA-003's "permanent reproducibility"
    /// requirement — a plain JSON string blob, not a structured
    /// versioning system (that's more machinery than this basic slice
    /// needs). Year aggregation (YearResult), GPA, ranking, and promotion
    /// proposals are out of this "basic subset" epic — full Grading is
    /// S4/E-402.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class TermResult : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int EnrollmentId { get; set; }

        public int CurriculumOfferingId { get; set; }

        public int TermId { get; set; }

        public decimal ScorePercent { get; set; }

        public int? ScaleBandId { get; set; }

        public string CalculationSnapshotJson { get; set; } = string.Empty;

        public DateTime PublishedAtUtc { get; set; }
    }
}
