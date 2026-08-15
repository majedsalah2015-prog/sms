using Sms.Domain.Schools;

namespace Sms.Application.Schools
{
    /// <summary>
    /// Pure BR-AYR-002/004/005/006 transition rules: Preparation → Active →
    /// Closing → Closed → Archived, one direction only. Activation's
    /// opening-checklist gate (BR-AYR-004) and closing's checklist gate
    /// (BR-AYR-005) aren't enforced here — the checklist mechanism doesn't
    /// exist yet (needs Calendar/Grades/Fees modules, E-103/S3).
    /// </summary>
    public static class AcademicYearStatusTransitions
    {
        public static bool CanTransition(AcademicYearStatus from, AcademicYearStatus to)
        {
            return (from, to) switch
            {
                (AcademicYearStatus.Preparation, AcademicYearStatus.Active) => true,
                (AcademicYearStatus.Active, AcademicYearStatus.Closing) => true,
                (AcademicYearStatus.Closing, AcademicYearStatus.Closed) => true,
                (AcademicYearStatus.Closed, AcademicYearStatus.Archived) => true,
                _ => false,
            };
        }
    }
}
