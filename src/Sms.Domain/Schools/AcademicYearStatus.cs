namespace Sms.Domain.Schools
{
    /// <summary>
    /// BR-AYR-002. Numbering matches DB doc A1's pivotal spec EXACTLY
    /// (0=Preparation) — deliberately NOT starting at 1 like this
    /// codebase's other enums, because the doc gives this one an explicit
    /// column-level numbering to honor.
    /// </summary>
    public enum AcademicYearStatus : short
    {
        Preparation = 0,
        Active = 1,
        Closing = 2,
        Closed = 3,
        Archived = 4,
    }
}
