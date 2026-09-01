using Sms.Application.Common.Exceptions;

namespace Sms.Application.Payroll
{
    /// <summary>
    /// Month arithmetic for payroll periods (owner request, 2026-08-28 — see
    /// <c>Sms.Domain.Payroll.PayrollRun</c> for the standing deviation from doc/Modules/12 §2).
    /// <para>
    /// A payroll period is a Gregorian (year, month) pair rather than a <see cref="System.DateTime"/>,
    /// because "the month of March" is not a day and storing it as one invites every timezone and
    /// day-of-month bug this product has already paid for elsewhere. That leaves the arithmetic —
    /// "which month is four instalments after this one" — with nowhere natural to live, so it
    /// lives here, once, instead of being re-derived in the scheduler and the run generator.
    /// </para>
    /// </summary>
    public static class PayrollPeriodMath
    {
        /// <summary>Below this a "year" is a typo, not a payroll period.</summary>
        public const int MinYear = 2000;

        /// <summary>Far enough out that no school reaches it, close enough that a fat-fingered 20260 is refused.</summary>
        public const int MaxYear = 2200;

        public static bool IsValid(int year, int month) =>
            year >= MinYear && year <= MaxYear && month >= 1 && month <= 12;

        /// <summary>Throws <see cref="InvalidPayrollPeriodException"/> when the pair is not a real month in range.</summary>
        public static void EnsureValid(int year, int month)
        {
            if (!IsValid(year, month))
            {
                throw new InvalidPayrollPeriodException(year, month);
            }
        }

        /// <summary>
        /// Months since year zero — the single number that makes "is this period before that one"
        /// a comparison rather than a nested conditional.
        /// </summary>
        public static int Ordinal(int year, int month) => (year * 12) + (month - 1);

        /// <summary>Negative when the left period is earlier, 0 when equal, positive when later.</summary>
        public static int Compare(int leftYear, int leftMonth, int rightYear, int rightMonth) =>
            Ordinal(leftYear, leftMonth) - Ordinal(rightYear, rightMonth);

        /// <summary>Walks <paramref name="monthsToAdd"/> months forward (or back, when negative) from a valid period.</summary>
        public static (int Year, int Month) AddMonths(int year, int month, int monthsToAdd)
        {
            EnsureValid(year, month);

            var ordinal = Ordinal(year, month) + monthsToAdd;
            var resultYear = ordinal / 12;
            var resultMonth = (ordinal % 12) + 1;

            EnsureValid(resultYear, resultMonth);
            return (resultYear, resultMonth);
        }
    }
}
