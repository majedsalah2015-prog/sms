namespace Sms.Domain.Students
{
    /// <summary>BR-STU-002: status changes only via workflows (enrollment, WF-03 withdrawal, rollover graduation) — not modeled here, just the value.</summary>
    public enum StudentStatus : short
    {
        Enrolled = 1,
        Suspended = 2,
        Withdrawn = 3,
        Graduated = 4,
        Transferred = 5,
        Alumni = 6,
    }
}
