using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Certificates;

namespace Sms.Application.Certificates
{
    /// <summary>
    /// doc/Modules/18 §8 Issuance desk / Certificate register / Bulk
    /// issuance wizard / Public verification page screens backing
    /// (screens deferred, the operations are core). Employee service
    /// certificates (BR-EMP-008) and report-card official copies
    /// (BR-GRA-008) are meant to register through this same engine per
    /// the doc's "one register for everything official" — neither
    /// integration is wired in this slice (both source modules already
    /// defer their own side of it). PDF rendering needs the O6 engine
    /// decision, same block as E-302's report cards.
    /// </summary>
    public interface ICertificateAdmin
    {
        /// <summary>
        /// Throws <see cref="Common.Exceptions.CertificateKindNotGateableException"/> when the country pack forbids
        /// gating this kind, <see cref="Common.Exceptions.FeeClearanceRuleNotSupportedException"/> for NoOverdue (BR-CRT-008).
        /// </summary>
        Task<CertificateType> DefineTypeAsync(
            CertificateKind kind, string nameAr, string nameEn, bool requiresPublishedResults, FeeClearanceRule feeClearanceRule, bool isPortalRequestable,
            int? validityDays = null, string numberingSeriesCode = "CERT", CancellationToken cancellationToken = default);

        Task<CertificateRequest> RequestAsync(int certificateTypeId, int studentId, int requestedByUserId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Throws <see cref="Common.Exceptions.CertificatePrerequisitesNotMetException"/> (BR-CRT-001/003) —
        /// specifically its <see cref="Common.Exceptions.CertificateFeeClearanceBlockedException"/> subclass when only
        /// the fee check failed. Supplying <paramref name="clearanceOverrideReason"/> lets a Principal approve past a
        /// failed clearance check (BR-CRT-008: T1 + reason); it never bypasses the published-results prerequisite.
        /// </summary>
        Task ApproveAsync(int certificateRequestId, string? clearanceOverrideReason = null, CancellationToken cancellationToken = default);

        Task RejectAsync(int certificateRequestId, string reason, CancellationToken cancellationToken = default);

        /// <summary>BR-CRT-002/003: atomic with numbering — issues the real doc 08 number, freezes the data snapshot, stamps expiry per the type's validity.</summary>
        Task<CertificateIssue> IssueAsync(int certificateRequestId, CancellationToken cancellationToken = default);

        /// <summary>BR-CRT-004: current-data reissue — a NEW request+certificate (new number, fresh snapshot, prerequisites re-checked) linked back to the original, which is revoked with <paramref name="revokeOriginalReason"/> when supplied.</summary>
        Task<CertificateIssue> ReissueAsync(int certificateIssueId, string? revokeOriginalReason = null, CancellationToken cancellationToken = default);

        /// <summary>BR-CRT-009: one batch over every active enrollment of a grade-year profile — auto-check, individual numbers, exceptions queue for those that fail.</summary>
        Task<CertificateBatchResult> IssueBatchAsync(int certificateTypeId, int gradeYearProfileId, int requestedByUserId, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.CertificateNotIssuedException"/>. Reason mandatory (P2 Principal chain not enforced here).</summary>
        Task RevokeAsync(int certificateIssueId, string reason, CancellationToken cancellationToken = default);

        /// <summary>BR-CRT-007: increments the reprint count — the "True Copy" watermark itself is a render-layer concern.</summary>
        Task<CertificateIssue> ReprintAsync(int certificateIssueId, CancellationToken cancellationToken = default);

        /// <summary>BR-CRT-005: logs the hit; returns null if the code doesn't resolve to any issue.</summary>
        Task<CertificateIssue?> VerifyAsync(string verificationCode, CancellationToken cancellationToken = default);
    }
}
