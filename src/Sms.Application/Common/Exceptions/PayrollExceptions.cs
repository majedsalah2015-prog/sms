using System;
using System.Collections.Generic;
using Sms.Domain.Payroll;

namespace Sms.Application.Common.Exceptions
{
    // Payroll and salary advances (owner request, 2026-08-28). doc/Modules/12 §2 places payroll
    // calculation out of scope and BR-EMP-007 says the SMS never computes a net salary; the owner
    // asked for it anyway, so these rules have no BR- ids to cite. They are stated here and in
    // Sms.Domain.Payroll.PayrollRun rather than borrowed from rules that do not cover them.
    //
    // Every message here is English on purpose - the Web layer translates before a user sees it.

    /// <summary>A payroll period is a Gregorian (year, month) pair; this one is not a real month.</summary>
    public class InvalidPayrollPeriodException : InvalidOperationException
    {
        public InvalidPayrollPeriodException(int year, int month)
            : base($"'{year}-{month}' is not a payroll period this system will accept.")
        {
            Year = year;
            Month = month;
        }

        public int Year { get; }

        public int Month { get; }
    }

    /// <summary>One non-cancelled run per school per month - a second would pay everybody twice.</summary>
    public class DuplicatePayrollRunException : InvalidOperationException
    {
        public DuplicatePayrollRunException(int year, int month, string existingRunNo)
            : base($"Payroll run '{existingRunNo}' already covers {year}-{month}.")
        {
            Year = year;
            Month = month;
            ExistingRunNo = existingRunNo;
        }

        public int Year { get; }

        public int Month { get; }

        public string ExistingRunNo { get; }
    }

    /// <summary>The requested run status pair is not a legal move.</summary>
    public class InvalidPayrollRunStatusTransitionException : InvalidOperationException
    {
        public InvalidPayrollRunStatusTransitionException(PayrollRunStatus from, PayrollRunStatus to)
            : base($"A payroll run cannot move from '{from}' to '{to}'.")
        {
            From = from;
            To = to;
        }

        public PayrollRunStatus From { get; }

        public PayrollRunStatus To { get; }
    }

    /// <summary>Lines, adjustments and employees may only be changed while the run is a draft.</summary>
    public class PayrollRunNotEditableException : InvalidOperationException
    {
        public PayrollRunNotEditableException(string runNo, PayrollRunStatus status)
            : base($"Payroll run '{runNo}' is '{status}' and can no longer be edited.")
        {
            RunNo = runNo;
            Status = status;
        }

        public string RunNo { get; }

        public PayrollRunStatus Status { get; }
    }

    /// <summary>An empty run has nothing to approve - see <c>PayrollRunApprovalGuard</c>.</summary>
    public class EmptyPayrollRunException : InvalidOperationException
    {
        public EmptyPayrollRunException(string runNo)
            : base($"Payroll run '{runNo}' has no employee lines to approve.")
        {
            RunNo = runNo;
        }

        public string RunNo { get; }
    }

    /// <summary>
    /// Deductions exceed pay on at least one line. Carries every offending employee so the school
    /// fixes them in one pass rather than one approval attempt at a time.
    /// <para>
    /// Employees are named by their <b>employee number</b>, not their row id. The Web boundary has
    /// to say who is at fault in Arabic, and it cannot compose a name out of an integer without
    /// another database round trip — while "EMP-00007" is the same string in both languages and is
    /// what the payroll officer has in front of them. This is the codebase's stated pattern for a
    /// refusal with a "because": put the case in the exception, put the words at the boundary.
    /// </para>
    /// </summary>
    public class NegativeNetPayException : InvalidOperationException
    {
        public NegativeNetPayException(string runNo, IReadOnlyList<int> employeeIds, IReadOnlyList<string> employeeNos)
            : base($"Payroll run '{runNo}' has {employeeIds.Count} line(s) whose deductions exceed the pay: {string.Join(", ", employeeNos)}.")
        {
            RunNo = runNo;
            EmployeeIds = employeeIds;
            EmployeeNos = employeeNos;
        }

        public string RunNo { get; }

        public IReadOnlyList<int> EmployeeIds { get; }

        /// <summary>The offending employees' numbers, in the order the register lists them.</summary>
        public IReadOnlyList<string> EmployeeNos { get; }
    }

    /// <summary>A pay component arrived negative - a deduction typed into an addition box.</summary>
    public class NegativePayComponentException : InvalidOperationException
    {
        public NegativePayComponentException(string component, decimal value)
            : base($"Pay component '{component}' cannot be negative (was {value}).")
        {
            Component = component;
            Value = value;
        }

