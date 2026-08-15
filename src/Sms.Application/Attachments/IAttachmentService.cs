using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Attachments;

namespace Sms.Application.Attachments
{
    /// <summary>
    /// doc 10 §2 upload/version/verify/void pipeline. A standalone operation
    /// (each method saves itself) — unlike numbering/notifications, an
    /// upload isn't riding some other business save. Access control
    /// (BR-ATT-004, <see cref="AttachmentAccessEvaluator"/>) is the caller's
    /// job before calling <see cref="ReadAsync"/>; this service only
    /// enforces the scan gate (BR-ATT-009).
    /// </summary>
    public interface IAttachmentService
    {
        /// <summary>Creates the Attachment on first upload for the (owning entity, document type) slot, or a new version on re-upload (doc 10 §2 "Version"). Throws <see cref="Common.Exceptions.AttachmentPolicyViolationException"/> on a format/size/expiry violation.</summary>
        Task<AttachmentVersion> UploadAsync(
            string documentTypeCode,
            string owningEntityType,
            long owningEntityId,
            byte[] content,
            string fileName,
            DocumentFormat format,
            string? titleAr = null,
            string? titleEn = null,
            DateTime? expiryDateUtc = null,
            CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.AttachmentQuarantinedException"/> unless the current version is Clean (BR-ATT-009).</summary>
        Task<byte[]> ReadCurrentVersionAsync(int attachmentId, CancellationToken cancellationToken = default);

        /// <summary>doc 10 §2 "Verification". Throws <see cref="Common.Exceptions.AttachmentQuarantinedException"/> unless the current version is Clean.</summary>
        Task VerifyAsync(int attachmentId, int verifiedByUserId, CancellationToken cancellationToken = default);

        /// <summary>BR-ATT-007: status change, never a physical delete while the owning record exists.</summary>
        Task VoidAsync(int attachmentId, string reason, CancellationToken cancellationToken = default);
    }
}
