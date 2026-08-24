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

        /// <summary>
        /// Approved, then taken out of the price list — a category the school stopped
        /// charging this grade, or a line approved by mistake. Not a deletion: the row
        /// stays so that a charge already posted from it can still be explained, and
        /// so the same (grade-year, category) pair cannot be quietly re-approved at a
        /// different amount as though the first had never existed.
        /// <para>
        /// BR-FEE-002 makes an approved <em>amount</em> immutable and this does not
        /// touch it. Before this state existed an approved line had no exit at all:
        /// the delete path is draft-only and the only transition was Draft → Approved,
        /// so a line approved against the wrong grade stayed in the price list for
        /// good. <c>PostChargeAsync</c> reads only Approved lines, so withdrawing one
        /// stops it billing without any further wiring.
        /// </para>
        /// </summary>
        Withdrawn = 3,
    }
}
