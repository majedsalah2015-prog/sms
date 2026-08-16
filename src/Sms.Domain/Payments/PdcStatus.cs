namespace Sms.Domain.Payments
{
    /// <summary>BR-PAY-004: Lodged -> Due -> Deposited -> {Cleared, Bounced} -> {Replaced, Settled}.</summary>
    public enum PdcStatus : short
    {
        Lodged = 1,
        Due = 2,
        Deposited = 3,
        Cleared = 4,
        Bounced = 5,
        Replaced = 6,
        Settled = 7,
    }
}
