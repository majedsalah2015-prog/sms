namespace Sms.Domain.Activities
{
    /// <summary>BR-ACT-002/005: Requested -> {Waitlisted (capacity), ConsentPending (BR-ACT-005 hard gate)} -> Active -> Withdrawn.</summary>
    public enum ProgramEnrollmentStatus : short
    {
        Requested = 1,
        Waitlisted = 2,
        ConsentPending = 3,
        Active = 4,
        Withdrawn = 5,
    }
}
