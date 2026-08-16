using System;
using Sms.Domain.Students;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-STU-003/BR-PAR-005: removing the last financially-responsible link on an active student is blocked.</summary>
    public class LastFinanciallyResponsibleGuardianException : InvalidOperationException
    {
        public LastFinanciallyResponsibleGuardianException(int studentId)
            : base($"Student {studentId} must keep at least one financially-responsible guardian link (BR-STU-003).")
        {
        }
    }

    /// <summary>BR-STU-002: the requested status pair isn't a legal move.</summary>
    public class InvalidStudentStatusTransitionException : InvalidOperationException
    {
        public InvalidStudentStatusTransitionException(StudentStatus from, StudentStatus to)
            : base($"Student status cannot move from '{from}' to '{to}' (BR-STU-002).")
        {
        }
    }

    /// <summary>BR-GLB-024: a student has at most one active enrollment per academic year.</summary>
    public class DuplicateEnrollmentException : InvalidOperationException
    {
        public DuplicateEnrollmentException(int studentId, int academicYearId)
            : base($"Student {studentId} already has an active enrollment for academic year {academicYearId} (BR-GLB-024).")
        {
        }
    }
}
