namespace Sms.Domain.Payments
{
    /// <summary>
    /// S6/E-605: a receipt is either fee money (allocates to charges, surplus =
    /// advance) or a cafeteria wallet top-up (payer money held as a liability
    /// per BR-CAF-001/007 — never allocated, never part of the fee advance).
    /// Every fee-side reader (allocation, refundable position, statement,
    /// GL) filters on FeePayment; the wallet ledger owns WalletTopUp receipts.
    /// </summary>
    public enum ReceiptPurpose : short
    {
        FeePayment = 1,
        WalletTopUp = 2,
    }
}
