using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Schools
{
    /// <summary>
    /// core.AcademicYear (DB doc A1 pivotal spec; doc/Modules/03). The
    /// scoping dimension of all transactional data (ADR-3).
    /// </summary>
    [Audited(AuditTier.T2)]
    public class AcademicYear : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public string LabelEn { get; set; } = string.Empty;

        public string LabelAr { get; set; } = string.Empty;

        public string HijriLabel { get; set; } = string.Empty;

        public AcademicYearStatus Status { get; set; } = AcademicYearStatus.Preparation;

        /// <summary>BR-AYR-005 closing window end; null outside Closing.</summary>
        public DateTime? ClosingEndsOn { get; set; }
    }
}
