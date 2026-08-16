namespace Sms.Application.Payments
{
    /// <summary>Pure BR-PAY-005: a refund never exceeds the payer's refundable position (hard rule).</summary>
    public static class RefundAmountGuard
    {
        public static bool IsWithinRefundablePosition(decimal refundAmount, decimal refundablePosition)
            => refundAmount > 0 && refundAmount <= refundablePosition;
    }
}
