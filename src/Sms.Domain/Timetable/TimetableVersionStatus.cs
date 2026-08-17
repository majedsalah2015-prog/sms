namespace Sms.Domain.Timetable
{
    /// <summary>BR-TTB-002 WF-12: Draft (editing) -> Validated (zero hard-constraint violations) -> Published (P2 VP, sessions generate). Only one Published version is operational at a time (enforced by the admin service, not the shape itself).</summary>
    public enum TimetableVersionStatus : short
    {
        Draft = 1,
        Validated = 2,
        Published = 3,
    }
}
