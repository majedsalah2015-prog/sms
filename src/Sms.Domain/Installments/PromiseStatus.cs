namespace Sms.Domain.Installments
{
    /// <summary>BR-INS-006: Open until the promised date passes — Kept if paid by then, Broken otherwise.</summary>
    public enum PromiseStatus : short
    {
        Open = 1,
        Kept = 2,
        Broken = 3,
    }
}
