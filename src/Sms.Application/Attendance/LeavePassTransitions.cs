using Sms.Domain.Attendance;

namespace Sms.Application.Attendance
{
    /// <summary>Pure BR-ATD-006 WF: Requested -> Approved (P2, not enforced here) -> Released -> Returned; Requested/Approved -> Rejected.</summary>
    public static class LeavePassTransitions
    {
        public static bool CanTransition(LeavePassStatus from, LeavePassStatus to)
        {
            return (from, to) switch
            {
                (LeavePassStatus.Requested, LeavePassStatus.Approved) => true,
                (LeavePassStatus.Requested, LeavePassStatus.Rejected) => true,
                (LeavePassStatus.Approved, LeavePassStatus.Released) => true,
                (LeavePassStatus.Approved, LeavePassStatus.Rejected) => true,
                (LeavePassStatus.Released, LeavePassStatus.Returned) => true,
                _ => false,
            };
        }
    }
}
