using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Grading
{
    /// <summary>
    /// core.Blueprint (doc/Modules/17 §7, BR-GRA-003): per offering per
    /// term, weighted components -> term score. Components in this slice
    /// are generic named weights (e.g. "Quiz 1", "Midterm") rather than
    /// linked to Module 16 exam sessions, which don't exist yet (S4) —
    /// continuous-assessment-only, same deferral category as this
    /// codebase's other not-yet-built-module gaps.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class Blueprint : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int CurriculumOfferingId { get; set; }

        public int TermId { get; set; }

        public int GradingScaleId { get; set; }

        /// <summary>BR-GRA-004 open question #1: doc's proposed default is "reduce denominator" for exempted marks — this flag lets a school opt into weight-redistribution instead. Redistribution itself is not implemented in this slice (default path only).</summary>
        public bool RedistributeWeightOnExemption { get; set; }

        /// <summary>Locks once weights sum to exactly 100 and FinalizeAsync is called — mirrors the CurriculumOffering/HomeroomAssignment "locks on first real use" family of patterns.</summary>
        public bool IsLocked { get; set; }
    }
}
