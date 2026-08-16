namespace Sms.Domain.Payments
{
    /// <summary>BR-PAY-002: voiding keeps the number Void with a reason (strict series continuity, doc 08) — never deleted.</summary>
    public enum ReceiptStatus : short
    {
        Posted = 1,
        Void = 2,
    }
}
