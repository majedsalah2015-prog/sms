using System;
using Sms.Domain.Installments;

namespace Sms.Application.Installments
{
    /// <summary>
    /// Pure BR-INS-007: Scheduled → Due → Overdue (grace elapsed) → Paid /
    /// PartiallyPaid / Rescheduled / Written-off. Derived from the paid
    /// amount (InstallmentPaymentWaterfall) and dates — never stored.
    /// Terminal flags win; then full payment; then partial payment only
    /// counts as a distinct status once the installment is at least Due
    /// (an early partial payment on a future installment is still
    /// Scheduled — nothing is late).
    /// </summary>
    public static class InstallmentStatusDeriver
    {
        public static InstallmentStatus Derive(decimal amount, decimal paid, DateTime dueDate, int graceDays, DateTime today, bool isSuperseded, bool isWrittenOff)
        {
            if (isSuperseded)
            {
                return InstallmentStatus.Rescheduled;
            }

            if (isWrittenOff)
            {
                return InstallmentStatus.WrittenOff;
            }

            if (paid >= amount)
            {
                return InstallmentStatus.Paid;
            }

            var day = today.Date;
            if (day < dueDate.Date)
            {
                return InstallmentStatus.Scheduled;
            }

            if (paid > 0m)
            {
                return InstallmentStatus.PartiallyPaid;
            }

            return day <= dueDate.Date.AddDays(graceDays) ? InstallmentStatus.Due : InstallmentStatus.Overdue;
        }

        /// <summary>BR-INS-008/009: "truly overdue" — the only condition dunning notices may fire on. PartiallyPaid past grace counts.</summary>
        public static bool IsTrulyOverdue(decimal amount, decimal paid, DateTime dueDate, int graceDays, DateTime today, bool isSuperseded, bool isWrittenOff)
        {
            if (isSuperseded || isWrittenOff || paid >= amount)
            {
                return false;
            }

            return today.Date > dueDate.Date.AddDays(graceDays);
        }
    }
}
