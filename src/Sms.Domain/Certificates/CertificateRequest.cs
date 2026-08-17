using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Certificates
{
    /// <summary>
    /// ppl.CertificateRequest (doc/Modules/18 §7, BR-CRT-003): WF-09's
    /// workflow-managed request row. T1 so BR-CRT-008's Principal
    /// clearance override is reason-required via the generic audit
    /// captor — the override flips <see cref="ClearanceOverridden"/> on
    /// an already-existing row (an EF Modified transition), so
    /// RequiresAuditReason fires exactly on the override, never on the
    /// initial request.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class CertificateRequest : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int CertificateTypeId { get; set; }

        public int StudentId { get; set; }

        public int RequestedByUserId { get; set; }

        public CertificateRequestStatus Status { get; set; } = CertificateRequestStatus.Requested;

        public DateTime RequestedAtUtc { get; set; }

        public string? RejectionReason { get; set; }

        /// <summary>BR-CRT-008: Principal override of a failed clearance check (T1 + reason). Feeds the "Clearance-override register" report (not built).</summary>
        [RequiresAuditReason]
        public bool ClearanceOverridden { get; set; }

        public string? ClearanceOverrideReason { get; set; }
    }
}
