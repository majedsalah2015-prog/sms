using System;
using Sms.Domain.Schools;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-SCH-005: the requested status pair isn't a legal move (e.g. Closed is terminal).</summary>
    public class InvalidSchoolStatusTransitionException : InvalidOperationException
    {
        public InvalidSchoolStatusTransitionException(SchoolStatus from, SchoolStatus to)
            : base($"School status cannot move from '{from}' to '{to}' (BR-SCH-005).")
        {
        }
    }

    /// <summary>BR-AYR-002: the requested status pair isn't a legal move (e.g. Preparation → Closed skips Active).</summary>
    public class InvalidAcademicYearStatusTransitionException : InvalidOperationException
    {
        public InvalidAcademicYearStatusTransitionException(AcademicYearStatus from, AcademicYearStatus to)
            : base($"Academic year status cannot move from '{from}' to '{to}' (BR-AYR-002).")
        {
        }
    }

    /// <summary>BR-AYR-001: date span (6–14 months) or overlap-with-another-year violation.</summary>
    public class InvalidAcademicYearDatesException : InvalidOperationException
    {
        public InvalidAcademicYearDatesException(string reason)
            : base($"Academic year dates are invalid: {reason} (BR-AYR-001).")
        {
        }
    }

    /// <summary>BR-AYR-002: at most one Preparation year per school.</summary>
    public class DuplicatePreparationYearException : InvalidOperationException
    {
        public DuplicatePreparationYearException()
            : base("A Preparation-status academic year already exists for this school (BR-AYR-002).")
        {
        }
    }
}
