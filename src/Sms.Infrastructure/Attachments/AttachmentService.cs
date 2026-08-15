using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Attachments;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Attachments;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Attachments
{
    /// <summary>
    /// doc 10 §2 upload/version/verify/void pipeline. A standalone operation
    /// — every method saves itself (no larger business transaction to ride,
    /// unlike numbering/notifications).
    /// </summary>
    public class AttachmentService : IAttachmentService
    {
        private readonly AppDbContext _db;
        private readonly IFileStore _fileStore;
        private readonly IVirusScanner _virusScanner;
        private readonly IClock _clock;

        public AttachmentService(AppDbContext db, IFileStore fileStore, IVirusScanner virusScanner, IClock clock)
        {
            _db = db;
            _fileStore = fileStore;
            _virusScanner = virusScanner;
            _clock = clock;
        }

        public async Task<AttachmentVersion> UploadAsync(
            string documentTypeCode,
            string owningEntityType,
            long owningEntityId,
            byte[] content,
            string fileName,
            DocumentFormat format,
            string? titleAr = null,
            string? titleEn = null,
            DateTime? expiryDateUtc = null,
            CancellationToken cancellationToken = default)
        {
            var documentType = await _db.DocumentTypes.SingleOrDefaultAsync(
                t => t.Code == documentTypeCode && t.IsActive, cancellationToken);
            if (documentType == null)
            {
                throw new DocumentTypeNotFoundException(documentTypeCode);
            }

            var violations = UploadLimitPolicy.Validate(documentType, format, content.LongLength, expiryDateUtc.HasValue);
            if (violations.Count > 0)
            {
                throw new AttachmentPolicyViolationException(violations);
            }

            // Re-upload for the same (owning entity, type) slot versions the existing
            // Attachment (doc 10 §2 "Version"); a voided slot starts a fresh one.
            var attachment = await _db.Attachments.SingleOrDefaultAsync(
                a => a.OwningEntityType == owningEntityType
                     && a.OwningEntityId == owningEntityId
                     && a.DocumentTypeId == documentType.Id
                     && a.Status != AttachmentStatus.Void,
                cancellationToken);

            if (attachment == null)
            {
                attachment = new Attachment
                {
                    DocumentTypeId = documentType.Id,
                    OwningEntityType = owningEntityType,
                    OwningEntityId = owningEntityId,
                };
                _db.Attachments.Add(attachment);
            }

            attachment.TitleAr = titleAr;
            attachment.TitleEn = titleEn;
            attachment.ExpiryDateUtc = expiryDateUtc;
            attachment.CurrentVersionNumber += 1;
            attachment.Status = AttachmentStatus.PendingScan;
            // A re-upload replaces the content that was verified — verification does not carry over.
            attachment.VerifiedByUserId = null;
            attachment.VerifiedAtUtc = null;

            var storageReference = await _fileStore.SaveAsync(content, fileName, cancellationToken);
            var version = new AttachmentVersion
            {
                VersionNumber = attachment.CurrentVersionNumber,
                FileName = fileName,
                Format = format,
                SizeBytes = content.LongLength,
                ContentHash = ComputeHash(content),
                StorageReference = storageReference,
                ScanStatus = ScanStatus.Pending,
            };
            attachment.Versions.Add(version);

            // Inline for v1 (no queue infra yet) — a real scan queue behind IVirusScanner
            // is a later swap (E-011), not a change to this pipeline's shape.
            var scanResult = await _virusScanner.ScanAsync(content, cancellationToken);
            version.ScanStatus = scanResult;
            attachment.Status = scanResult == ScanStatus.Clean ? AttachmentStatus.Active : AttachmentStatus.Quarantined;

            await _db.SaveChangesAsync(cancellationToken);
            return version;
        }

        public async Task<byte[]> ReadCurrentVersionAsync(int attachmentId, CancellationToken cancellationToken = default)
        {
            var version = await CurrentCleanVersionAsync(attachmentId, cancellationToken);
            return await _fileStore.ReadAsync(version.StorageReference, cancellationToken);
        }

        public async Task VerifyAsync(int attachmentId, int verifiedByUserId, CancellationToken cancellationToken = default)
        {
            await CurrentCleanVersionAsync(attachmentId, cancellationToken);

            var attachment = await _db.Attachments.SingleAsync(a => a.Id == attachmentId, cancellationToken);
            attachment.VerifiedByUserId = verifiedByUserId;
            attachment.VerifiedAtUtc = _clock.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task VoidAsync(int attachmentId, string reason, CancellationToken cancellationToken = default)
        {
            var attachment = await _db.Attachments.SingleAsync(a => a.Id == attachmentId, cancellationToken);
            attachment.Status = AttachmentStatus.Void;
            attachment.VoidReason = reason;
            attachment.VoidedAtUtc = _clock.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
        }

        private async Task<AttachmentVersion> CurrentCleanVersionAsync(int attachmentId, CancellationToken cancellationToken)
        {
            var attachment = await _db.Attachments.SingleAsync(a => a.Id == attachmentId, cancellationToken);
            var version = await _db.AttachmentVersions.SingleAsync(
                v => v.AttachmentId == attachmentId && v.VersionNumber == attachment.CurrentVersionNumber, cancellationToken);

            if (version.ScanStatus != ScanStatus.Clean)
            {
                throw new AttachmentQuarantinedException(attachmentId);
            }

            return version;
        }

        private static string ComputeHash(byte[] content)
        {
            using var sha256 = SHA256.Create();
            return Convert.ToHexString(sha256.ComputeHash(content));
        }
    }
}
