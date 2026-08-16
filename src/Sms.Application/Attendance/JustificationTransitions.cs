using Sms.Domain.Attendance;

namespace Sms.Application.Attendance
{
    /// <summary>Pure BR-ATD-005: a justification is reviewed exactly once.</summary>
    public static class JustificationTransitions
    {
        public static bool CanTransition(JustificationReviewState from, JustificationReviewState to)
        {
            return (from, to) switch
            {
                (JustificationReviewState.Submitted, JustificationReviewState.Accepted) => true,
                (JustificationReviewState.Submitted, JustificationReviewState.Rejected) => true,
                _ => false,
            };
        }
    }
}
