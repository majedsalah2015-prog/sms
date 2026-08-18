using Sms.Domain.Grading;
using Sms.Domain.Rollover;

namespace Sms.Application.Rollover
{
    /// <summary>
    /// BR-AYR-008 step 3 / BR-GRA-006: maps Module 17's <see cref="PromotionOutcome"/>
    /// (the criteria engine's proposal) onto the rollover's per-student decision.
    /// Graduating grades exit to Graduate instead of promoting (doc/Modules/03 §4
    /// step 3); a retained student in a graduating grade repeats it. No year
    /// result yet ⇒ Undecided (blocks activation until reviewed, doc §9).
    /// </summary>
    public static class PromotionDecisionMapper
    {
        public static PromotionDecision Propose(PromotionOutcome? outcome, bool isGraduatingGrade)
        {
            if (outcome == null)
            {
                return PromotionDecision.Undecided;
            }

            return outcome.Value switch
            {
                PromotionOutcome.Retain => PromotionDecision.Retain,
                PromotionOutcome.Promote => isGraduatingGrade ? PromotionDecision.Graduate : PromotionDecision.Promote,
                PromotionOutcome.Conditional => isGraduatingGrade ? PromotionDecision.Graduate : PromotionDecision.Conditional,
                _ => PromotionDecision.Undecided,
            };
        }

        /// <summary>
        /// Which grade level the student sits in next year for a given decision:
        /// Promote/Conditional → the grade's promotion target; Retain → the same
        /// grade; Graduate/Undecided → none. Returns null when the decision needs
        /// a target that the grade doesn't define (BR-GRD-002 gap — surfaced by
        /// the caller, not silently ignored).
        /// </summary>
        public static int? ResolveTargetGradeLevelId(PromotionDecision decision, int currentGradeLevelId, int? promotionTargetGradeLevelId)
        {
            return decision switch
            {
                PromotionDecision.Promote => promotionTargetGradeLevelId,
                PromotionDecision.Conditional => promotionTargetGradeLevelId,
                PromotionDecision.Retain => currentGradeLevelId,
                _ => null,
            };
        }

        /// <summary>A decision that requires the student to hold a seat next year.</summary>
        public static bool RequiresTargetSeat(PromotionDecision decision)
            => decision == PromotionDecision.Promote || decision == PromotionDecision.Conditional || decision == PromotionDecision.Retain;
    }
}
