namespace Sms.Domain.Learning
{
    /// <summary>
    /// doc/Modules/37 §7 lateness policy, shaped by BR-LRN-005.
    ///
    /// <para>
    /// There is deliberately no "refuse late work" member. BR-LRN-005: a late
    /// submission is <em>accepted and flagged, never silently refused</em> — the
    /// policy decides the mark penalty, not the acceptance. Leaving the refusal
    /// out of the enum means no screen can offer it and no later slice can
    /// quietly add it through a dropdown: the rule is enforced by the shape of
    /// the type rather than by a check someone has to remember to write.
    /// </para>
    ///
    /// Starts at 1 per the SMALLINT convention (docs/Database/01).
    /// </summary>
    public enum LatenessPolicy
    {
        /// <summary>Late work is flagged for the teacher but carries no automatic mark penalty.</summary>
        AcceptWithoutPenalty = 1,

        /// <summary>Late work is flagged and a penalty percentage applies — see <c>Homework.LatePenaltyPercent</c>.</summary>
        AcceptWithPenalty = 2,
    }
}
