namespace Sms.Domain.Grading
{
    /// <summary>BR-GRA-005 WF-07 spine (approval-authority scope checks not enforced here, same precedent as every other epic's status-only workflow substitution).</summary>
    public enum MarksheetStatus : short
    {
        Draft = 1,
        Submitted = 2,
        HoDReviewed = 3,
        Approved = 4,
        Published = 5,
    }
}
