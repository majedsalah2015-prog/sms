using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Grading
{
    /// <summary>
    /// core.YearResult (doc/Modules/17 §7, BR-GRA-006/007): year
    /// aggregation across a student's TermResults for a grade-year —
    /// GPA + the promotion proposal outcome. A separate persisted `Rank`
    /// entity (doc's own DB concept) is simplified away here — ranking is
    /// computed on demand from a section/grade's YearResult rows via the
    /// pure RankCalculator rather than stored, since nothing yet needs a
    /// frozen historical rank snapshot the way a published TermResult does.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class YearResult : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int EnrollmentId { get; set; }

        public decimal Gpa { get; set; }

        public int FailedSubjectCount { get; set; }

        public PromotionOutcome PromotionOutcome { get; set; }

        public DateTime ComputedAtUtc { get; set; }
    }
}
