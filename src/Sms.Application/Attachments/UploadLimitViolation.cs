namespace Sms.Application.Attachments
{
    public enum UploadLimitViolation
    {
        FormatNotAllowed,
        ExceedsTypeSizeLimit,
        ExceedsProductSizeCeiling,
        ExpiryDateRequired,
    }
}
