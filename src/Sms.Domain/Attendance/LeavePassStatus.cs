namespace Sms.Domain.Attendance
{
    /// <summary>BR-ATD-006 WF: Requested -> Approved (P2) -> Released -> Returned; Requested/Approved -> Rejected.</summary>
    public enum LeavePassStatus : short
    {
        Requested = 1,
        Approved = 2,
        Released = 3,
        Returned = 4,
        Rejected = 5,
    }
}
