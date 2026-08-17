using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Certificates
{
    /// <summary>ppl.VerificationLog (doc/Modules/18 §7, BR-CRT-005): a verification hit — no personal data beyond the certificate reference, feeding the "repeated failed codes" fraud-signal report (not built, screens deferred).</summary>
    [Audited(AuditTier.T3)]
    public class VerificationLog : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int? CertificateIssueId { get; set; }

        public string SubmittedCode { get; set; } = string.Empty;

        public bool WasFound { get; set; }

        public DateTime VerifiedAtUtc { get; set; }
    }
}
