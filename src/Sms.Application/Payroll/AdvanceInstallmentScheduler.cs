using System;
using System.Collections.Generic;
using Sms.Application.Common.Exceptions;

namespace Sms.Application.Payroll
{
    /// <summary>
    /// Splits a disbursed advance into the monthly instalments the payroll runs will recover
    /// (owner request, 2026-08-28 — see <c>Sms.Domain.Payroll.PayrollRun</c> for the deviation).
    /// <para>
    /// Pure, and deliberately the only place the split is expressed: an employee is told the whole
    /// schedule at the counter, the payroll run reads it back a month at a time, and the advances
    /// statement adds it up. Three readers of one arithmetic is exactly the shape that drifts when
    /// each computes its own.
    /// </para>
    /// </summary>
    public static class AdvanceInstallmentScheduler
    {
        /// <summary>
        /// Five years. Not a legal limit — a ceiling that keeps a mistyped instalment count from
        /// writing a schedule the school will still be carrying when the employee has left.
        /// </summary>
        public const int MaxInstallments = 60;

        /// <summary>One row of the repayment schedule, before it becomes a persisted instalment.</summary>
        public sealed record ScheduledInstallment(int SequenceNo, int DueYear, int DueMonth, decimal Amount);

        /// <summary>
        /// Builds the schedule. Every instalment is the same amount except the last, which carries
        /// the rounding remainder so the instalments sum to the advance <b>exactly</b> — a schedule
        /// that recovers 999.99 of a 1,000 advance leaves a balance nobody can clear.
        /// <para>
        /// Throws <see cref="InvalidAdvanceAmountException"/> for a non-positive amount,
        /// <see cref="InvalidAdvanceInstallmentCountException"/> when the count is outside
        /// 1..<see cref="MaxInstallments"/> or is so large that an instalment would round to
        /// nothing, and <see cref="InvalidPayrollPeriodException"/> when the first deduction month
        /// is not a real month.
        /// </para>
        /// </summary>
        public static IReadOnlyList<ScheduledInstallment> Build(
            decimal amount, int installmentCount, int firstDeductionYear, int firstDeductionMonth)
        {
            EnsureSchedulable(amount, installmentCount, firstDeductionYear, firstDeductionMonth);

            // Floor rather than round: rounding up on every instalment can push the running total
            // past the advance, which would then need a negative last instalment to correct.
            var perInstallment = Math.Floor(amount / installmentCount * 100m) / 100m;

            var schedule = new List<ScheduledInstallment>(installmentCount);
            var allocated = 0m;

            for (var sequence = 1; sequence <= installmentCount; sequence++)
            {
                var isLast = sequence == installmentCount;
                var instalmentAmount = isLast ? amount - allocated : perInstallment;
                allocated += instalmentAmount;

                var (year, month) = PayrollPeriodMath.AddMonths(firstDeductionYear, firstDeductionMonth, sequence - 1);
                schedule.Add(new ScheduledInstallment(sequence, year, month, instalmentAmount));
            }

            return schedule;
        }

        /// <summary>
        /// The same refusals <see cref="Build"/> makes, without building anything — so a request
        /// screen can reject an impossible advance at the moment it is typed, months before a
        /// schedule exists to build. One set of rules with two callers, rather than two sets that
        /// drift.
        /// </summary>
        public static void EnsureSchedulable(
            decimal amount, int installmentCount, int firstDeductionYear, int firstDeductionMonth)
        {
            if (amount <= 0m)
            {
                throw new InvalidAdvanceAmountException(amount);
            }

            if (installmentCount < 1 || installmentCount > MaxInstallments)
            {
                throw new InvalidAdvanceInstallmentCountException(installmentCount, MaxInstallments);
            }

            // An instalment that rounds to zero is a row the payroll run would carry, the statement
            // would print, and nobody could explain. Refuse the split instead of emitting one.
            if (amount < installmentCount * 0.01m)
            {
                throw new InvalidAdvanceInstallmentCountException(installmentCount, MaxInstallments);
            }

            PayrollPeriodMath.EnsureValid(firstDeductionYear, firstDeductionMonth);
        }
    }
}
