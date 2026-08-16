using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Grading
{
    /// <summary>core.Marksheet (doc/Modules/17 §7, BR-GRA-005): one blueprint x section instance moving through WF-07.</summary>
    [Audited(AuditTier.T2)]
    public class Marksheet : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int BlueprintId { get; set; }

        public int SectionId { get; set; }

        public MarksheetStatus Status { get; set; } = MarksheetStatus.Draft;

        public int? SubmittedByUserId { get; set; }

        public DateTime? SubmittedAtUtc { get; set; }

        public int? ReviewedByUserId { get; set; }

        public DateTime? ReviewedAtUtc { get; set; }

        public int? ApprovedByUserId { get; set; }

        public DateTime? ApprovedAtUtc { get; set; }

        public DateTime? PublishedAtUtc { get; set; }
    }
}
