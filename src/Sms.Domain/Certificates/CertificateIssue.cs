using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Certificates
{
    /// <summary>
    /// ppl.CertificateIssue (doc/Modules/18 §7, BR-CRT-002/003/004): the
    /// permanent issuance register row. Generation is atomic with
    /// numbering (BR-WF-009, same "materializes only on commit" pattern
    /// as every other E-006 numbering consumer) — no unnumbered official
    /// output exists. PDF rendering (BR-CRT-002's actual document) needs
    /// the O6 engine decision, same block as E-302's report cards — this
    /// entity carries the data a PDF would render from
    /// (DataSnapshotJson), not a PDF reference.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class CertificateIssue : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int CertificateRequestId { get; set; }

        public int CertificateTypeId { get; set; }

        public int StudentId { get; set; }

        /// <summary>doc 08 series per CertificateType.NumberingSeriesCode (CERT/TC/…).</summary>
        public string CertificateNo { get; set; } = string.Empty;

        /// <summary>BR-CRT-004: frozen at issuance — reprints reproduce this exactly, never live data.</summary>
        public string DataSnapshotJson { get; set; } = string.Empty;

        /// <summary>BR-CRT-005: resolves via the public verification endpoint (not built — screens deferred).</summary>
        public string VerificationCode { get; set; } = string.Empty;

        public CertificateIssueStatus Status { get; set; } = CertificateIssueStatus.Issued;

        public DateTime IssuedAtUtc { get; set; }

        public DateTime? RevokedAtUtc { get; set; }

        public string? RevokedReason { get; set; }

        /// <summary>BR-CRT-007: original prints once; each subsequent print increments this (the "True Copy" watermark itself is a render-layer concern).</summary>
        public int ReprintCount { get; set; }

        /// <summary>BR-CRT-001: IssuedAtUtc + CertificateType.ValidityDays; null = never expires. Printed where the type expires (doc §9); verification consumers compare against now.</summary>
        public DateTime? ExpiresAtUtc { get; set; }

        /// <summary>BR-CRT-004: set when this is a current-data reissue — a NEW certificate (new number, fresh snapshot) superseding the referenced one, which is optionally revoked.</summary>
        public int? ReissuedFromCertificateIssueId { get; set; }
    }
}
