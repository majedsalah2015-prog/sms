using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Attendance
{
    /// <summary>
    /// core.Justification (doc/Modules/14 §7, BR-ATD-005): parent-submitted
    /// excuse against a specific AttendanceDay. Medical requires a document
    /// (doc 10) — represented as an optional Attachment ref, not enforced
    /// as mandatory here (content/workflow-gate concern, deferred).
    /// </summary>
    [Audited(AuditTier.T2)]
    public class Justification : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int AttendanceDayId { get; set; }

        public JustificationType Type { get; set; }

        public DateTime SubmittedAtUtc { get; set; }

        public JustificationReviewState ReviewState { get; set; } = JustificationReviewState.Submitted;

        public int? ReviewedByUserId { get; set; }

        public DateTime? ReviewedAtUtc { get; set; }

        public string? RejectionReason { get; set; }

        public int? DocumentAttachmentId { get; set; }
    }
}
