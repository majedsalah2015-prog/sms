namespace Sms.Domain.Notifications
{
    /// <summary>BR-NTF-001: Draft -> TestSent (mandatory before first publish) -> Published (immutable — a further edit creates a new TemplateVersion starting back at Draft, never mutates a Published row).</summary>
    public enum TemplatePublishStatus : short
    {
        Draft = 1,
        TestSent = 2,
        Published = 3,
    }
}
