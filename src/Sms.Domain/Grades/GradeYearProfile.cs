using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Grades
{
    /// <summary>
    /// core.GradeYearProfile (doc/Modules/05 §7, BR-GRD-003/004/005/006/008):
    /// grade × academic year — the year-versioning vehicle. Rollover copies
    /// the active year's profiles into the Preparation year (adjustable
    /// there without touching the running year); Enrollment (S2) references
    /// this row, freezing historical structure naturally.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class GradeYearProfile : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int GradeLevelId { get; set; }

        /// <summary>BR-GRD-003: lookup-backed (core.LookupValue, category e.g. "Curriculum") — national/American/IGCSE/IB/custom.</summary>
        public int? CurriculumLookupValueId { get; set; }

        /// <summary>BR-GRD-004: narrows the stage's default, never widens.</summary>
        public GenderPolicy GenderPolicy { get; set; } = GenderPolicy.Mixed;

        /// <summary>BR-GRD-005: age in years at the cutoff date; both null = no age rule configured.</summary>
        public decimal? MinAgeAtCutoff { get; set; }

        public decimal? MaxAgeAtCutoff { get; set; }

        public DateTime? AgeCutoffDate { get; set; }

        /// <summary>BR-GRD-006: planned seats = TargetSections × TargetSectionSize.</summary>
        public int TargetSections { get; set; }

        public int TargetSectionSize { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
