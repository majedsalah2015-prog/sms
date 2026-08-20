using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-SUB-001: subject codes are unique per school.</summary>
    public class DuplicateSubjectCodeException : InvalidOperationException
    {
        public DuplicateSubjectCodeException(string code)
            : base($"A subject with code '{code}' already exists for this school (BR-SUB-001).")
        {
        }
    }

    /// <summary>A subject/department can only be removed (deactivated) while nothing current references it.</summary>
    public class SubjectInUseException : InvalidOperationException
    {
        public SubjectInUseException(string reason)
            : base($"Cannot remove: {reason}.")
        {
        }
    }

    /// <summary>BR-SUB §9: offering uniqueness is (grade-year profile, subject).</summary>
    public class DuplicateOfferingException : InvalidOperationException
    {
        public DuplicateOfferingException(int gradeYearProfileId, int subjectId)
            : base($"An offering for subject {subjectId} already exists on grade-year profile {gradeYearProfileId}.")
        {
        }
    }

    /// <summary>BR-SUB §9: an assessable offering must carry a positive GPA weight.</summary>
    public class InvalidOfferingWeightException : InvalidOperationException
    {
        public InvalidOfferingWeightException()
            : base("An assessable offering must carry a GPA weight greater than zero (BR-SUB §9).")
        {
        }
    }
}
