using System;
using Sms.Domain.Grades;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-GRD-009: grade codes are unique per school.</summary>
    public class DuplicateGradeCodeException : InvalidOperationException
    {
        public DuplicateGradeCodeException(string code)
            : base($"A grade level with code '{code}' already exists for this school (BR-GRD-009).")
        {
        }
    }

    /// <summary>BR-GRD-004: a grade/section may narrow its stage's gender policy, never widen it.</summary>
    public class InvalidGenderPolicyNarrowingException : InvalidOperationException
    {
        public InvalidGenderPolicyNarrowingException(GenderPolicy stagePolicy, GenderPolicy requestedPolicy)
            : base($"'{requestedPolicy}' does not narrow the stage's '{stagePolicy}' policy (BR-GRD-004).")
        {
        }
    }
}

namespace Sms.Application.Common.Exceptions
{
    /// <summary>doc/Modules/05 §9: the promotion path must be acyclic.</summary>
    public class PromotionPathCycleException : System.InvalidOperationException
    {
        public PromotionPathCycleException()
            : base("Promotion path would form a cycle (doc/Modules/05 §9).")
        {
        }
    }
}
