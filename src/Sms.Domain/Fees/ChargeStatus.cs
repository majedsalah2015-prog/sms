namespace Sms.Domain.Fees
{
    /// <summary>BR-FEE-003/BR-GLB-062: posted charges are immutable — corrections are CreditNotes, not edits or re-transitions. Void exists only for pre-allocation error correction.</summary>
    public enum ChargeStatus : short
    {
        Posted = 1,
        Void = 2,
    }
}
