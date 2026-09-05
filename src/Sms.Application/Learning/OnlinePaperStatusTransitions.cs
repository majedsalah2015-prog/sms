using Sms.Domain.Learning;

namespace Sms.Application.Learning
{
    /// <summary>
    /// Pure doc/Modules/37 §4 paper spine: draft -> pending approval -> approved,
    /// with withdrawal reachable from everything that is not already withdrawn.
    ///
    /// <para>
    /// Two edges are deliberately absent. There is no <c>Approved -> Draft</c>:
    /// the item list is what the head of department signed, and reopening it for
    /// editing would leave an approval standing on a document that no longer
    /// exists. A paper that needs to change after approval is withdrawn and
    /// rebuilt, which is visible. And there is nothing out of
    /// <c>Withdrawn</c> — reviving a paper a class was told was cancelled is a
    /// new paper, not a state move (BR-LRN-016).
    /// </para>
    ///
    /// <para>
    /// <c>PendingApproval -> Draft</c> exists and is the rejection path: the head
    /// of department hands it back, and the author edits and resubmits. That is
    /// one edge rather than a Rejected status, because a rejected paper and a
    /// draft are the same thing to everyone who touches them next.
    /// </para>
    /// </summary>
    public static class OnlinePaperStatusTransitions
    {
        public static bool CanTransition(OnlinePaperStatus from, OnlinePaperStatus to)
        {
            return (from, to) switch
            {
                (OnlinePaperStatus.Draft, OnlinePaperStatus.PendingApproval) => true,
                (OnlinePaperStatus.Draft, OnlinePaperStatus.Withdrawn) => true,

                (OnlinePaperStatus.PendingApproval, OnlinePaperStatus.Approved) => true,
                (OnlinePaperStatus.PendingApproval, OnlinePaperStatus.Draft) => true,
                (OnlinePaperStatus.PendingApproval, OnlinePaperStatus.Withdrawn) => true,

                (OnlinePaperStatus.Approved, OnlinePaperStatus.Withdrawn) => true,

                _ => false,
            };
        }

        /// <summary>
        /// Whether items may still be added, removed or reordered. Draft only:
        /// once the paper is with the head of department it is a document under
        /// review, and once approved it is the one they approved.
        /// </summary>
        public static bool IsEditable(OnlinePaperStatus status)
            => status == OnlinePaperStatus.Draft;

        /// <summary>§8.8 schedules an approved paper and nothing else.</summary>
        public static bool CanBeScheduled(OnlinePaperStatus status)
            => status == OnlinePaperStatus.Approved;
    }
}
