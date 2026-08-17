using Sms.Domain.Grading;

namespace Sms.Application.Grading
{
    /// <summary>
    /// Pure BR-GRA-005 WF-07 spine — approval-authority scope checks not
    /// enforced here, same precedent as every other status-only workflow
    /// substitution in this codebase. Published -> Draft is BR-GRA-005's
    /// WF-08 post-publication correction path (P4 Principal, reason
    /// mandatory — the admin service demands the ambient audit reason,
    /// not this table); re-entry and re-publish reuse the same
    /// Draft-through-Published mechanics, so no separate "corrected"
    /// state is modeled.
    /// </summary>
    public static class MarksheetStatusTransitions
    {
        public static bool CanTransition(MarksheetStatus from, MarksheetStatus to)
        {
            return (from, to) switch
            {
                (MarksheetStatus.Draft, MarksheetStatus.Submitted) => true,
                (MarksheetStatus.Submitted, MarksheetStatus.HoDReviewed) => true,
                (MarksheetStatus.HoDReviewed, MarksheetStatus.Approved) => true,
                (MarksheetStatus.Approved, MarksheetStatus.Published) => true,
                (MarksheetStatus.Published, MarksheetStatus.Draft) => true,
                _ => false,
            };
        }
    }
}
