using Sms.Domain.Admissions;

namespace Sms.Application.Admissions
{
    /// <summary>
    /// Pure BR-ADM-005 WF-01 transition rules. Same split as every other
    /// lifecycle in this codebase (SchoolStatusTransitions,
    /// AcademicYearStatusTransitions, StudentStatusTransitions): which
    /// status pairs are legal moves — the approval-authority checks
    /// (Admissions Officer recommends, Registrar approves, Principal
    /// approves exceptions) aren't enforced here.
    /// </summary>
    public static class ApplicationStatusTransitions
    {
        public static bool CanTransition(ApplicationStatus from, ApplicationStatus to)
        {
            return (from, to) switch
            {
                (ApplicationStatus.Draft, ApplicationStatus.Submitted) => true,
                (ApplicationStatus.Submitted, ApplicationStatus.UnderReview) => true,
                (ApplicationStatus.UnderReview, ApplicationStatus.Recommended) => true,
                (ApplicationStatus.UnderReview, ApplicationStatus.Rejected) => true,
                (ApplicationStatus.Recommended, ApplicationStatus.Approved) => true,
                (ApplicationStatus.Recommended, ApplicationStatus.Rejected) => true,
                (ApplicationStatus.Recommended, ApplicationStatus.Waitlisted) => true,
                (ApplicationStatus.Waitlisted, ApplicationStatus.Approved) => true, // offer accepted
                (ApplicationStatus.Waitlisted, ApplicationStatus.Rejected) => true,
                (ApplicationStatus.Waitlisted, ApplicationStatus.Lapsed) => true, // offer expired, no more seats
                (ApplicationStatus.Approved, ApplicationStatus.Registered) => true,
                (ApplicationStatus.Approved, ApplicationStatus.Lapsed) => true, // registration deadline missed
                _ => false,
            };
        }
    }
}
