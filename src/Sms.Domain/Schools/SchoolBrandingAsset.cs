namespace Sms.Domain.Schools
{
    /// <summary>
    /// Which branding slot a file fills (BR-SCH-006, doc/Modules/02 §8.1). Two slots, not a
    /// free-form gallery: an official document has one place for a logo and one for a seal, and
    /// naming them is what lets a template ask for "the seal" rather than "attachment 41".
    /// </summary>
    public enum SchoolBrandingAsset : short
    {
        /// <summary>The mark that heads a document.</summary>
        Logo = 1,

        /// <summary>The stamp that authenticates one.</summary>
        Seal = 2,
    }
}
