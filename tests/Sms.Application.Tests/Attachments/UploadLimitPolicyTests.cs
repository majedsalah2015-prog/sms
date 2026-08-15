using Sms.Application.Attachments;
using Sms.Domain.Attachments;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Attachments
{
    public class UploadLimitPolicyTests
    {
        private static DocumentType MakeType(
            DocumentFormat allowed = DocumentFormat.Pdf | DocumentFormat.Jpg,
            int? maxSizeBytes = null,
            bool isExpiryTracked = false)
            => new() { Code = "T", AllowedFormats = allowed, MaxSizeBytes = maxSizeBytes, IsExpiryTracked = isExpiryTracked };

        [Fact]
        [BusinessRule("BR-ATT-002")]
        public void A_compliant_upload_has_no_violations()
        {
            var violations = UploadLimitPolicy.Validate(MakeType(), DocumentFormat.Pdf, 1024, expiryDateProvided: false);

            Assert.Empty(violations);
        }

        [Fact]
        [BusinessRule("BR-ATT-002")]
        public void A_format_outside_the_allowed_set_is_rejected()
        {
            var violations = UploadLimitPolicy.Validate(MakeType(allowed: DocumentFormat.Pdf), DocumentFormat.Docx, 1024, expiryDateProvided: false);

            Assert.Contains(UploadLimitViolation.FormatNotAllowed, violations);
        }

        [Fact]
        [BusinessRule("BR-ATT-003")]
        public void Default_size_limit_is_the_product_default_of_10_MB()
        {
            var justUnder = UploadLimitPolicy.Validate(MakeType(), DocumentFormat.Pdf, UploadLimitPolicy.ProductDefaultSizeBytes, expiryDateProvided: false);
            var justOver = UploadLimitPolicy.Validate(MakeType(), DocumentFormat.Pdf, UploadLimitPolicy.ProductDefaultSizeBytes + 1, expiryDateProvided: false);

            Assert.Empty(justUnder);
            Assert.Contains(UploadLimitViolation.ExceedsTypeSizeLimit, justOver);
        }

        [Fact]
        [BusinessRule("BR-ATT-003")]
        public void A_per_type_limit_above_the_product_ceiling_is_capped_at_the_ceiling()
        {
            var overCeiling = UploadLimitPolicy.ProductCeilingSizeBytes + 1;
            var violations = UploadLimitPolicy.Validate(
                MakeType(maxSizeBytes: (int)(UploadLimitPolicy.ProductCeilingSizeBytes + 1_000_000)), DocumentFormat.Pdf, overCeiling, expiryDateProvided: false);

            Assert.Contains(UploadLimitViolation.ExceedsProductSizeCeiling, violations);
        }

        [Fact]
        [BusinessRule("BR-ATT-003")]
        public void A_tighter_per_type_limit_is_honored_below_the_ceiling()
        {
            var violations = UploadLimitPolicy.Validate(MakeType(maxSizeBytes: 1000), DocumentFormat.Pdf, 1001, expiryDateProvided: false);

            Assert.Contains(UploadLimitViolation.ExceedsTypeSizeLimit, violations);
        }

        [Fact]
        [BusinessRule("BR-ATT-008")]
        public void An_expiry_tracked_type_requires_an_expiry_date_at_upload()
        {
            var violations = UploadLimitPolicy.Validate(MakeType(isExpiryTracked: true), DocumentFormat.Pdf, 1024, expiryDateProvided: false);

            Assert.Contains(UploadLimitViolation.ExpiryDateRequired, violations);
        }

        [Fact]
        [BusinessRule("BR-ATT-008")]
        public void A_non_expiry_tracked_type_does_not_require_a_date()
        {
            var violations = UploadLimitPolicy.Validate(MakeType(isExpiryTracked: false), DocumentFormat.Pdf, 1024, expiryDateProvided: false);

            Assert.DoesNotContain(UploadLimitViolation.ExpiryDateRequired, violations);
        }
    }
}
