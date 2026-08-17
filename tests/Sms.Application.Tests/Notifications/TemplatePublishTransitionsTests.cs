using Sms.Application.Notifications;
using Sms.Domain.Notifications;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Notifications
{
    public class TemplatePublishTransitionsTests
    {
        [Theory]
        [InlineData(TemplatePublishStatus.Draft, TemplatePublishStatus.TestSent)]
        [InlineData(TemplatePublishStatus.TestSent, TemplatePublishStatus.Published)]
        [BusinessRule("BR-NTF-001")]
        public void Legal_moves_are_allowed(TemplatePublishStatus from, TemplatePublishStatus to)
        {
            Assert.True(TemplatePublishTransitions.CanTransition(from, to));
        }

        [Theory]
        [InlineData(TemplatePublishStatus.Draft, TemplatePublishStatus.Published)]
        [InlineData(TemplatePublishStatus.Published, TemplatePublishStatus.Draft)]
        [InlineData(TemplatePublishStatus.Published, TemplatePublishStatus.TestSent)]
        [BusinessRule("BR-NTF-001")]
        public void Illegal_moves_are_rejected(TemplatePublishStatus from, TemplatePublishStatus to)
        {
            Assert.False(TemplatePublishTransitions.CanTransition(from, to));
        }
    }
}
