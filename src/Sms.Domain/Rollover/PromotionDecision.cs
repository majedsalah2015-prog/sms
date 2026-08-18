namespace Sms.Domain.Rollover
{
    /// <summary>
    /// doc/Modules/03 §4 step 3 — the per-student promotion decision the
    /// rollover consumes. Grading (Module 17) proposes promote/retain/
    /// conditional via <c>PromotionOutcome</c>; this enum adds the rollover-
    /// only states: Undecided (no year result yet / not reviewed) and
    /// Graduate (graduating-grade exit, BR-AYR §4 "Graduating-grade students
    /// exit to Graduate status").
    /// </summary>
    public enum PromotionDecision : short
    {
        Undecided = 1,
        Promote = 2,
        Conditional = 3,
        Retain = 4,
        Graduate = 5,
    }
}
