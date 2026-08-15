namespace Sms.Domain.Calendar
{
    /// <summary>Whether a CalendarDay row is a materialized weekend-rule default or an explicit manual override.</summary>
    public enum CalendarDaySource : short
    {
        Rule = 1,
        Manual = 2,
    }
}
