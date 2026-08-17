using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Examinations
{
    /// <summary>
    /// core.ExamIncident (doc/Modules/16 §7, BR-EXM-007): cheating/disruption
    /// log per sitting — restricted (🔒). Academic mark-treatment decisions
    /// and the automatic Module 25 (Discipline) case linkage aren't wired —
    /// Discipline doesn't exist yet.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class ExamIncident : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int ExamSittingId { get; set; }

        public int EnrollmentId { get; set; }

        public string Category { get; set; } = string.Empty;

        public string Narrative { get; set; } = string.Empty;

        public int RecordedByUserId { get; set; }

        public DateTime RecordedAtUtc { get; set; }
    }
}
