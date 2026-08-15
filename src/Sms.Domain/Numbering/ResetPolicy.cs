namespace Sms.Domain.Numbering
{
    /// <summary>doc 08 §3: when the sequence returns to 1.</summary>
    public enum ResetPolicy : short
    {
        Never = 1,
        PerAcademicYear = 2,
        PerCalendarYear = 3,
    }
}
