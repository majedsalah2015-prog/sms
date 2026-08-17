namespace Sms.Application.Dashboards
{
    /// <summary>Pure BR-DSH-006: portal dashboards may only surface portal-eligible widgets — no staff widget is portal-reachable.</summary>
    public static class PortalWidgetGate
    {
        public static bool CanRender(bool widgetIsPortalEligible, bool isPortalContext)
            => !isPortalContext || widgetIsPortalEligible;
    }
}
