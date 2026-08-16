using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Subjects
{
    /// <summary>
    /// core.CurriculumOffering (DB doc A5 pivotal spec; doc/Modules/07 §7,
    /// BR-SUB-002..005/008): grade-year profile × subject — the reference
    /// target for timetable sessions, marksheets, and assignments (never
    /// raw Subject, for year-correctness by construction). BR-SUB-004: an
    /// offering already referenced elsewhere is only end-dated
    /// (EffectiveToUtc), never removed.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class CurriculumOffering : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int GradeYearProfileId { get; set; }

        public int SubjectId { get; set; }

        public int WeeklyPeriods { get; set; }

        /// <summary>BR-SUB-003: non-assessable offerings (assembly, homeroom) are timetabled but never enter marks/GPA.</summary>
        public bool IsAssessable { get; set; }

        /// <summary>Neutral numeric weight — credit-hours-vs-percentage semantics are Module 17's, not this offering's.</summary>
        public decimal GpaWeight { get; set; }

        public bool IsElective { get; set; }

        public string? ElectiveGroupTag { get; set; }

        public DateTime EffectiveFromUtc { get; set; }

        public DateTime? EffectiveToUtc { get; set; }
    }
}
