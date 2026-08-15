namespace Sms.Domain.Calendar
{
    /// <summary>BR-CAL-001: every date in an academic year resolves to exactly one of these.</summary>
    public enum DayType : short
    {
        Working = 1,
        Weekend = 2,
        Holiday = 3,

        /// <summary>Short day.</summary>
        Partial = 4,

        ExamPeriodWorking = 5,
    }
}
