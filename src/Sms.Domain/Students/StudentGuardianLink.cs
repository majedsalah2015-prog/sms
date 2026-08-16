using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Students
{
    /// <summary>
    /// ppl.StudentGuardianLink (DB doc A4 pivotal spec 🔒custody; doc/Modules/10
    /// §7 = doc/Modules/11's ParentStudentLink — one table serves both the
    /// student-side and parent-side perspective). BR-STU-003/BR-PAR-009:
    /// links and flags are T1-audited — they control money and child access.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class StudentGuardianLink : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int StudentId { get; set; }

        public int ParentId { get; set; }

        /// <summary>References core.LookupValue, category "RelationshipType" (seeded by E-010).</summary>
        public int RelationshipLookupId { get; set; }

        [RequiresAuditReason]
        public bool IsPrimaryContact { get; set; }

        [RequiresAuditReason]
        public bool IsFinanciallyResponsible { get; set; }

        [RequiresAuditReason]
        public bool IsPickupAuthorized { get; set; }

        [RequiresAuditReason]
        public bool IsPortalVisible { get; set; } = true;

        /// <summary>References doc.Attachment — mandatory when the relationship is a non-parent guardian (BR-STU-003).</summary>
        public int? GuardianshipDocAttachmentId { get; set; }

        public DateTime EffectiveFromUtc { get; set; }

        public DateTime? EffectiveToUtc { get; set; }
    }
}
