using System;
using System.Collections.Generic;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Attachments
{
    /// <summary>
    /// doc.Attachment (doc 10 §2): the logical document — one row per
    /// (owning entity, document type) slot; re-upload adds an
    /// <see cref="AttachmentVersion"/> rather than a new Attachment
    /// (doc 10 §2 "Version"). Never IActivatable — BR-ATT-007 voiding is a
    /// status, not a deactivation, and BR-ATT-011 purge is a deliberate,
    /// certificate-logged physical delete that the generic hard-delete guard
    /// must NOT block.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class Attachment : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int DocumentTypeId { get; set; }

        public string OwningEntityType { get; set; } = string.Empty;

        public long OwningEntityId { get; set; }

        public string? TitleAr { get; set; }

        public string? TitleEn { get; set; }

        public string? NotesAr { get; set; }

        public string? NotesEn { get; set; }

        public AttachmentStatus Status { get; set; } = AttachmentStatus.PendingScan;

        public int CurrentVersionNumber { get; set; }

        /// <summary>BR-ATT-008: required at upload when the document type is expiry-tracked.</summary>
        public DateTime? ExpiryDateUtc { get; set; }

        /// <summary>doc 10 §2 "Verification".</summary>
        public int? VerifiedByUserId { get; set; }

        public DateTime? VerifiedAtUtc { get; set; }

        public string? VoidReason { get; set; }

        public DateTime? VoidedAtUtc { get; set; }

        public List<AttachmentVersion> Versions { get; } = new();
    }
}
