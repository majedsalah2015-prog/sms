namespace Sms.Domain.Installments
{
    /// <summary>BR-INS-001: templates are approved with the fee structure (P3) before assignment.</summary>
    public enum PlanTemplateStatus : short
    {
        Draft = 1,
        Approved = 2,
    }
}
