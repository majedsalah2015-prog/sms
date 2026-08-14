namespace Sms.Domain.Common
{
    /// <summary>
    /// Opt-in marker (docs/Database/01 §5: "soft-active filter opt-in per entity"):
    /// entities implementing this are hidden from queries once deactivated,
    /// unless the query calls IgnoreQueryFilters().
    /// </summary>
    public interface ISoftActiveFiltered : IActivatable
    {
    }
}
