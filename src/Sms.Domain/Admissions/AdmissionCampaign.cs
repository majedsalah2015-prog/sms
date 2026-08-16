using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Admissions
{
    /// <summary>
    /// ppl.AdmissionCampaign (doc/Modules/09 §7, BR-ADM-001): school × year ×
    /// grade window. One row targets one grade-year profile — a school
    /// offering several grades runs several campaign rows, one per grade,
    /// rather than a single row fanning out to many grades.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class AdmissionCampaign : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int GradeYearProfileId { get; set; }

        public DateTime OpenDate { get; set; }

        public DateTime CloseDate { get; set; }

        public bool RequiresAssessment { get; set; }

        /// <summary>Null = no application fee for this campaign (BR-ADM-008).</summary>
        public decimal? ApplicationFeeAmount { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
