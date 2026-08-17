using Sms.Domain.Common;

namespace Sms.Domain.Installments
{
    /// <summary>
    /// doc/Modules/20 §7: "Installment↔Charge is many-to-many via
    /// scheduled-allocation lines (a schedule spans multiple charges)".
    /// The amount of a given charge scheduled into a given installment;
    /// per-installment lines sum to Installment.Amount, per-charge lines
    /// sum to that charge's net scheduled amount.
    /// </summary>
    public class InstallmentChargeLine : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int InstallmentId { get; set; }

        public int ChargeId { get; set; }

        public decimal Amount { get; set; }
    }
}
