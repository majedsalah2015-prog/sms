namespace Sms.Application.Attachments
{
    /// <summary>
    /// Pure BR-ATT-004: access inherits from the owning entity AND respects
    /// the document type's restricted-category flag. Both inputs are
    /// resolved by the caller — "sees the owning entity" is module-specific
    /// (doc 06 scope resolution), "has restricted-category access" is
    /// BR-GLB-072/090 permission evaluation — this function only combines
    /// them.
    /// </summary>
    public static class AttachmentAccessEvaluator
    {
        public static bool CanView(bool seesOwningEntity, bool documentTypeIsRestricted, bool hasRestrictedCategoryAccess)
        {
            if (!seesOwningEntity)
            {
                return false;
            }

            return !documentTypeIsRestricted || hasRestrictedCategoryAccess;
        }
    }
}
