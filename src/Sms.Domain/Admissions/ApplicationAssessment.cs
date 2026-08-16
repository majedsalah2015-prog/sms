using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Admissions
{
    /// <summary>ppl.ApplicationAssessment (doc/Modules/09 §7): a score entry against a configured criterion — kept as a single overall score in this slice.</summary>
    [Audited(AuditTier.T2)]
    public class ApplicationAssessment : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int ApplicationId { get; set; }

        public decimal Score { get; set; }

        public string? Notes { get; set; }

        public int AssessedByUserId { get; set; }

        public DateTime AssessedAtUtc { get; set; }
    }
}
