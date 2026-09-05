using Sms.Domain.Learning;

namespace Sms.Application.Learning
{
    /// <summary>
    /// Pure BR-LRN-008: "the generated item count and mark total must reconcile to
    /// the <c>BlueprintComponent</c> the paper will feed. Module 17 owns the
    /// weight; this module matches it or refuses to publish the paper."
    ///
    /// <para>
    /// <b>Reconciliation is on the mark total, and the item count is information.</b>
    /// A blueprint component states what it is worth — <c>MaxScore</c> — and says
    /// nothing about how many questions should carry it. Ten questions of two
    /// marks and four of five are both a twenty-mark component, and inventing a
    /// count rule Module 17 never stated would refuse papers for failing a
    /// requirement no document contains.
    /// </para>
    ///
    /// <para>
    /// The same check answers both "may this be sent for approval" and "may this
    /// be approved", because a head of department should never be handed a paper
    /// that cannot be approved. One gate, asked twice, is what keeps the two
    /// answers from drifting.
    /// </para>
    /// </summary>
    public static class PaperReconciliationGate
    {
        /// <summary>BR-LRN-008's arithmetic, alone, so a screen can light a meter without asking about status.</summary>
        public static bool Reconciles(decimal paperTotalMarks, decimal componentMaxScore)
            => paperTotalMarks == componentMaxScore;

        /// <summary>
        /// How far the paper is from the component, signed: positive is over,
        /// negative is short. The meter shows this rather than a bare pass/fail,
        /// because "you are three marks over" is a sentence an author can act on
        /// and "does not reconcile" is not.
        /// </summary>
        public static decimal Variance(decimal paperTotalMarks, decimal componentMaxScore)
            => paperTotalMarks - componentMaxScore;

        /// <summary>
        /// The first refusal that applies, or <see cref="PaperRefusal.None"/>.
        /// Ordered so the author fixes what the paper <em>is</em> — a lifecycle
        /// mistake, then an empty paper, then a withdrawn question on it — before
        /// the arithmetic, which is the only one they can be part-way through
        /// solving.
        /// </summary>
        public static PaperRefusal Check(
            OnlinePaperStatus status,
            OnlinePaperStatus requiredStatus,
            int itemCount,
            decimal paperTotalMarks,
            decimal componentMaxScore,
            int withdrawnQuestionCount)
        {
            if (status != requiredStatus)
            {
                return PaperRefusal.WrongStatus;
            }

            if (itemCount == 0)
            {
                return PaperRefusal.NoItems;
            }

            // A question withdrawn from the bank after it was put on this paper.
            // BR-LRN-007 keeps it on papers already sat and takes it out of future
            // picks — and a paper not yet approved is exactly a future pick, so
            // approving it here would be the one case that rule exists to stop.
            if (withdrawnQuestionCount > 0)
            {
                return PaperRefusal.ContainsWithdrawnQuestion;
            }

            if (!Reconciles(paperTotalMarks, componentMaxScore))
            {
                return PaperRefusal.MarksDoNotReconcile;
            }

            return PaperRefusal.None;
        }
    }

    /// <summary>
    /// Why a paper cannot move. A code rather than a sentence, so the Web boundary
    /// translates it — BR-LRN-008 requires the refusal to be bilingual and to name
    /// both totals, which only the boundary can do.
    /// </summary>
    public enum PaperRefusal
    {
        None = 0,
        WrongStatus = 1,
        NoItems = 2,
        ContainsWithdrawnQuestion = 3,
        MarksDoNotReconcile = 4,
    }
}
