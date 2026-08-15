using System;
using System.Collections.Generic;
using Sms.Application.Attachments;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-ATT-002/003/008: the upload fails one or more policy checks.</summary>
    public class AttachmentPolicyViolationException : InvalidOperationException
    {
        public AttachmentPolicyViolationException(IReadOnlyList<UploadLimitViolation> violations)
            : base($"Attachment upload does not meet policy (BR-ATT-002/003/008): {string.Join(", ", violations)}.")
        {
            Violations = violations;
        }

        public IReadOnlyList<UploadLimitViolation> Violations { get; }
    }

    /// <summary>BR-ATT-009: a quarantined or unscanned version can never be read or verified.</summary>
    public class AttachmentQuarantinedException : InvalidOperationException
    {
        public AttachmentQuarantinedException(int attachmentId)
            : base($"Attachment {attachmentId} is quarantined or not yet scanned (BR-ATT-009).")
        {
        }
    }

    /// <summary>BR-ATT-001: every attachment must resolve to a registered document type — untyped uploads are impossible.</summary>
    public class DocumentTypeNotFoundException : InvalidOperationException
    {
        public DocumentTypeNotFoundException(string code)
            : base($"No active document type '{code}' (BR-ATT-001).")
        {
        }
    }
}
