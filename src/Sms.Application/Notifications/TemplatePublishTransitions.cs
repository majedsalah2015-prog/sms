using Sms.Domain.Notifications;

namespace Sms.Application.Notifications
{
    /// <summary>Pure BR-NTF-001 spine: Draft -> TestSent (mandatory) -> Published. A further edit always creates a new TemplateVersion starting back at Draft — this table never allows leaving Published.</summary>
    public static class TemplatePublishTransitions
    {
        public static bool CanTransition(TemplatePublishStatus from, TemplatePublishStatus to)
        {
            return (from, to) switch
            {
                (TemplatePublishStatus.Draft, TemplatePublishStatus.TestSent) => true,
                (TemplatePublishStatus.TestSent, TemplatePublishStatus.Published) => true,
                _ => false,
            };
        }
    }
}
