namespace Sms.Domain.Fees
{
    public enum PayerType : short
    {
        Parent = 1,

        /// <summary>BR-FEE-004: activation-flagged future path (company/embassy/charity) — no Sponsor entity exists yet, this value is reserved but unused.</summary>
        Sponsor = 2,
    }
}
