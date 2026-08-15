using System;
using System.Collections.Generic;
using Sms.Domain.Attachments;

namespace Sms.Application.Attachments
{
    /// <summary>Pure BR-ATT-002/003/008 upload-time checks.</summary>
    public static class UploadLimitPolicy
    {
        public const long ProductDefaultSizeBytes = 10L * 1024 * 1024;

        public const long ProductCeilingSizeBytes = 25L * 1024 * 1024;

        public static IReadOnlyList<UploadLimitViolation> Validate(
            DocumentType documentType, DocumentFormat format, long sizeBytes, bool expiryDateProvided)
        {
            var violations = new List<UploadLimitViolation>();

            if ((documentType.AllowedFormats & format) == 0)
            {
                violations.Add(UploadLimitViolation.FormatNotAllowed);
            }

            var configuredLimit = documentType.MaxSizeBytes ?? ProductDefaultSizeBytes;
            var effectiveLimit = Math.Min(configuredLimit, ProductCeilingSizeBytes);
            if (sizeBytes > effectiveLimit)
            {
                violations.Add(configuredLimit > ProductCeilingSizeBytes
                    ? UploadLimitViolation.ExceedsProductSizeCeiling
                    : UploadLimitViolation.ExceedsTypeSizeLimit);
            }

            if (documentType.IsExpiryTracked && !expiryDateProvided)
            {
                violations.Add(UploadLimitViolation.ExpiryDateRequired);
            }

            return violations;
        }
    }
}
