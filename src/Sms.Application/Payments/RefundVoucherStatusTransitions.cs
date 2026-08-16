using Sms.Domain.Payments;

namespace Sms.Application.Payments
{
    /// <summary>Pure BR-PAY-005 WF-05: Requested -> Approved -> Paid; Requested/Approved -> Rejected.</summary>
    public static class RefundVoucherStatusTransitions
    {
        public static bool CanTransition(RefundVoucherStatus from, RefundVoucherStatus to)
        {
            return (from, to) switch
            {
                (RefundVoucherStatus.Requested, RefundVoucherStatus.Approved) => true,
                (RefundVoucherStatus.Requested, RefundVoucherStatus.Rejected) => true,
                (RefundVoucherStatus.Approved, RefundVoucherStatus.Paid) => true,
                (RefundVoucherStatus.Approved, RefundVoucherStatus.Rejected) => true,
                _ => false,
            };
        }
    }
}
