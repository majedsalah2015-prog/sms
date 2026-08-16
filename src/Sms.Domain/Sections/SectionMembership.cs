using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Sections
{
    /// <summary>
    /// core.SectionMembership (BR-SCN-005/006): effective-dated enrollment ×
    /// section link — current section = the open-ended (EffectiveToUtc null)
    /// row. EnrollmentId is a forward reference: ppl.Enrollment (S2) doesn't
    /// exist yet, so it's an unconstrained int, matching
    /// Attachment.OwningEntityId's precedent — a real FK is added once
    /// Enrollment lands.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class SectionMembership : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int SectionId { get; set; }

        public int EnrollmentId { get; set; }

        public DateTime EffectiveFromUtc { get; set; }

        /// <summary>Null = the student's current section.</summary>
        public DateTime? EffectiveToUtc { get; set; }

        /// <summary>Null on the very first assignment — a transfer always carries one (balancing/behavioral/parent request/medical).</summary>
        public string? TransferReasonCode { get; set; }
    }
}
