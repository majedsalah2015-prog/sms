using Sms.Domain.Fees;

namespace Sms.Application.Fees
{
    /// <summary>
    /// BR-FEE-002's lifecycle, and the whole of it: a line is drafted, approved, and
    /// — where the school stops charging that category to that grade — withdrawn.
    /// <para>
    /// There is no way back to Draft on purpose. Re-editing an approved amount is
    /// exactly what BR-FEE-002 forbids, and a route through Draft would be that edit
    /// with an extra click in front of it. A price that was wrong is withdrawn and
    /// replaced, which leaves both the old figure and the decision to stop using it
    /// on the record.
    /// </para>
    /// </summary>
    public static class FeeStructureLineStatusTransitions
    {
        public static bool CanTransition(FeeStructureLineStatus from, FeeStructureLineStatus to) => (from, to) switch
        {
            (FeeStructureLineStatus.Draft, FeeStructureLineStatus.Approved) => true,
            (FeeStructureLineStatus.Approved, FeeStructureLineStatus.Withdrawn) => true,
            _ => false,
        };
    }
}
