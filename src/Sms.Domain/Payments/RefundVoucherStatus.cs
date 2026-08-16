namespace Sms.Domain.Payments
{
    /// <summary>BR-PAY-005 WF-05: Requested -> Approved -> Paid; Requested/Approved -> Rejected.</summary>
    public enum RefundVoucherStatus : short
    {
        Requested = 1,
        Approved = 2,
        Paid = 3,
        Rejected = 4,
    }
}
