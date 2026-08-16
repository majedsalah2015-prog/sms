using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-TCH-001: designation/assignment requires an employee with an active contract.</summary>
    public class EmployeeNotEligibleForTeachingException : InvalidOperationException
    {
        public EmployeeNotEligibleForTeachingException(int employeeId)
            : base($"Employee {employeeId} has no active contract and cannot hold a teaching flag/assignment (BR-TCH-001).")
        {
        }
    }

    /// <summary>BR-TCH-005: one primary teacher per offering x section at a time.</summary>
    public class DuplicatePrimaryTeacherException : InvalidOperationException
    {
        public DuplicatePrimaryTeacherException(int curriculumOfferingId, int sectionId)
            : base($"Offering {curriculumOfferingId} / section {sectionId} already has a primary teacher (BR-TCH-005).")
        {
        }
    }

    /// <summary>BR-TCH-004: exceeding max weekly periods requires an explicit, logged override.</summary>
    public class LoadExceededException : InvalidOperationException
    {
        public LoadExceededException(int teacherProfileId, int currentLoad, int maxWeeklyPeriods)
            : base($"Teacher {teacherProfileId} load {currentLoad} would exceed max {maxWeeklyPeriods} (BR-TCH-004) — pass overrideLoad to proceed anyway.")
        {
        }
    }
}
