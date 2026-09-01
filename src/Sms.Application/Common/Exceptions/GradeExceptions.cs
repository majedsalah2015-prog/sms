using System;
using Sms.Application.Common.Guards;
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

    /// <summary>
    /// BR-GRD-007: a stage / grade level / grade-year profile can only be edited or
    /// deactivated while nothing downstream (grades, profiles, sections, enrollments,
    /// promotion paths) depends on it.
    /// </summary>
    /// <remarks>
    /// What blocks the change is carried as a <see cref="UsageReport"/> rather than as an English
    /// clause. The report is bilingual by construction — the same shape <c>RecordInUseException</c>
    /// uses — so the screen can name what is in the way in the reader's own language instead of
    /// telling an Arabic-speaking registrar, in English, that a stage "still has 3 active grade
    /// level(s)".
    /// </remarks>
    public class GradeStructureInUseException : InvalidOperationException
    {
        public GradeStructureInUseException(UsageReport usage)
            : base($"Cannot change grade structure: {usage.Describe(arabic: false)} (BR-GRD-007).")
        {
            Usage = usage;
        }

        /// <summary>Everything that still depends on the row, so the refusal can list what to clear first.</summary>
        public UsageReport Usage { get; }
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
