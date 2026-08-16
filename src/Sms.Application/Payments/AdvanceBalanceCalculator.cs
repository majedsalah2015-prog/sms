namespace Sms.Application.Payments
{
    /// <summary>Pure BR-PAY-003: the unallocated remainder of a payer's receipts — visible on statements, auto-consumed by the next due charge, and the base for BR-PAY-005's refundable-position check.</summary>
    public static class AdvanceBalanceCalculator
    {
        public static decimal Calculate(decimal totalReceiptsForPayer, decimal totalAllocatedForPayer)
            => totalReceiptsForPayer - totalAllocatedForPayer;
    }
}
