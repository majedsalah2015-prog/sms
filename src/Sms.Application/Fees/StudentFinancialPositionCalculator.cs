namespace Sms.Application.Fees
{
    /// <summary>Pure BR-FEE-008 (+ BR-DIS-005/010): a student's financial position = posted charges - credit notes - discount documents - allocated payments. Discounts are a separate term, never folded into charges (Module 22).</summary>
    public static class StudentFinancialPositionCalculator
    {
        public static decimal Calculate(decimal totalPostedCharges, decimal totalCreditNotes, decimal totalAllocatedPayments)
            => Calculate(totalPostedCharges, totalCreditNotes, 0m, totalAllocatedPayments);

        public static decimal Calculate(decimal totalPostedCharges, decimal totalCreditNotes, decimal totalDiscounts, decimal totalAllocatedPayments)
            => totalPostedCharges - totalCreditNotes - totalDiscounts - totalAllocatedPayments;
    }
}
