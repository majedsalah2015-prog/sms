namespace Sms.Domain.Examinations
{
    /// <summary>BR-EXM §4: mirrors Timetable's WF-12 shape — Draft (schedule building) -> Validated (zero clash/capacity violations) -> Published (P2 VP).</summary>
    public enum ExamRoundStatus : short
    {
        Draft = 1,
        Validated = 2,
        Published = 3,
    }
}
