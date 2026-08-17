using Sms.Application.Dashboards;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Dashboards
{
    public class PortalWidgetGateTests
    {
        [Fact]
        [BusinessRule("BR-DSH-006")]
        public void Staff_context_shows_everything_permitted_regardless_of_portal_eligibility()
        {
            Assert.True(PortalWidgetGate.CanRender(widgetIsPortalEligible: false, isPortalContext: false));
        }

        [Fact]
        [BusinessRule("BR-DSH-006")]
        public void Portal_context_only_shows_portal_eligible_widgets()
        {
            Assert.True(PortalWidgetGate.CanRender(widgetIsPortalEligible: true, isPortalContext: true));
            Assert.False(PortalWidgetGate.CanRender(widgetIsPortalEligible: false, isPortalContext: true));
        }
    }
}
