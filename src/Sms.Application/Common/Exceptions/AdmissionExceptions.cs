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

    /// <summary>What an application still lacks before a seat can be registered against it.</summary>
    public enum RegistrationBlocker
    {
        /// <summary>The application has not been approved — a decision comes before a seat.</summary>
        NotApproved = 1,

        /// <summary>No parent is linked, so the seat would have nobody to bill or to contact.</summary>
        NoParentLinked = 2,
    }

    /// <summary>
    /// BR-ADM-007: registration requires an Approved application with a linked parent.
    /// <para>
    /// The blocker is carried as a value and the application's own status alongside it, so the
    /// screen can say which of the two rules stopped it — and say it in the reader's language,
    /// which an English clause built inside the engine could never do.
    /// </para>
    /// </summary>
    public class ApplicationNotReadyForRegistrationException : InvalidOperationException
    {
        public ApplicationNotReadyForRegistrationException(RegistrationBlocker blocker, ApplicationStatus status)
            : base($"Application is not ready for registration: {(blocker == RegistrationBlocker.NotApproved ? $"status is '{status}', not Approved" : "no parent linked to the application")} (BR-ADM-007).")
        {
            Blocker = blocker;
            Status = status;
        }

        public RegistrationBlocker Blocker { get; }

        /// <summary>Where the application actually stands, so the refusal can name it rather than only deny.</summary>
        public ApplicationStatus Status { get; }
    }
}
