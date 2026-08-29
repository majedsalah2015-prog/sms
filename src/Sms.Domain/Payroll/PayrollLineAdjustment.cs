using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Payroll
{
    /// <summary>
    /// ppl.PayrollLineAdjustment — one hand-entered addition or deduction on one payslip.
    /// <para>
    /// The owner chose contract-driven pay (basic + allowances) over a configurable component
    /// catalogue, so this is where everything a month actually varies by lands: overtime, a bonus,
    /// a fine, an unpaid-leave day, a share of an insurance premium. Each is a row with a
    /// description the school writes, so the payslip explains itself to the person holding it.
    /// </para>
    /// <para>
    /// <see cref="Description"/> is a single free-text column rather than the Ar/En pair this
    /// product uses for system strings. That rule governs text the *product* shows; this is text a
    /// user typed about one payslip, and obliging a payroll clerk to write "خصم تأخير" twice would
    /// only ever produce one filled box and one empty one.
    /// </para>
    /// </summary>
    [Audited(AuditTier.T2)]
    public class PayrollLineAdjustment : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int PayrollRunLineId { get; set; }

        public PayrollAdjustmentKind Kind { get; set; }

        /// <summary>What this is, in the school's own words. Shown verbatim on the payslip.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Always positive — <see cref="Kind"/> carries the direction. See <see cref="PayrollAdjustmentKind"/> for why.</summary>
        public decimal Amount { get; set; }
    }
}
