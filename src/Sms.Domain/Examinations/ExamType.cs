using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Examinations
{
    /// <summary>core.ExamType (doc/Modules/16 §7, BR-EXM-001): quiz/midterm/final/practical/oral/makeup… school-configurable.</summary>
    [Audited(AuditTier.T3)]
    public class ExamType : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        /// <summary>True = needs a Timetable/exam-period slot; false = classroom-level (e.g. a pop quiz).</summary>
        public bool IsScheduled { get; set; }

        public bool IsMakeupEligible { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
