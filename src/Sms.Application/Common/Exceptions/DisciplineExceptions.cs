using System;
using Sms.Domain.Discipline;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-DCP-003: the requested case status pair isn't a legal WF-11 move.</summary>
    public class InvalidCaseStatusTransitionException : InvalidOperationException
    {
        public InvalidCaseStatusTransitionException(CaseStatus from, CaseStatus to)
            : base($"Discipline case cannot move from '{from}' to '{to}' (BR-DCP-003).")
        {
        }
    }

    /// <summary>BR-DCP-002 / doc §9: merit points within type bounds.</summary>
    public class MeritPointsOutOfBoundsException : InvalidOperationException
    {
        public MeritPointsOutOfBoundsException(int meritTypeId, int points)
            : base($"Merit type {meritTypeId} does not allow {points} points (BR-DCP-002).")
        {
        }
    }

    /// <summary>BR-DCP-003: decisions cite the code article — no free-form punishments.</summary>
    public class DecisionArticleRequiredException : InvalidOperationException
    {
        public DecisionArticleRequiredException(int caseId)
            : base($"Case {caseId}'s decision must cite a behavior-code article (BR-DCP-003).")
        {
        }
    }

    /// <summary>BR-DCP-003 due process: statements mandatory for severity ≥ 3.</summary>
    public class StatementsRequiredException : InvalidOperationException
    {
        public StatementsRequiredException(int caseId)
            : base($"Case {caseId} needs a student or parent statement before a decision (BR-DCP-003).")
        {
        }
    }

    /// <summary>BR-DCP-005: deviating below the ladder proposal needs a reason.</summary>
    public class DecisionDeviationReasonRequiredException : InvalidOperationException
    {
        public DecisionDeviationReasonRequiredException(int caseId)
            : base($"Case {caseId}'s decision is below the ladder proposal — a reason is required (BR-DCP-005).")
        {
        }
    }

    /// <summary>BR-DCP-004/005: above-code, suspension-class or severity-4 decisions require the Principal.</summary>
    public class PrincipalApprovalRequiredException : InvalidOperationException
    {
        public PrincipalApprovalRequiredException(int caseId)
            : base($"Case {caseId}'s decision requires the Principal (BR-DCP-004/005).")
        {
        }
    }

    /// <summary>BR-DCP-004: suspension days exceed the pack legal cap.</summary>
    public class SuspensionExceedsPackLimitException : InvalidOperationException
    {
        public SuspensionExceedsPackLimitException(int days, int max)
            : base($"Suspension of {days} days exceeds the country-pack limit of {max} (BR-DCP-004).")
        {
        }
    }

    /// <summary>BR-DCP-006: appeal outside the window, below severity 2, or already filed.</summary>
    public class AppealNotAllowedException : InvalidOperationException
    {
        public AppealNotAllowedException(int caseId)
            : base($"An appeal is not allowed for case {caseId} (BR-DCP-006).")
        {
        }
    }

    /// <summary>BR-DCP-006 / BR-WF-003: the reviewer must not be the original decider.</summary>
    public class AppealReviewerNotIndependentException : InvalidOperationException
    {
        public AppealReviewerNotIndependentException(int appealId)
            : base($"Appeal {appealId} must be reviewed by someone other than the original decider (BR-DCP-006).")
        {
        }
    }

    /// <summary>A case closes only after the appeal window elapsed or the appeal was decided.</summary>
    public class CaseNotClosableException : InvalidOperationException
    {
        public CaseNotClosableException(int caseId)
            : base($"Case {caseId} cannot close yet — appeal window open or appeal pending (BR-DCP-006).")
        {
        }
    }
}
