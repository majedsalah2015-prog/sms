namespace Sms.Domain.Installments
{
    /// <summary>
    /// BR-INS-007: DERIVED, never stored — Installment carries no status
    /// column; InstallmentStatusDeriver computes this from allocations
    /// (Module 21) and dates. Enum lives in Domain so consumers share one
    /// vocabulary.
    /// </summary>
    public enum InstallmentStatus : short
    {
        Scheduled = 1,
        Due = 2,
        Overdue = 3,
        Paid = 4,
        PartiallyPaid = 5,
        Rescheduled = 6,
        WrittenOff = 7,
    }
}
