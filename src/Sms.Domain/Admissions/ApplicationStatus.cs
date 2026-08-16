namespace Sms.Domain.Admissions
{
    /// <summary>WF-01 per doc/Modules/09 §4/§5 (BR-ADM-005): status changes only via the admission pipeline.</summary>
    public enum ApplicationStatus : short
    {
        Draft = 1,
        Submitted = 2,
        UnderReview = 3,
        Recommended = 4,
        Approved = 5,
        Rejected = 6,
        Waitlisted = 7,
        Registered = 8,

        /// <summary>BR-ADM-007: an approved application not registered by the deadline lapses (seat released).</summary>
        Lapsed = 9,
    }
}
