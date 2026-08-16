using Sms.Domain.Payments;

namespace Sms.Application.Payments
{
    /// <summary>Pure BR-PAY-004: Lodged -> Due -> Deposited -> {Cleared, Bounced} -> {Replaced, Settled}.</summary>
    public static class PdcStatusTransitions
    {
        public static bool CanTransition(PdcStatus from, PdcStatus to)
        {
            return (from, to) switch
            {
                (PdcStatus.Lodged, PdcStatus.Due) => true,
                (PdcStatus.Due, PdcStatus.Deposited) => true,
                (PdcStatus.Deposited, PdcStatus.Cleared) => true,
                (PdcStatus.Deposited, PdcStatus.Bounced) => true,
                (PdcStatus.Bounced, PdcStatus.Replaced) => true,
                (PdcStatus.Bounced, PdcStatus.Settled) => true,
                _ => false,
            };
        }
    }
}
