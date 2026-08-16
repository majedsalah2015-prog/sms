using Sms.Domain.Students;

namespace Sms.Application.Students
{
    /// <summary>
    /// Pure BR-STU-002 transition rules. The doc's owning workflows (WF-03
    /// withdrawal with parallel clearance + finance veto; graduation via
    /// rollover; enrollment via Admissions/M09) are not modeled — only which
    /// status pairs are legal moves at all, same split as
    /// SchoolStatusTransitions (E-102) and AcademicYearStatusTransitions.
    /// </summary>
    public static class StudentStatusTransitions
    {
        public static bool CanTransition(StudentStatus from, StudentStatus to)
        {
            return (from, to) switch
            {
                (StudentStatus.Enrolled, StudentStatus.Suspended) => true,
                (StudentStatus.Suspended, StudentStatus.Enrolled) => true,
                (StudentStatus.Enrolled, StudentStatus.Withdrawn) => true,
                (StudentStatus.Suspended, StudentStatus.Withdrawn) => true,
                (StudentStatus.Enrolled, StudentStatus.Graduated) => true,
                (StudentStatus.Suspended, StudentStatus.Graduated) => true,
                (StudentStatus.Enrolled, StudentStatus.Transferred) => true,
                (StudentStatus.Suspended, StudentStatus.Transferred) => true,
                (StudentStatus.Graduated, StudentStatus.Alumni) => true,
                // BR-STU-007: re-admission reactivates the original record through Admissions.
                (StudentStatus.Withdrawn, StudentStatus.Enrolled) => true,
                _ => false,
            };
        }
    }
}
