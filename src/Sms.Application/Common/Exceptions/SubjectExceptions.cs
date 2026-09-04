using System;
using Sms.Application.Common.Guards;

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
    /// <remarks>
    /// The blocking references travel as a bilingual <see cref="UsageReport"/>, not as an English
    /// clause — see <see cref="GradeStructureInUseException"/> for why.
    /// </remarks>
    public class SubjectInUseException : InvalidOperationException
    {
        public SubjectInUseException(UsageReport usage)
            : base($"Cannot remove: {usage.Describe(arabic: false)}.")
        {
            Usage = usage;
        }

        /// <summary>Everything that still references the subject or department.</summary>
        public UsageReport Usage { get; }
    }

    /// <summary>BR-SUB §9: offering uniqueness is (grade-year profile, subject).</summary>
    public class DuplicateOfferingException : InvalidOperationException
    {
        public DuplicateOfferingException(int gradeYearProfileId, int subjectId)
            : base($"An offering for subject {subjectId} already exists on grade-year profile {gradeYearProfileId}.")
        {
        }
    }

    /// <summary>
    /// BR-SUB-004: a plan line that has already been end-dated is history, and history is not
    /// rewritten. Reopening the numbers behind a term that has been taught would silently restate
    /// marks and timetables already issued against them; the way to say something different from
    /// now on is a new offering, which is what end-dating leaves room for.
    /// </summary>
    public class EndedOfferingNotEditableException : InvalidOperationException
    {
        public EndedOfferingNotEditableException(int offeringId)
            : base($"Offering {offeringId} has been end-dated and can no longer be edited (BR-SUB-004).")
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

    /// <summary>
    /// BR-SUB §9: "periods/week ≥ 1 for scheduled offerings". A plan line worth zero periods is not
    /// a lighter subject, it is a subject the timetable can never place — and it reads as a real
    /// line on the grade's plan while being unteachable.
    /// </summary>
    public class InvalidOfferingPeriodsException : InvalidOperationException
    {
        public InvalidOfferingPeriodsException(int weeklyPeriods)
            : base($"An offering needs at least one period a week; {weeklyPeriods} was given (BR-SUB §9).")
        {
        }
    }
}
