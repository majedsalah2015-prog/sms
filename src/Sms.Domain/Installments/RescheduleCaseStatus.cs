namespace Sms.Domain.Installments
{
    /// <summary>BR-INS-005: Proposed -> Approved | Rejected.</summary>
    public enum RescheduleCaseStatus : short
    {
        Proposed = 1,
        Approved = 2,
        Rejected = 3,
    }
}
