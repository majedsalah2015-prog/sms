using Sms.Application.Attachments;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Attachments
{
    public class AttachmentAccessEvaluatorTests
    {
        [Fact]
        [BusinessRule("BR-ATT-004")]
        public void No_visibility_on_the_owning_entity_means_no_visibility_on_its_attachments()
        {
            Assert.False(AttachmentAccessEvaluator.CanView(seesOwningEntity: false, documentTypeIsRestricted: false, hasRestrictedCategoryAccess: true));
        }

        [Fact]
        [BusinessRule("BR-ATT-004")]
        public void An_unrestricted_type_is_visible_once_the_owning_entity_is()
        {
            Assert.True(AttachmentAccessEvaluator.CanView(seesOwningEntity: true, documentTypeIsRestricted: false, hasRestrictedCategoryAccess: false));
        }

        [Fact]
        [BusinessRule("BR-ATT-004")]
        public void A_restricted_type_needs_the_restricted_category_permission_even_when_the_entity_is_visible()
        {
            Assert.False(AttachmentAccessEvaluator.CanView(seesOwningEntity: true, documentTypeIsRestricted: true, hasRestrictedCategoryAccess: false));
            Assert.True(AttachmentAccessEvaluator.CanView(seesOwningEntity: true, documentTypeIsRestricted: true, hasRestrictedCategoryAccess: true));
        }
    }
}
