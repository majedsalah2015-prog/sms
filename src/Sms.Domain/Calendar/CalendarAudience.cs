namespace Sms.Domain.Calendar
{
    /// <summary>BR-CAL-002/003: a day type or event can target a narrower audience (e.g. staff training day).</summary>
    public enum CalendarAudience : short
    {
        All = 1,
        StudentsOnly = 2,
        StaffOnly = 3,
    }
}
