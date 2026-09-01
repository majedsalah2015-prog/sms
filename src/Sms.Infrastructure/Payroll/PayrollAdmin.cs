using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Numbering;
using Sms.Application.Payroll;
using Sms.Domain.Employees;
using Sms.Domain.Payroll;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Payroll
{
    /// <summary>
    /// مسير الرواتب (owner request, 2026-08-28). Standalone shape — each method saves itself.
    /// <para>
    /// See <c>Sms.Domain.Payroll.PayrollRun</c> for the stated deviation from doc/Modules/12 §2 and
    /// BR-EMP-007, and for what this deliberately does not do: no GL journal, no WPS return, no
    /// end-of-service settlement, no tax.
    /// </para>
    /// <para>
    /// The line and run totals are recomputed in one private place after every mutation, so the
    /// cached columns on <c>ppl.PayrollRun</c> cannot drift from the rows beneath them.
    /// </para>
    /// </summary>
    public class PayrollAdmin : IPayrollAdmin
    {
        private readonly AppDbContext _db;
        private readonly INumberIssuer _numberIssuer;
        private readonly IClock _clock;

        public PayrollAdmin(AppDbContext db, INumberIssuer numberIssuer, IClock clock)
        {
            _db = db;
            _numberIssuer = numberIssuer;
            _clock = clock;
        }

        public async Task<PayrollRun> OpenRunAsync(
            int periodYear, int periodMonth, DateTime paymentDate, string? notes = null,
            CancellationToken cancellationToken = default)
        {
            PayrollPeriodMath.EnsureValid(periodYear, periodMonth);

            var existing = await _db.PayrollRuns
                .Where(r => r.PeriodYear == periodYear
                            && r.PeriodMonth == periodMonth
                            && r.Status != PayrollRunStatus.Cancelled)
                .Select(r => r.PayrollRunNo)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing != null)
            {
                throw new DuplicatePayrollRunException(periodYear, periodMonth, existing);
            }

            var run = new PayrollRun
            {
                PayrollRunNo = await _numberIssuer.IssueAsync("PAY", cancellationToken),
                PeriodYear = periodYear,
                PeriodMonth = periodMonth,
                PaymentDate = paymentDate,
                Status = PayrollRunStatus.Draft,
                Notes = Blank(notes),
            };

            _db.PayrollRuns.Add(run);
            await _db.SaveChangesAsync(cancellationToken);
            return run;
        }

        public async Task<PayrollRun> UpdateRunAsync(
            int runId, DateTime paymentDate, string? notes, CancellationToken cancellationToken = default)
        {
            var run = await LoadEditableRunAsync(runId, cancellationToken);

            run.PaymentDate = paymentDate;
            run.Notes = Blank(notes);

            await _db.SaveChangesAsync(cancellationToken);
            return run;
        }

        public async Task<PayrollRun> GenerateLinesAsync(int runId, CancellationToken cancellationToken = default)
        {
            var run = await LoadEditableRunAsync(runId, cancellationToken);

            // A regeneration, not a merge — the screen warns before it calls. Adjustments go with
            // the lines that carried them; keeping them against rebuilt figures would leave a
            // payslip half-derived from a contract that has since changed.
            var oldLines = await _db.PayrollRunLines.Where(l => l.PayrollRunId == runId).ToListAsync(cancellationToken);
            var oldLineIds = oldLines.Select(l => l.Id).ToList();
            var oldAdjustments = await _db.PayrollLineAdjustments
                .Where(a => oldLineIds.Contains(a.PayrollRunLineId))
                .ToListAsync(cancellationToken);

            _db.PayrollLineAdjustments.RemoveRange(oldAdjustments);
            _db.PayrollRunLines.RemoveRange(oldLines);

            // Committed before the new lines are added rather than batched with them: the rebuilt
            // rows reuse the (run, employee) pair the old ones held, and that pair is a unique
            // index. Letting one SaveChanges decide whether the deletes reach the database before
            // the inserts is a coin toss this does not need to take.
            await _db.SaveChangesAsync(cancellationToken);

            var (periodStart, periodEnd) = PeriodBounds(run.PeriodYear, run.PeriodMonth);

            // An active contract that overlaps the month, for an employee who is still on the
            // books. A contract that started mid-month still earns — proration is not a rule this
            // product has been given, and inventing one would be a substitution.
            var contracts = await _db.Contracts
                .Where(c => c.Status == ContractStatus.Active
                            && c.StartDate <= periodEnd
                            && c.EndDate >= periodStart)
                .Select(c => new { c.Id, c.EmployeeId, c.SalaryBasic, c.SalaryAllowances })
                .ToListAsync(cancellationToken);

            var activeEmployeeIds = await _db.Employees
                .Where(e => e.Status == EmployeeStatus.Active)
                .Select(e => e.Id)
                .ToListAsync(cancellationToken);
            var activeEmployees = new HashSet<int>(activeEmployeeIds);

            // One line per employee even when the paperwork left two overlapping contracts behind:
            // the newest wins, and the duplicate is a contracts problem, not a payroll one.
            var payable = contracts
                .Where(c => activeEmployees.Contains(c.EmployeeId))
                .GroupBy(c => c.EmployeeId)
                .Select(g => g.OrderByDescending(c => c.Id).First())
                .OrderBy(c => c.EmployeeId)
                .ToList();

            var dueInstallments = await DueInstallmentsAsync(run.PeriodYear, run.PeriodMonth, cancellationToken);

            foreach (var contract in payable)
            {
                var advanceDue = dueInstallments
                    .Where(i => i.EmployeeId == contract.EmployeeId)
                    .Sum(i => i.Amount);

                var totals = PayrollLineCalculator.Calculate(
                    contract.SalaryBasic,
                    contract.SalaryAllowances ?? 0m,
                    Array.Empty<(PayrollAdjustmentKind, decimal)>(),
                    advanceDue);

                _db.PayrollRunLines.Add(new PayrollRunLine
                {
                    PayrollRunId = runId,
                    EmployeeId = contract.EmployeeId,
                    ContractId = contract.Id,
                    BasicSalary = contract.SalaryBasic,
                    Allowances = contract.SalaryAllowances ?? 0m,
                    AdditionsTotal = totals.AdditionsTotal,
                    DeductionsTotal = totals.DeductionsTotal,
                    AdvanceDeduction = advanceDue,
                    GrossPay = totals.GrossPay,
                    NetPay = totals.NetPay,
                });
            }

            await _db.SaveChangesAsync(cancellationToken);

            await RestateRunTotalsAsync(run, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return run;
        }

        public async Task<PayrollRunLine> AddLineAsync(
            int runId, int employeeId, decimal? basicSalary = null, decimal? allowances = null,
            CancellationToken cancellationToken = default)
        {
            var run = await LoadEditableRunAsync(runId, cancellationToken);

            var already = await _db.PayrollRunLines
                .AnyAsync(l => l.PayrollRunId == runId && l.EmployeeId == employeeId, cancellationToken);
            if (already)
            {
                throw new DuplicatePayrollLineException(employeeId, run.PayrollRunNo);
            }

            var (periodStart, periodEnd) = PeriodBounds(run.PeriodYear, run.PeriodMonth);
            var contract = await _db.Contracts
                .Where(c => c.EmployeeId == employeeId
                            && c.Status == ContractStatus.Active
                            && c.StartDate <= periodEnd
                            && c.EndDate >= periodStart)
                .OrderByDescending(c => c.Id)
                .Select(c => new { c.Id, c.SalaryBasic, c.SalaryAllowances })
                .FirstOrDefaultAsync(cancellationToken);

            if (contract == null && basicSalary == null)
            {
                throw new NoActiveContractException(employeeId);
            }

            var basic = basicSalary ?? contract!.SalaryBasic;
            var allowance = allowances ?? contract?.SalaryAllowances ?? 0m;

            var dueInstallments = await DueInstallmentsAsync(run.PeriodYear, run.PeriodMonth, cancellationToken);
            var advanceDue = dueInstallments.Where(i => i.EmployeeId == employeeId).Sum(i => i.Amount);

            var totals = PayrollLineCalculator.Calculate(
                basic, allowance, Array.Empty<(PayrollAdjustmentKind, decimal)>(), advanceDue);

            var line = new PayrollRunLine
            {
                PayrollRunId = runId,
                EmployeeId = employeeId,
                ContractId = contract?.Id,
                BasicSalary = basic,
                Allowances = allowance,
                AdditionsTotal = totals.AdditionsTotal,
                DeductionsTotal = totals.DeductionsTotal,
                AdvanceDeduction = advanceDue,
                GrossPay = totals.GrossPay,
                NetPay = totals.NetPay,
            };

            _db.PayrollRunLines.Add(line);
            await _db.SaveChangesAsync(cancellationToken);

            await RestateRunTotalsAsync(run, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return line;
        }

        public async Task RemoveLineAsync(int lineId, CancellationToken cancellationToken = default)
        {
            var line = await _db.PayrollRunLines.SingleAsync(l => l.Id == lineId, cancellationToken);
            var run = await LoadEditableRunAsync(line.PayrollRunId, cancellationToken);

            var adjustments = await _db.PayrollLineAdjustments
                .Where(a => a.PayrollRunLineId == lineId)
                .ToListAsync(cancellationToken);

            _db.PayrollLineAdjustments.RemoveRange(adjustments);
            _db.PayrollRunLines.Remove(line);
            await _db.SaveChangesAsync(cancellationToken);

            await RestateRunTotalsAsync(run, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<PayrollLineAdjustment> AddAdjustmentAsync(
            int lineId, PayrollAdjustmentKind kind, string description, decimal amount,
            CancellationToken cancellationToken = default)
        {
            if (amount <= 0m)
            {
                throw new NegativePayComponentException(nameof(amount), amount);
            }

            var line = await _db.PayrollRunLines.SingleAsync(l => l.Id == lineId, cancellationToken);
            var run = await LoadEditableRunAsync(line.PayrollRunId, cancellationToken);

            var adjustment = new PayrollLineAdjustment
            {
                PayrollRunLineId = lineId,
                Kind = kind,
                Description = description.Trim(),
                Amount = amount,
            };

            _db.PayrollLineAdjustments.Add(adjustment);
            await _db.SaveChangesAsync(cancellationToken);

            await RestateLineAsync(line, cancellationToken);
            await RestateRunTotalsAsync(run, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return adjustment;
        }

        public async Task RemoveAdjustmentAsync(int adjustmentId, CancellationToken cancellationToken = default)
        {
            var adjustment = await _db.PayrollLineAdjustments.SingleAsync(a => a.Id == adjustmentId, cancellationToken);
            var line = await _db.PayrollRunLines.SingleAsync(l => l.Id == adjustment.PayrollRunLineId, cancellationToken);
            var run = await LoadEditableRunAsync(line.PayrollRunId, cancellationToken);

            _db.PayrollLineAdjustments.Remove(adjustment);
            await _db.SaveChangesAsync(cancellationToken);

            await RestateLineAsync(line, cancellationToken);
            await RestateRunTotalsAsync(run, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<PayrollRunLine> SetLineNotesAsync(int lineId, string? notes, CancellationToken cancellationToken = default)
        {
            var line = await _db.PayrollRunLines.SingleAsync(l => l.Id == lineId, cancellationToken);
            await LoadEditableRunAsync(line.PayrollRunId, cancellationToken);

            line.Notes = Blank(notes);
            await _db.SaveChangesAsync(cancellationToken);
            return line;
        }

        public async Task<PayrollRun> ApproveRunAsync(int runId, CancellationToken cancellationToken = default)
        {
            var run = await LoadRunAsync(runId, cancellationToken);
            EnsureTransition(run.Status, PayrollRunStatus.Approved);

            var lines = await _db.PayrollRunLines
                .Where(l => l.PayrollRunId == runId)
                .Select(l => new { l.EmployeeId, l.NetPay })
                .ToListAsync(cancellationToken);

            if (!PayrollRunApprovalGuard.HasPayableContent(lines.Count))
            {
                throw new EmptyPayrollRunException(run.PayrollRunNo);
            }

            var unpayable = PayrollRunApprovalGuard.FindUnpayableEmployees(
                lines.Select(l => (l.EmployeeId, l.NetPay)));
            if (unpayable.Count > 0)
            {
                // Resolved here rather than at the boundary: the refusal has to name who is at
                // fault, and a row id names nobody. Loaded only on the failing path.
                var employeeNos = await _db.Employees
                    .Where(e => unpayable.Contains(e.Id))
                    .OrderBy(e => e.EmployeeNo)
                    .Select(e => e.EmployeeNo)
                    .ToListAsync(cancellationToken);

                throw new NegativeNetPayException(run.PayrollRunNo, unpayable, employeeNos);
            }

            run.Status = PayrollRunStatus.Approved;
            run.ApprovedAtUtc = _clock.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
            return run;
        }

        public async Task<PayrollRun> ReopenRunAsync(int runId, CancellationToken cancellationToken = default)
        {
            var run = await LoadRunAsync(runId, cancellationToken);
            EnsureTransition(run.Status, PayrollRunStatus.Draft);

            run.Status = PayrollRunStatus.Draft;
            run.ApprovedAtUtc = null;

            await _db.SaveChangesAsync(cancellationToken);
            return run;
        }

        public async Task<PayrollRun> MarkRunPaidAsync(int runId, DateTime paidOn, CancellationToken cancellationToken = default)
        {
            var run = await LoadRunAsync(runId, cancellationToken);
            EnsureTransition(run.Status, PayrollRunStatus.Paid);

            var lines = await _db.PayrollRunLines
                .Where(l => l.PayrollRunId == runId)
                .ToListAsync(cancellationToken);
            var lineByEmployee = lines.ToDictionary(l => l.EmployeeId, l => l.Id);

            // The money has moved, so the instalments this run carried are now recovered. Only
            // still-scheduled rows are touched — a waiver in the same month is blocked while the
            // run stands approved, so there should be none, and if one slipped through it is not
            // this method's place to resurrect it.
            var due = await DueInstallmentsAsync(run.PeriodYear, run.PeriodMonth, cancellationToken);
            var touchedAdvanceIds = new HashSet<int>();

            foreach (var installment in due)
            {
                if (!lineByEmployee.TryGetValue(installment.EmployeeId, out var lineId))
                {
                    continue;
                }

                installment.Row.Status = SalaryAdvanceInstallmentStatus.Deducted;
                installment.Row.PayrollRunLineId = lineId;
                installment.Row.DeductedAtUtc = _clock.UtcNow;
                touchedAdvanceIds.Add(installment.Row.SalaryAdvanceId);
            }

            await SettleExhaustedAdvancesAsync(touchedAdvanceIds, cancellationToken);

            run.Status = PayrollRunStatus.Paid;

            // PaymentDate stops being the plan and becomes the fact; PaidAtUtc is when the school
            // recorded it. Two columns because they answer different questions — the register is
            // dated by the first, and the audit trail runs on the second.
            run.PaymentDate = paidOn;
            run.PaidAtUtc = _clock.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
            return run;
        }

        public async Task<PayrollRun> CancelRunAsync(int runId, string? reason, CancellationToken cancellationToken = default)
        {
            var run = await LoadRunAsync(runId, cancellationToken);
            EnsureTransition(run.Status, PayrollRunStatus.Cancelled);

            run.Status = PayrollRunStatus.Cancelled;
            run.CancelledAtUtc = _clock.UtcNow;
            run.Notes = Blank(reason) ?? run.Notes;

            await _db.SaveChangesAsync(cancellationToken);
            return run;
        }

        // ------------------------------------------------------------------ internals

        private Task<PayrollRun> LoadRunAsync(int runId, CancellationToken cancellationToken) =>
            _db.PayrollRuns.SingleAsync(r => r.Id == runId, cancellationToken);

        private async Task<PayrollRun> LoadEditableRunAsync(int runId, CancellationToken cancellationToken)
        {
            var run = await LoadRunAsync(runId, cancellationToken);
            if (!PayrollRunStatusTransitions.IsEditable(run.Status))
            {
                throw new PayrollRunNotEditableException(run.PayrollRunNo, run.Status);
            }

            return run;
        }

        private static void EnsureTransition(PayrollRunStatus from, PayrollRunStatus to)
        {
            if (!PayrollRunStatusTransitions.CanTransition(from, to))
            {
                throw new InvalidPayrollRunStatusTransitionException(from, to);
            }
        }

        /// <summary>First and last day of the payroll month, as a contract's dates are compared against.</summary>
        private static (DateTime Start, DateTime End) PeriodBounds(int year, int month)
        {
            var start = new DateTime(year, month, 1);
            return (start, start.AddMonths(1).AddDays(-1));
        }

        /// <summary>
        /// The advance instalments falling due in one month, with the employee each belongs to —
        /// tracked rows, because <see cref="MarkRunPaidAsync"/> writes to them.
        /// <para>
        /// Only instalments of a Disbursed advance count: a request still awaiting a decision owes
        /// nothing, and a settled one has nothing left.
        /// </para>
        /// </summary>
        private async Task<List<DueInstallment>> DueInstallmentsAsync(int year, int month, CancellationToken cancellationToken)
        {
            var rows = await (
                from installment in _db.SalaryAdvanceInstallments
                join advance in _db.SalaryAdvances on installment.SalaryAdvanceId equals advance.Id
                where installment.DueYear == year
                      && installment.DueMonth == month
                      && installment.Status == SalaryAdvanceInstallmentStatus.Scheduled
                      && advance.Status == SalaryAdvanceStatus.Disbursed
                select new { Installment = installment, advance.EmployeeId })
                .ToListAsync(cancellationToken);

            return rows
                .Select(r => new DueInstallment(r.EmployeeId, r.Installment.Amount, r.Installment))
                .ToList();
        }

        private sealed record DueInstallment(int EmployeeId, decimal Amount, SalaryAdvanceInstallment Row);

        /// <summary>
        /// Recomputes one line from its adjustments through the engine, so the arithmetic on a
        /// payslip is never restated in a service.
        /// </summary>
        private async Task RestateLineAsync(PayrollRunLine line, CancellationToken cancellationToken)
        {
            var adjustments = await _db.PayrollLineAdjustments
                .Where(a => a.PayrollRunLineId == line.Id)
                .Select(a => new { a.Kind, a.Amount })
                .ToListAsync(cancellationToken);

            var totals = PayrollLineCalculator.Calculate(
                line.BasicSalary,
                line.Allowances,
                adjustments.Select(a => (a.Kind, a.Amount)),
                line.AdvanceDeduction);

            line.AdditionsTotal = totals.AdditionsTotal;
            line.DeductionsTotal = totals.DeductionsTotal;
            line.GrossPay = totals.GrossPay;
            line.NetPay = totals.NetPay;
        }

        /// <summary>
        /// Restates the run's cached totals from its lines.
        /// <para>
        /// Materialised and summed in memory: <c>SumAsync()</c> over a decimal column compiles and
        /// then throws at runtime on Sqlite, which is what every test here runs on.
        /// </para>
        /// <para>
        /// The lines are loaded as <b>entities, not a projection</b>, and that is not a style
        /// choice. A tracked query identity-resolves to instances the change tracker already holds,
        /// so a line <see cref="RestateLineAsync"/> has just recomputed but not yet saved is summed
        /// at its new value. Projecting into an anonymous type bypasses the tracker and reads the
        /// database, which left the run's totals lagging exactly one adjustment behind — the drift
        /// this cache exists to avoid.
        /// </para>
        /// </summary>
        private async Task RestateRunTotalsAsync(PayrollRun run, CancellationToken cancellationToken)
        {
            var lines = await _db.PayrollRunLines
                .Where(l => l.PayrollRunId == run.Id)
                .ToListAsync(cancellationToken);

            run.LineCount = lines.Count;
            run.TotalGross = lines.Sum(l => l.GrossPay);
            run.TotalDeductions = lines.Sum(l => l.DeductionsTotal + l.AdvanceDeduction);
            run.TotalNet = lines.Sum(l => l.NetPay);
        }

        /// <summary>Closes every advance whose schedule this run has just emptied.</summary>
        private async Task SettleExhaustedAdvancesAsync(ICollection<int> advanceIds, CancellationToken cancellationToken)
        {
            if (advanceIds.Count == 0)
            {
                return;
            }

            var advances = await _db.SalaryAdvances
                .Where(a => advanceIds.Contains(a.Id))
                .ToListAsync(cancellationToken);

            // The whole schedule for those advances, tracked — the deductions above are still
            // uncommitted, so a fresh "any still scheduled" query would answer from stale rows.
            var schedule = await _db.SalaryAdvanceInstallments
                .Where(i => advanceIds.Contains(i.SalaryAdvanceId))
                .ToListAsync(cancellationToken);

            foreach (var advance in advances.Where(a => a.Status == SalaryAdvanceStatus.Disbursed))
            {
                var stillOpen = schedule.Any(i =>
                    i.SalaryAdvanceId == advance.Id && i.Status == SalaryAdvanceInstallmentStatus.Scheduled);
                if (stillOpen)
                {
                    continue;
                }

                advance.Status = SalaryAdvanceStatus.Settled;
                advance.SettledAtUtc = _clock.UtcNow;
            }
        }

        private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
