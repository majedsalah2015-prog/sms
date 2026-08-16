using Sms.Domain.Fees;

namespace Sms.Application.Fees
{
    /// <summary>Pure BR-FEE-002: Draft -> Approved. Locking at year activation is deferred cross-module integration (see FeeStructureLineStatus doc comment).</summary>
    public static class FeeStructureLineStatusTransitions
    {
        public static bool CanTransition(FeeStructureLineStatus from, FeeStructureLineStatus to)
            => from == FeeStructureLineStatus.Draft && to == FeeStructureLineStatus.Approved;
    }
}
