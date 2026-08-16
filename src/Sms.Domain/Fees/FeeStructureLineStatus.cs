namespace Sms.Domain.Fees
{
    /// <summary>
    /// BR-FEE-002 workflow: Draft (Finance Manager) -> Approved (Principal,
    /// P3 not enforced here). "Locked at year activation" (the doc's third
    /// state) is cross-module integration with AcademicYearAdmin.ActivateAsync
    /// that isn't wired — same deferral as E-103's PromotionPathValidator —
    /// so this enum stops at Approved.
    /// </summary>
    public enum FeeStructureLineStatus : short
    {
        Draft = 1,
        Approved = 2,
    }
}
