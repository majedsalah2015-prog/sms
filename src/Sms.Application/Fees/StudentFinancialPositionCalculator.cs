namespace Sms.Application.Fees
{
    /// <summary>Pure BR-FEE-008: a student's financial position = posted charges - credit notes - allocated payments (Module 22 discounts not modeled in this slice).</summary>
    public static class StudentFinancialPositionCalculator
    {
        public static decimal Calculate(decimal totalPostedCharges, decimal totalCreditNotes, decimal totalAllocatedPayments)
            => totalPostedCharges - totalCreditNotes - totalAllocatedPayments;
    }
}
