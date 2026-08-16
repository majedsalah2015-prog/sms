using System;
using Sms.Domain.Admissions;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-ADM-002: duplicate live applications for the same applicant are blocked.</summary>
    public class DuplicateLiveApplicationException : InvalidOperationException
    {
        public DuplicateLiveApplicationException(int existingApplicationId)
            : base($"A live application already exists for this applicant (id {existingApplicationId}) (BR-ADM-002).")
        {
            ExistingApplicationId = existingApplicationId;
        }

        public int ExistingApplicationId { get; }
    }

    /// <summary>BR-GRD-005/BR-ADM-004: the applicant's age falls outside the grade's configured range.</summary>
    public class AgeIneligibleException : InvalidOperationException
    {
        public AgeIneligibleException()
            : base("Applicant does not meet the grade's age eligibility rule (BR-GRD-005).")
        {
        }
    }

    /// <summary>BR-ADM-005: the requested status pair isn't a legal WF-01 move.</summary>
    public class InvalidApplicationStatusTransitionException : InvalidOperationException
    {
        public InvalidApplicationStatusTransitionException(ApplicationStatus from, ApplicationStatus to)
            : base($"Application status cannot move from '{from}' to '{to}' (BR-ADM-005).")
        {
        }
    }

    /// <summary>BR-ADM-007: registration requires an Approved application with a linked parent.</summary>
    public class ApplicationNotReadyForRegistrationException : InvalidOperationException
    {
        public ApplicationNotReadyForRegistrationException(string reason)
            : base($"Application is not ready for registration: {reason} (BR-ADM-007).")
        {
        }
    }
}
