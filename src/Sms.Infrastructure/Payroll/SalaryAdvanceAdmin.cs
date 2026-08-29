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
using Sms.Domain.Payroll;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Payroll
{
    /// <summary>
    /// سلف الموظفين (owner request, 2026-08-28). Standalone shape — each method saves itself.
    /// The ADV number rides <c>INumberIssuer</c>, which never saves, so the number materialises
    /// only with the advance it stamps (BR-NUM-003).
    /// <para>
    /// See <c>Sms.Domain.Payroll.SalaryAdvance</c> for the stated deviation: doc/Modules/12 does
    /// not describe staff advances at all.
    /// </para>
    /// </summary>
    public class SalaryAdvanceAdmin : ISalaryAdvanceAdmin
    {
        private readonly AppDbContext _db;
        private readonly INumberIssuer _numberIssuer;
        private readonly IClock _clock;

        public SalaryAdvanceAdmin(AppDbContext db, INumberIssuer numberIssuer, IClock clock)
        {
            _db = db;
            _numberIssuer = numberIssuer;
            _clock = clock;
        }

        public async Task<SalaryAdvance> RequestAsync(
            int employeeId, DateTime requestDate, decimal amount, int installmentCount,
            int firstDeductionYear, int firstDeductionMonth, string? reason = null,
            CancellationToken cancellationToken = default)
        {
            AdvanceInstallmentScheduler.EnsureSchedulable(amount, installmentCount, firstDeductionYear, firstDeductionMonth);
            await EnsureNoOutstandingAdvanceAsync(employeeId, null, cancellationToken);

            var advance = new SalaryAdvance
            {
                EmployeeId = employeeId,
                AdvanceNo = await _numberIssuer.IssueAsync("ADV", cancellationToken),
                RequestDate = requestDate,
                Amount = amount,
                InstallmentCount = installmentCount,
                FirstDeductionYear = firstDeductionYear,
                FirstDeductionMonth = firstDeductionMonth,
                Reason = Blank(reason),
                Status = SalaryAdvanceStatus.Requested,
            };

            _db.SalaryAdvances.Add(advance);
            await _db.SaveChangesAsync(cancellationToken);
            return advance;
        }

        public async Task<SalaryAdvance> UpdateRequestAsync(
            int advanceId, DateTime requestDate, decimal amount, int installmentCount,
            int firstDeductionYear, int firstDeductionMonth, string? reason,
            CancellationToken cancellationToken = default)
        {
            var advance = await LoadAsync(advanceId, cancellationToken);
            if (advance.Status != SalaryAdvanceStatus.Requested)
            {
                throw new InvalidSalaryAdvanceStatusTransitionException(advance.Status, SalaryAdvanceStatus.Requested);
            }

            AdvanceInstallmentScheduler.EnsureSchedulable(amount, installmentCount, firstDeductionYear, firstDeductionMonth);

            advance.RequestDate = requestDate;
            advance.Amount = amount;
            advance.InstallmentCount = installmentCount;
            advance.FirstDeductionYear = firstDeductionYear;
            advance.FirstDeductionMonth = firstDeductionMonth;
            advance.Reason = Blank(reason);

            await _db.SaveChangesAsync(cancellationToken);
            return advance;
        }

        public Task<SalaryAdvance> ApproveAsync(int advanceId, string? note = null, CancellationToken cancellationToken = default) =>
            DecideAsync(advanceId, SalaryAdvanceStatus.Approved, note, cancellationToken);

        public Task<SalaryAdvance> RejectAsync(int advanceId, string? note = null, CancellationToken cancellationToken = default) =>
            DecideAsync(advanceId, SalaryAdvanceStatus.Rejected, note, cancellationToken);

        public Task<SalaryAdvance> CancelAsync(int advanceId, string? note = null, CancellationToken cancellationToken = default) =>
            DecideAsync(advanceId, SalaryAdvanceStatus.Cancelled, note, cancellationToken);

        public async Task<SalaryAdvance> DisburseAsync(
            int advanceId, DateTime disbursedOn, AdvanceDisbursementMethod method,
            string? referenceNo = null, CancellationToken cancellationToken = default)
        {
            var advance = await LoadAsync(advanceId, cancellationToken);
            EnsureTransition(advance.Status, SalaryAdvanceStatus.Disbursed);

            advance.Status = SalaryAdvanceStatus.Disbursed;
            advance.DisbursedOn = disbursedOn;
            advance.DisbursementMethod = method;
            advance.DisbursementRefNo = Blank(referenceNo);

            // The whole schedule at once, so the employee leaves the counter knowing every
            // instalment rather than discovering them a month at a time.
            foreach (var scheduled in AdvanceInstallmentScheduler.Build(
                advance.Amount, advance.InstallmentCount, advance.FirstDeductionYear, advance.FirstDeductionMonth))
            {
                _db.SalaryAdvanceInstallments.Add(new SalaryAdvanceInstallment
                {
                    SalaryAdvanceId = advance.Id,
                    SequenceNo = scheduled.SequenceNo,
                    DueYear = scheduled.DueYear,
                    DueMonth = scheduled.DueMonth,
                    Amount = scheduled.Amount,
                    Status = SalaryAdvanceInstallmentStatus.Scheduled,
                });
            }

            await _db.SaveChangesAsync(cancellationToken);
            return advance;
        }

        public async Task WaiveInstallmentAsync(int installmentId, string? note = null, CancellationToken cancellationToken = default)
        {
            var target = await _db.SalaryAdvanceInstallments
                .SingleAsync(i => i.Id == installmentId, cancellationToken);

            if (target.Status != SalaryAdvanceInstallmentStatus.Scheduled)
            {
                throw new InstallmentNotWaivableException(installmentId, target.Status);
            }

            await EnsurePeriodNotLockedAsync(target, cancellationToken);

            // The whole schedule, so settlement is decided from the change tracker rather than from
            // the database — the waiver below is not committed yet, and a query would still see the
            // instalment as scheduled and leave the advance open forever.
            var schedule = await ScheduleAsync(target.SalaryAdvanceId, cancellationToken);

            target.Status = SalaryAdvanceInstallmentStatus.Waived;
            target.WaiverNote = Blank(note);

            await SettleIfExhaustedAsync(target.SalaryAdvanceId, schedule, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<SalaryAdvance> WaiveRemainingAsync(int advanceId, string? note = null, CancellationToken cancellationToken = default)
        {
            var advance = await LoadAsync(advanceId, cancellationToken);
            if (advance.Status != SalaryAdvanceStatus.Disbursed)
            {
                throw new InvalidSalaryAdvanceStatusTransitionException(advance.Status, SalaryAdvanceStatus.Settled);
            }

            var schedule = await ScheduleAsync(advanceId, cancellationToken);
            foreach (var installment in schedule.Where(i => i.Status == SalaryAdvanceInstallmentStatus.Scheduled))
            {
                await EnsurePeriodNotLockedAsync(installment, cancellationToken);
                installment.Status = SalaryAdvanceInstallmentStatus.Waived;
                installment.WaiverNote = Blank(note);
            }

            await SettleIfExhaustedAsync(advanceId, schedule, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return advance;
        }

        public async Task<SalaryAdvance> RescheduleAsync(
            int advanceId, int installmentCount, int firstDeductionYear, int firstDeductionMonth,
            CancellationToken cancellationToken = default)
        {
            var advance = await LoadAsync(advanceId, cancellationToken);
            if (advance.Status != SalaryAdvanceStatus.Disbursed)
            {
                throw new InvalidSalaryAdvanceStatusTransitionException(advance.Status, SalaryAdvanceStatus.Disbursed);
            }

            var schedule = await ScheduleAsync(advanceId, cancellationToken);
            var scheduled = schedule.Where(i => i.Status == SalaryAdvanceInstallmentStatus.Scheduled).ToList();
            foreach (var installment in scheduled)
            {
                await EnsurePeriodNotLockedAsync(installment, cancellationToken);
            }

            // What is left to recover — anything a paid run already took, or the school forgave,
            // stays exactly as it was. Only the unexecuted plan is replaced. Summed in memory
            // because SumAsync() over a decimal column throws at runtime on Sqlite.
            var executed = schedule.Where(i => i.Status != SalaryAdvanceInstallmentStatus.Scheduled).ToList();
            var remaining = advance.Amount - executed.Sum(i => i.Amount);

            AdvanceInstallmentScheduler.EnsureSchedulable(remaining, installmentCount, firstDeductionYear, firstDeductionMonth);

            // Two saves, so one transaction around both. The delete has to reach the database
            // before the replacements are added — the old rows and the new ones share
            // (SchoolId, SalaryAdvanceId, SequenceNo), and EF gives no ordering guarantee between a
            // delete and an insert on one table inside a single batch, so doing both at once trips
            // the unique index whenever the new schedule reuses a sequence number, which is almost
            // always. Without the transaction, a reschedule that failed on the second save (a
            // missing audit reason is the likely one) would leave the advance with its old plan
            // deleted and no new one: money owed and nothing scheduled to recover it.
            using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            // A physical delete of rows that record nothing that happened: an unexecuted schedule
            // line is a plan, not history, so BR-GLB-005's no-delete rule does not reach it. The
            // instalments that DID happen are untouched above.
            _db.SalaryAdvanceInstallments.RemoveRange(scheduled);
            await _db.SaveChangesAsync(cancellationToken);

            // New rows continue the numbering rather than restarting it, so a payslip that cites
            // "instalment 3 of 6" still points at the row it deducted after a reschedule.
            var nextSequence = executed.Count == 0 ? 0 : executed.Max(i => i.SequenceNo);

            foreach (var slot in AdvanceInstallmentScheduler.Build(remaining, installmentCount, firstDeductionYear, firstDeductionMonth))
            {
                _db.SalaryAdvanceInstallments.Add(new SalaryAdvanceInstallment
                {
                    SalaryAdvanceId = advanceId,
                    SequenceNo = nextSequence + slot.SequenceNo,
                    DueYear = slot.DueYear,
                    DueMonth = slot.DueMonth,
                    Amount = slot.Amount,
                    Status = SalaryAdvanceInstallmentStatus.Scheduled,
                });
            }

            advance.InstallmentCount = nextSequence + installmentCount;
            advance.FirstDeductionYear = firstDeductionYear;
            advance.FirstDeductionMonth = firstDeductionMonth;

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return advance;
        }

        // ------------------------------------------------------------------ internals

        private async Task<SalaryAdvance> DecideAsync(
            int advanceId, SalaryAdvanceStatus newStatus, string? note, CancellationToken cancellationToken)
        {
            var advance = await LoadAsync(advanceId, cancellationToken);
            EnsureTransition(advance.Status, newStatus);

            advance.Status = newStatus;
            advance.DecisionAtUtc = _clock.UtcNow;
            advance.DecisionNote = Blank(note);

            await _db.SaveChangesAsync(cancellationToken);
            return advance;
        }

        private Task<SalaryAdvance> LoadAsync(int advanceId, CancellationToken cancellationToken) =>
            _db.SalaryAdvances.SingleAsync(a => a.Id == advanceId, cancellationToken);

        private static void EnsureTransition(SalaryAdvanceStatus from, SalaryAdvanceStatus to)
        {
            if (!SalaryAdvanceStatusTransitions.CanTransition(from, to))
            {
                throw new InvalidSalaryAdvanceStatusTransitionException(from, to);
            }
        }

        /// <summary>
        /// One advance at a time. The status filter runs in memory over one employee's handful of
        /// rows so the rule stays in <c>SalaryAdvanceStatusTransitions</c> rather than being
        /// restated as an EF-translatable predicate that could drift from it.
        /// </summary>
        private async Task EnsureNoOutstandingAdvanceAsync(int employeeId, int? excludingAdvanceId, CancellationToken cancellationToken)
        {
            var existing = await _db.SalaryAdvances
                .Where(a => a.EmployeeId == employeeId && a.Id != (excludingAdvanceId ?? 0))
                .Select(a => new { a.Id, a.AdvanceNo, a.Status })
                .ToListAsync(cancellationToken);

            var outstanding = existing.FirstOrDefault(a => SalaryAdvanceStatusTransitions.IsOutstanding(a.Status));
            if (outstanding != null)
            {
                throw new OutstandingAdvanceException(employeeId, outstanding.AdvanceNo);
            }
        }

        /// <summary>Every instalment of one advance, tracked, in due order — a handful of rows.</summary>
        private Task<List<SalaryAdvanceInstallment>> ScheduleAsync(int advanceId, CancellationToken cancellationToken) =>
            _db.SalaryAdvanceInstallments
                .Where(i => i.SalaryAdvanceId == advanceId)
                .OrderBy(i => i.SequenceNo)
                .ToListAsync(cancellationToken);

        /// <summary>
        /// Closes the advance once nothing is left scheduled.
        /// <para>
        /// Decided from the caller's in-memory schedule, never from a fresh query: the waiver or
        /// deduction that emptied the schedule is still uncommitted when this runs, and the
        /// database would report it as outstanding.
        /// </para>
        /// </summary>
        private async Task SettleIfExhaustedAsync(
            int advanceId, IReadOnlyCollection<SalaryAdvanceInstallment> schedule, CancellationToken cancellationToken)
        {
            var advance = await LoadAsync(advanceId, cancellationToken);
            if (advance.Status != SalaryAdvanceStatus.Disbursed)
            {
                return;
            }

            if (schedule.Any(i => i.Status == SalaryAdvanceInstallmentStatus.Scheduled))
            {
                return;
            }

            advance.Status = SalaryAdvanceStatus.Settled;
            advance.SettledAtUtc = _clock.UtcNow;
        }

        /// <summary>
        /// Refuses to touch an instalment whose month has already been signed off. See
        /// <see cref="InstallmentLockedByPayrollRunException"/> for why a waiver after approval
        /// would put the payslip and the advances statement out of step.
        /// </summary>
        private async Task EnsurePeriodNotLockedAsync(SalaryAdvanceInstallment installment, CancellationToken cancellationToken)
        {
            var run = await _db.PayrollRuns
                .Where(r => r.PeriodYear == installment.DueYear
                            && r.PeriodMonth == installment.DueMonth
                            && (r.Status == PayrollRunStatus.Approved || r.Status == PayrollRunStatus.Paid))
                .Select(r => new { r.PayrollRunNo, r.Status })
                .FirstOrDefaultAsync(cancellationToken);

            if (run != null)
            {
                throw new InstallmentLockedByPayrollRunException(installment.Id, run.PayrollRunNo, run.Status);
            }
        }

        private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
