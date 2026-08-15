using Sms.Domain.Common;

namespace Sms.Domain.Attachments
{
    /// <summary>
    /// doc.AttachmentVersion: one immutable row per upload (append-only, like
    /// TemplateVersion/PasswordHistory). BR-ATT-010: the database stores
    /// metadata + a content reference, never file bytes.
    /// </summary>
    public class AttachmentVersion : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int AttachmentId { get; set; }

        public int VersionNumber { get; set; }

        public string FileName { get; set; } = string.Empty;

        public DocumentFormat Format { get; set; }

        public long SizeBytes { get; set; }

        /// <summary>SHA-256 hex, BR-ATT-010 integrity check.</summary>
        public string ContentHash { get; set; } = string.Empty;

        /// <summary>Opaque key into the abstracted file store (T-7) — never a raw path exposed to callers.</summary>
        public string StorageReference { get; set; } = string.Empty;

        public ScanStatus ScanStatus { get; set; } = ScanStatus.Pending;
    }
}
