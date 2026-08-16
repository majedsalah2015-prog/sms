using Sms.Application.Portal;
using Sms.TestSupport;
using Xunit;
using GuardianLink = Sms.Application.Portal.GuardianVisibilityEvaluator.GuardianLink;

namespace Sms.Application.Tests.Portal
{
    public class GuardianVisibilityEvaluatorTests
    {
        [Fact]
        [BusinessRule("BR-SEC-011")]
        public void Active_portal_visible_links_are_included()
        {
            var links = new[] { new GuardianLink(1, isPortalVisible: true, effectiveToUtc: null) };

            Assert.Equal(new[] { 1 }, GuardianVisibilityEvaluator.GetVisibleStudentIds(links));
        }

        [Fact]
        [BusinessRule("BR-SEC-011")]
        public void Portal_hidden_links_are_excluded()
        {
            var links = new[] { new GuardianLink(1, isPortalVisible: false, effectiveToUtc: null) };

            Assert.Empty(GuardianVisibilityEvaluator.GetVisibleStudentIds(links));
        }

        [Fact]
        [BusinessRule("BR-SEC-011")]
        public void Unlinked_custody_revoked_links_are_excluded()
        {
            var links = new[] { new GuardianLink(1, isPortalVisible: true, effectiveToUtc: new System.DateTime(2027, 1, 1)) };

            Assert.Empty(GuardianVisibilityEvaluator.GetVisibleStudentIds(links));
        }

        [Fact]
        [BusinessRule("BR-PAR-004")]
        public void Multiple_children_all_resolve()
        {
            var links = new[]
            {
                new GuardianLink(1, true, null),
                new GuardianLink(2, true, null),
                new GuardianLink(3, false, null),
            };

            Assert.Equal(new[] { 1, 2 }, GuardianVisibilityEvaluator.GetVisibleStudentIds(links));
        }
    }
}
