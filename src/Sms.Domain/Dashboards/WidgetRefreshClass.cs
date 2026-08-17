namespace Sms.Domain.Dashboards
{
    /// <summary>doc/Modules/31 §14 open question #1's proposed default split.</summary>
    public enum WidgetRefreshClass : short
    {
        Live = 1,
        Cached15Min = 2,
        Daily = 3,
    }
}
