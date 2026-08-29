using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Payroll;
using Sms.Domain.Employees;
using Sms.Domain.Payroll;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Payroll
{
    /// <summary>
    /// الكشوفات — the four statements the owner asked for on 2026-08-28, read-only.
    /// <para>
    /// Every total here is summed in memory after materialising the column. <c>SumAsync()</c> over
    /// a decimal compiles and then throws at runtime on Sqlite, which is what the whole test suite
    /// runs on, so a statement that summed in the database would pass review and fail in a test.
    /// </para>
    /// <para>
    /// See <c>Sms.Domain.Payroll.PayrollRun</c> for the stated deviation from doc/Modules/12 §2.
    /// </para>
    /// </summary>
    public class PayrollStatements : IPayrollStatements
    {
        private readonly AppDbContext _db;

        public PayrollStatements(AppDbContext db)
        {
            _db = db;
        }

        public async Task<PayrollRegister> BuildRegisterAsync(int runId, CancellationToken cancellationToken = default)
        {
            var run = await _db.PayrollRuns.SingleAsync(r => r.Id == runId, cancellationToken);

            var rows = await (
                from line in _db.PayrollRunLines
                join employee in _db.Employees on line.EmployeeId equals employee.Id
                where line.PayrollRunId == runId
                select new
                {
                    line.Id,
                    line.EmployeeId,
                    employee.EmployeeNo,
                    employee.FirstNameAr,
                    employee.FatherNameAr,
                    employee.GrandfatherNameAr,
                    employee.FamilyNameAr,
                    employee.FirstNameEn,
                    employee.FatherNameEn,
                    employee.GrandfatherNameEn,
                    employee.FamilyNameEn,
                    line.BasicSalary,
                    line.Allowances,
                    line.AdditionsTotal,
                    line.DeductionsTotal,
                    line.AdvanceDeduction,
                    line.GrossPay,
                    line.NetPay,
                })
                .ToListAsync(cancellationToken);

            var lines = rows
                .Select(r => new PayrollRegisterLine(
                    r.Id,
                    new PayrollStatementEmployee(
                        r.EmployeeId,
                        r.EmployeeNo,
                        Join(r.FirstNameAr, r.FatherNameAr, r.GrandfatherNameAr, r.FamilyNameAr),
                        Join(r.FirstNameEn, r.FatherNameEn, r.GrandfatherNameEn, r.FamilyNameEn)),
                    r.BasicSalary,
                    r.Allowances,
                    r.AdditionsTotal,
                    r.DeductionsTotal,
                    r.AdvanceDeduction,
                    r.GrossPay,
                    r.NetPay))
                .OrderBy(l => l.Employee.EmployeeNo, StringComparer.Ordinal)
                .ToList();

            return new PayrollRegister(
                run.Id,
                run.PayrollRunNo,
                run.PeriodYear,
                run.PeriodMonth,
                run.PaymentDate,
                run.Status,
                lines,
                lines.Sum(l => l.BasicSalary),
                lines.Sum(l => l.Allowances),
                lines.Sum(l => l.AdditionsTotal),
                lines.Sum(l => l.DeductionsTotal),
                lines.Sum(l => l.AdvanceDeduction),
                lines.Sum(l => l.GrossPay),
                lines.Sum(l => l.NetPay));
        }

        public async Task<Payslip> BuildPayslipAsync(int lineId, CancellationToken cancellationToken = default)
        {
            var row = await (
                from line in _db.PayrollRunLines
                join run in _db.PayrollRuns on line.PayrollRunId equals run.Id
                join employee in _db.Employees on line.EmployeeId equals employee.Id
                where line.Id == lineId
                select new { Line = line, Run = run, Employee = employee })
                .SingleAsync(cancellationToken);

            var adjustments = await _db.PayrollLineAdjustments
                .Where(a => a.PayrollRunLineId == lineId)
                .OrderBy(a => a.Kind).ThenBy(a => a.Id)
                .Select(a => new { a.Kind, a.Description, a.Amount })
                .ToListAsync(cancellationToken);

            // The instalments this payslip carried. While the run is still open they are the rows
            // due in its month; once it is paid they are the rows that name this very line. Both
            // reads are here because a draft payslip has to show what it is about to deduct.
            var installmentRows = await (
                from installment in _db.SalaryAdvanceInstallments
                join advance in _db.SalaryAdvances on installment.SalaryAdvanceId equals advance.Id
                where advance.EmployeeId == row.Line.EmployeeId
                      && ((installment.PayrollRunLineId == lineId)
                          || (installment.Status == SalaryAdvanceInstallmentStatus.Scheduled
                              && installment.DueYear == row.Run.PeriodYear
                              && installment.DueMonth == row.Run.PeriodMonth
                              && advance.Status == SalaryAdvanceStatus.Disbursed))
                select new
                {
                    advance.AdvanceNo,
                    AdvanceId = advance.Id,
                    advance.Amount,
                    installment.SequenceNo,
                    installment.DueYear,
                    installment.DueMonth,
                    InstallmentAmount = installment.Amount,
                })
                .ToListAsync(cancellationToken);

            var advanceIds = installmentRows.Select(i => i.AdvanceId).Distinct().ToList();
            var recoveredByAdvance = await RecoveredByAdvanceAsync(advanceIds, cancellationToken);
            var countsByAdvance = await InstallmentCountsAsync(advanceIds, cancellationToken);

            var advanceInstallments = installmentRows
                .OrderBy(i => i.AdvanceNo, StringComparer.Ordinal).ThenBy(i => i.SequenceNo)
                .Select(i => new PayslipAdvanceInstallment(
                    i.AdvanceNo,
                    i.SequenceNo,
                    countsByAdvance.TryGetValue(i.AdvanceId, out var count) ? count : 0,
                    i.InstallmentAmount,

                    // What is still owed once this payslip's instalment is counted, so the employee
                    // can read the end of the schedule off the slip in their hand.
                    i.Amount - (recoveredByAdvance.TryGetValue(i.AdvanceId, out var recovered) ? recovered : 0m)
                        - (row.Run.Status == PayrollRunStatus.Paid ? 0m : i.InstallmentAmount)))
                .ToList();

            return new Payslip(
                row.Line.Id,
                row.Run.Id,
                row.Run.PayrollRunNo,
                row.Run.PeriodYear,
                row.Run.PeriodMonth,
                row.Run.PaymentDate,
                row.Run.Status,
                Describe(row.Employee),
                row.Employee.BankName,
                row.Employee.BankAccountNo,
                row.Line.BasicSalary,
                row.Line.Allowances,
                adjustments.Select(a => new PayslipAdjustment(a.Kind, a.Description, a.Amount)).ToList(),
                advanceInstallments,
                row.Line.AdditionsTotal,
                row.Line.DeductionsTotal,
                row.Line.AdvanceDeduction,
                row.Line.GrossPay,
                row.Line.NetPay,
                row.Line.Notes);
        }

        public async Task<BankTransferList> BuildBankTransferListAsync(int runId, CancellationToken cancellationToken = default)
        {
            var run = await _db.PayrollRuns.SingleAsync(r => r.Id == runId, cancellationToken);

            var rows = await (
                from line in _db.PayrollRunLines
                join employee in _db.Employees on line.EmployeeId equals employee.Id
                where line.PayrollRunId == runId
                select new { Employee = employee, line.NetPay })
                .ToListAsync(cancellationToken);

            var transferRows = rows
                .Select(r => new BankTransferRow(
                    Describe(r.Employee),
                    r.Employee.BankName,
                    r.Employee.BankAccountNo,
                    r.Employee.PalPayWalletNo,
                    r.Employee.JawwalPayWalletNo,
                    r.NetPay))
                .OrderBy(r => r.Employee.EmployeeNo, StringComparer.Ordinal)
                .ToList();

            return new BankTransferList(
                run.Id,
                run.PayrollRunNo,
                run.PeriodYear,
                run.PeriodMonth,
                run.PaymentDate,
                run.Status,
                transferRows,
                transferRows.Sum(r => r.NetPay),
                transferRows.Count(r => !r.HasDestination));
        }

        public async Task<EmployeeAdvanceStatement> BuildAdvanceStatementAsync(int employeeId, CancellationToken cancellationToken = default)
        {
            var employee = await _db.Employees.SingleAsync(e => e.Id == employeeId, cancellationToken);

            var advances = await _db.SalaryAdvances
                .Where(a => a.EmployeeId == employeeId)
                .OrderByDescending(a => a.Id)
                .ToListAsync(cancellationToken);

            var advanceIds = advances.Select(a => a.Id).ToList();
            var installments = await InstallmentViewsAsync(advanceIds, cancellationToken);

            var views = advances
                .Select(advance =>
                {
                    var own = installments.TryGetValue(advance.Id, out var list)
                        ? list
                        : new List<AdvanceInstallmentView>();

                    var deducted = own.Where(i => i.Status == SalaryAdvanceInstallmentStatus.Deducted).Sum(i => i.Amount);
                    var waived = own.Where(i => i.Status == SalaryAdvanceInstallmentStatus.Waived).Sum(i => i.Amount);

                    // A request nobody has handed money over for owes nothing, whatever its
                    // schedule would eventually say.
                    var outstanding = advance.Status == SalaryAdvanceStatus.Disbursed
                        ? advance.Amount - deducted - waived
                        : 0m;

                    return new AdvanceView(
                        advance.Id,
                        advance.AdvanceNo,
                        advance.RequestDate,
                        advance.Amount,
                        advance.InstallmentCount,
                        advance.Status,
                        advance.DisbursedOn,
                        advance.DisbursementMethod,
                        advance.Reason,
                        deducted,
                        waived,
                        outstanding,
                        own);
                })
                .ToList();

            var disbursed = views.Where(v => v.DisbursedOn != null).ToList();

            return new EmployeeAdvanceStatement(
                Describe(employee),
                views,
                disbursed.Sum(v => v.Amount),
                views.Sum(v => v.DeductedTotal),
                views.Sum(v => v.WaivedTotal),
                views.Sum(v => v.OutstandingBalance));
        }

        public async Task<OutstandingAdvancesReport> BuildOutstandingAdvancesAsync(CancellationToken cancellationToken = default)
        {
            var rows = await (
                from advance in _db.SalaryAdvances
                join employee in _db.Employees on advance.EmployeeId equals employee.Id
                where advance.Status == SalaryAdvanceStatus.Disbursed
                select new { Advance = advance, Employee = employee })
                .ToListAsync(cancellationToken);

            var advanceIds = rows.Select(r => r.Advance.Id).ToList();
            var installments = await InstallmentViewsAsync(advanceIds, cancellationToken);

            var reportRows = rows
                .Select(r =>
                {
                    var own = installments.TryGetValue(r.Advance.Id, out var list)
                        ? list
                        : new List<AdvanceInstallmentView>();

                    var deducted = own.Where(i => i.Status == SalaryAdvanceInstallmentStatus.Deducted).Sum(i => i.Amount);
                    var waived = own.Where(i => i.Status == SalaryAdvanceInstallmentStatus.Waived).Sum(i => i.Amount);
                    var open = own.Where(i => i.Status == SalaryAdvanceInstallmentStatus.Scheduled)
                        .OrderBy(i => i.DueYear).ThenBy(i => i.DueMonth)
                        .ToList();
                    var next = open.FirstOrDefault();

                    return new OutstandingAdvanceRow(
                        Describe(r.Employee),
                        r.Advance.Id,
                        r.Advance.AdvanceNo,
                        r.Advance.DisbursedOn,
                        r.Advance.Amount,
                        deducted,
                        waived,
                        r.Advance.Amount - deducted - waived,
                        open.Count,
                        next?.DueYear,
                        next?.DueMonth);
                })
                .OrderByDescending(r => r.DisbursedOn)
                .ThenBy(r => r.Employee.EmployeeNo, StringComparer.Ordinal)
                .ToList();

            return new OutstandingAdvancesReport(
                reportRows,
                reportRows.Sum(r => r.Amount),
                reportRows.Sum(r => r.Deducted),
                reportRows.Sum(r => r.Waived),
                reportRows.Sum(r => r.Outstanding));
        }

        // ------------------------------------------------------------------ internals

        /// <summary>Every instalment of the given advances, grouped by advance, with the run that consumed each named.</summary>
        private async Task<Dictionary<int, List<AdvanceInstallmentView>>> InstallmentViewsAsync(
            IReadOnlyCollection<int> advanceIds, CancellationToken cancellationToken)
        {
            if (advanceIds.Count == 0)
            {
                return new Dictionary<int, List<AdvanceInstallmentView>>();
            }

            var rows = await (
                from installment in _db.SalaryAdvanceInstallments
                where advanceIds.Contains(installment.SalaryAdvanceId)
                join line in _db.PayrollRunLines on installment.PayrollRunLineId equals line.Id into lineGroup
                from line in lineGroup.DefaultIfEmpty()
                join run in _db.PayrollRuns on line.PayrollRunId equals run.Id into runGroup
                from run in runGroup.DefaultIfEmpty()
                select new
                {
                    installment.SalaryAdvanceId,
                    installment.Id,
                    installment.SequenceNo,
                    installment.DueYear,
                    installment.DueMonth,
                    installment.Amount,
                    installment.Status,
                    installment.DeductedAtUtc,
                    installment.WaiverNote,
                    RunNo = run == null ? null : run.PayrollRunNo,
                })
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(r => r.SalaryAdvanceId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(r => r.SequenceNo)
                        .Select(r => new AdvanceInstallmentView(
                            r.Id, r.SequenceNo, r.DueYear, r.DueMonth, r.Amount, r.Status,
                            r.DeductedAtUtc, r.RunNo, r.WaiverNote))
                        .ToList());
        }

        /// <summary>How much of each advance has already been accounted for — deducted plus waived.</summary>
        private async Task<Dictionary<int, decimal>> RecoveredByAdvanceAsync(
            IReadOnlyCollection<int> advanceIds, CancellationToken cancellationToken)
        {
            if (advanceIds.Count == 0)
            {
                return new Dictionary<int, decimal>();
            }

            var rows = await _db.SalaryAdvanceInstallments
                .Where(i => advanceIds.Contains(i.SalaryAdvanceId)
                            && i.Status != SalaryAdvanceInstallmentStatus.Scheduled)
                .Select(i => new { i.SalaryAdvanceId, i.Amount })
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(r => r.SalaryAdvanceId)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));
        }

        private async Task<Dictionary<int, int>> InstallmentCountsAsync(
            IReadOnlyCollection<int> advanceIds, CancellationToken cancellationToken)
        {
            if (advanceIds.Count == 0)
            {
                return new Dictionary<int, int>();
            }

            var rows = await _db.SalaryAdvanceInstallments
                .Where(i => advanceIds.Contains(i.SalaryAdvanceId))
                .Select(i => i.SalaryAdvanceId)
                .ToListAsync(cancellationToken);

            return rows.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        }

        private static PayrollStatementEmployee Describe(Employee employee) =>
            new(employee.Id,
                employee.EmployeeNo,
                Join(employee.FirstNameAr, employee.FatherNameAr, employee.GrandfatherNameAr, employee.FamilyNameAr),
                Join(employee.FirstNameEn, employee.FatherNameEn, employee.GrandfatherNameEn, employee.FamilyNameEn));

        private static string Join(params string[] parts) =>
            string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}
