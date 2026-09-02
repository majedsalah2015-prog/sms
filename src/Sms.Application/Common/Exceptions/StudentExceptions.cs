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

    /// <summary>
    /// BR-GLB-023: correcting an enrollment may change the grade, never the academic year.
    /// <para>
    /// The enrollment is the year pivot every year-scoped row hangs off, so re-pointing one at
    /// another year would re-file this year's attendance, marks and charges under a year they did
    /// not happen in. Moving a child between years is the rollover, which writes a new enrollment
    /// and leaves the old one closed behind it.
    /// </para>
    /// </summary>
    public class EnrollmentYearChangeException : InvalidOperationException
    {
        public EnrollmentYearChangeException(int enrollmentId, int fromAcademicYearId, int toAcademicYearId)
            : base($"Enrollment {enrollmentId} is in academic year {fromAcademicYearId} and cannot be corrected into year {toAcademicYearId} — that is a rollover, not a correction (BR-GLB-023).")
        {
            FromAcademicYearId = fromAcademicYearId;
            ToAcademicYearId = toAcademicYearId;
        }

        public int FromAcademicYearId { get; }

        public int ToAcademicYearId { get; }
    }

    /// <summary>
    /// The grade cannot be corrected under a child who is sitting in a section, because the section
    /// belongs to the grade being corrected (BR-SCN-002/003 are properties of that pairing).
    /// <para>
    /// Carries the section's names so the refusal can say which seat to give up rather than only
    /// that there is one.
    /// </para>
    /// </summary>
    public class EnrollmentSeatedException : InvalidOperationException
    {
        public EnrollmentSeatedException(int enrollmentId, string sectionNameEn, string sectionNameAr)
            : base($"Enrollment {enrollmentId} is seated in section '{sectionNameEn}'; a section belongs to one grade-year, so the seat must be given up before the grade is corrected.")
        {
            SectionNameEn = sectionNameEn;
            SectionNameAr = sectionNameAr;
        }

        public string SectionNameEn { get; }

        public string SectionNameAr { get; }
    }
}