        public string Component { get; }

        public decimal Value { get; }
    }

    /// <summary>An employee already on the run cannot be added to it twice.</summary>
    public class DuplicatePayrollLineException : InvalidOperationException
    {
        public DuplicatePayrollLineException(int employeeId, string runNo)
            : base($"Employee {employeeId} already has a line on payroll run '{runNo}'.")
        {
            EmployeeId = employeeId;
            RunNo = runNo;
        }

        public int EmployeeId { get; }

        public string RunNo { get; }
    }

    /// <summary>An advance for nothing, or for a negative amount.</summary>
    public class InvalidAdvanceAmountException : InvalidOperationException
    {
        public InvalidAdvanceAmountException(decimal amount)
            : base($"A salary advance must be for a positive amount (was {amount}).")
        {
            Amount = amount;
        }

        public decimal Amount { get; }
    }

    /// <summary>The instalment count is outside 1..max, or so large that an instalment would round to nothing.</summary>
    public class InvalidAdvanceInstallmentCountException : InvalidOperationException
    {
        public InvalidAdvanceInstallmentCountException(int count, int maximum)
            : base($"A salary advance must be repaid over 1 to {maximum} instalments, each worth at least 0.01 (was {count}).")
        {
            Count = count;
            Maximum = maximum;
        }

        public int Count { get; }

        public int Maximum { get; }
    }

    /// <summary>The requested advance status pair is not a legal move.</summary>
    public class InvalidSalaryAdvanceStatusTransitionException : InvalidOperationException
    {
        public InvalidSalaryAdvanceStatusTransitionException(SalaryAdvanceStatus from, SalaryAdvanceStatus to)
            : base($"A salary advance cannot move from '{from}' to '{to}'.")
        {
            From = from;
            To = to;
        }

        public SalaryAdvanceStatus From { get; }

        public SalaryAdvanceStatus To { get; }
    }

    /// <summary>
    /// One advance at a time per employee. Lending again on top of an unrecovered advance is how a
    /// school ends up deducting more than it pays - and the negative-net refusal would then block
    /// the whole run.
    /// </summary>
    public class OutstandingAdvanceException : InvalidOperationException
    {
        public OutstandingAdvanceException(int employeeId, string outstandingAdvanceNo)
            : base($"Employee {employeeId} still has advance '{outstandingAdvanceNo}' outstanding.")
        {
            EmployeeId = employeeId;
            OutstandingAdvanceNo = outstandingAdvanceNo;
        }

        public int EmployeeId { get; }

        public string OutstandingAdvanceNo { get; }
    }

    /// <summary>An instalment already deducted by a paid run cannot be waived - the money came back.</summary>
    public class InstallmentNotWaivableException : InvalidOperationException
    {
        public InstallmentNotWaivableException(int installmentId, SalaryAdvanceInstallmentStatus status)
            : base($"Advance instalment {installmentId} is '{status}' and cannot be waived.")
        {
            InstallmentId = installmentId;
            Status = status;
        }

        public int InstallmentId { get; }

        public SalaryAdvanceInstallmentStatus Status { get; }
    }

    /// <summary>
    /// The instalment falls in a month whose payroll has already been approved or paid.
    /// <para>
    /// Waiving it — or rescheduling around it — would leave a payslip claiming a deduction the
    /// employee never suffered, and the advances statement would stop reconciling to the payroll
    /// register. Reopen the run first, or waive from the following month.
    /// </para>
    /// </summary>
    public class InstallmentLockedByPayrollRunException : InvalidOperationException
    {
        public InstallmentLockedByPayrollRunException(int installmentId, string runNo, PayrollRunStatus runStatus)
            : base($"Advance instalment {installmentId} falls in a month whose payroll run '{runNo}' is already '{runStatus}'.")
        {
            InstallmentId = installmentId;
            RunNo = runNo;
            RunStatus = runStatus;
        }

        public int InstallmentId { get; }

        public string RunNo { get; }

        public PayrollRunStatus RunStatus { get; }
    }

    /// <summary>An employee with no active contract has no pay figures to snapshot.</summary>
    public class NoActiveContractException : InvalidOperationException
    {
        public NoActiveContractException(int employeeId)
            : base($"Employee {employeeId} has no active contract covering this payroll period.")
        {
            EmployeeId = employeeId;
        }

        public int EmployeeId { get; }
    }
}
