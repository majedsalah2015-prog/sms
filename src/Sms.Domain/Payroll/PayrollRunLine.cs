using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Payroll
{
    /// <summary>
    /// ppl.PayrollRunLine — what one employee is owed for one month, and the payslip behind it.
    /// <para>
    /// See <see cref="PayrollRun"/> for the standing deviation this whole area represents
    /// (doc/Modules/12 §2 / BR-EMP-007 place the calculation out of scope; the owner asked for it
    /// on 2026-08-28).
    /// </para>
    /// <para>
    /// <b>The pay figures are a snapshot, not a live read of the contract.</b>
    /// <see cref="BasicSalary"/> and <see cref="Allowances"/> are copied from the employee's active
    /// contract when the line is generated and never chase it afterwards. A contract renewed in
    /// March must not silently restate what February's payslip said, and a payslip that changes
    /// after it was handed to the employee is not a payslip. <see cref="ContractId"/> records which
    /// contract the figures came from so the two can be reconciled.
    /// </para>
    /// <para>
    /// T2 rather than T1: every field here is arithmetic derived from things that are themselves
    /// T1-audited (the contract's salary, the adjustments, the advance schedule). Demanding a
    /// written reason for a number the system computed would teach everyone to type a full stop
    /// into the reason box, which is how a mandatory reason stops meaning anything on the fields
    /// that need one.
    /// </para>
    /// </summary>
    [Audited(AuditTier.T2)]
    public class PayrollRunLine : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int PayrollRunId { get; set; }

        public int EmployeeId { get; set; }

        /// <summary>
        /// The contract the pay figures were copied from. Nullable because a school may pay
        /// somebody whose paperwork is not yet on the system — the line is then keyed in by hand,
        /// which is exactly the case a payroll screen exists to make visible rather than to refuse.
        /// </summary>
        public int? ContractId { get; set; }

        /// <summary>الراتب الأساسي — snapshot of <c>Contract.SalaryBasic</c>.</summary>
        public decimal BasicSalary { get; set; }

        /// <summary>البدلات — snapshot of <c>Contract.SalaryAllowances</c>.</summary>
        public decimal Allowances { get; set; }

        /// <summary>Sum of this line's <see cref="PayrollAdjustmentKind.Addition"/> adjustments.</summary>
        public decimal AdditionsTotal { get; set; }

        /// <summary>Sum of this line's <see cref="PayrollAdjustmentKind.Deduction"/> adjustments. Excludes the advance below, which is tracked separately because it answers a different question.</summary>
        public decimal DeductionsTotal { get; set; }

        /// <summary>
        /// خصم السلف — what this month's payroll recovers against outstanding advances.
        /// Its own column rather than one more deduction line: the advances statement has to be
        /// able to say what was recovered when, and a school asks "how much of the staff loans came
        /// back this month" far more often than it asks about any other deduction.
        /// </summary>
        public decimal AdvanceDeduction { get; set; }

        /// <summary>الإجمالي = <see cref="BasicSalary"/> + <see cref="Allowances"/> + <see cref="AdditionsTotal"/>.</summary>
        public decimal GrossPay { get; set; }

        /// <summary>الصافي = <see cref="GrossPay"/> − <see cref="DeductionsTotal"/> − <see cref="AdvanceDeduction"/>.</summary>
        public decimal NetPay { get; set; }

        /// <summary>ملاحظات on this employee's payslip.</summary>
        public string? Notes { get; set; }
    }
}
