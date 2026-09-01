using System.Threading;
using System.Threading.Tasks;

namespace Sms.Application.Payroll
{
    /// <summary>
    /// الكشوفات - the four statements the owner asked for on 2026-08-28, read-only.
    /// <para>
    /// A separate port from <see cref="IPayrollAdmin"/> on the same reasoning that keeps
    /// <c>IStatementService</c> apart from the fees admin: these answer questions, write nothing,
    /// and are the half of the module a school actually prints. Keeping them separate also keeps
    /// the read screens off a port whose every other method saves.
    /// </para>
    /// </summary>
    public interface IPayrollStatements
    {
        /// <summary>
        /// مسير الرواتب الشهري - the whole school for one month, with column totals.
        /// Throws <see cref="System.InvalidOperationException"/> when the run does not exist.
        /// </summary>
        Task<PayrollRegister> BuildRegisterAsync(int runId, CancellationToken cancellationToken = default);

        /// <summary>
        /// قسيمة راتب الموظف - one payslip, with its adjustments and advance instalments broken out.
        /// </summary>
        Task<Payslip> BuildPayslipAsync(int lineId, CancellationToken cancellationToken = default);

        /// <summary>
        /// كشف التحويل البنكي - the disbursement list for one run, with the employees who have no
        /// payment destination counted rather than silently printed blank.
        /// </summary>
        Task<BankTransferList> BuildBankTransferListAsync(int runId, CancellationToken cancellationToken = default);

        /// <summary>
        /// كشف السلف - one employee's advances, every instalment, and the balance still owed.
        /// </summary>
        Task<EmployeeAdvanceStatement> BuildAdvanceStatementAsync(int employeeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// كشف السلف القائمة - what the school is owed across all staff, newest disbursement first.
        /// </summary>
        Task<OutstandingAdvancesReport> BuildOutstandingAdvancesAsync(CancellationToken cancellationToken = default);
    }
}
