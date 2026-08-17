using Sms.Domain.Activities;

namespace Sms.Application.Activities
{
    /// <summary>Pure BR-ACT-002/005 spine.</summary>
    public static class ProgramEnrollmentStatusTransitions
    {
        public static bool CanTransition(ProgramEnrollmentStatus from, ProgramEnrollmentStatus to)
        {
            return (from, to) switch
            {
                (ProgramEnrollmentStatus.Requested, ProgramEnrollmentStatus.Waitlisted) => true,
                (ProgramEnrollmentStatus.Requested, ProgramEnrollmentStatus.ConsentPending) => true,
                (ProgramEnrollmentStatus.Requested, ProgramEnrollmentStatus.Active) => true,
                (ProgramEnrollmentStatus.Waitlisted, ProgramEnrollmentStatus.ConsentPending) => true,
                (ProgramEnrollmentStatus.Waitlisted, ProgramEnrollmentStatus.Active) => true,
                (ProgramEnrollmentStatus.ConsentPending, ProgramEnrollmentStatus.Active) => true,
                (ProgramEnrollmentStatus.ConsentPending, ProgramEnrollmentStatus.Waitlisted) => true,
                (ProgramEnrollmentStatus.Requested, ProgramEnrollmentStatus.Withdrawn) => true,
                (ProgramEnrollmentStatus.Waitlisted, ProgramEnrollmentStatus.Withdrawn) => true,
                (ProgramEnrollmentStatus.ConsentPending, ProgramEnrollmentStatus.Withdrawn) => true,
                (ProgramEnrollmentStatus.Active, ProgramEnrollmentStatus.Withdrawn) => true,
                _ => false,
            };
        }
    }
}
