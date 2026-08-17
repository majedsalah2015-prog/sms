using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Domain.Installments;

namespace Sms.Application.Installments
{
    /// <summary>
    /// Pure BR-INS-008/009/006: which single ladder step (if any) an
    /// installment should fire today. Offsets are days relative to the
    /// due date: reminders D-7/D-1 (per BR-NOT catalog), overdue notices
    /// +3/+14/+30 (the doc's proposed timings — its own open question Q1
    /// asks each market to confirm), then the human-gated flag stages
    /// (portal banner +45, statement letter +60, escalation +90 —
    /// PROPOSED defaults, the doc gives none). One step per run: the
    /// latest eligible step not yet reached, so a first run at +20 fires
    /// Overdue14 once rather than a backlog of three notices. Rules:
    /// nothing fires on a paid/superseded/written-off installment; overdue
    /// steps fire only on truly-overdue status; PDC coverage suppresses
    /// the whole ladder; a broken promise advances to the next unfired
    /// step immediately regardless of its offset.
    /// </summary>
    public static class DunningLadderEvaluator
    {
        public static readonly IReadOnlyDictionary<DunningStep, int> ProposedOffsetsDays = new Dictionary<DunningStep, int>
        {
            [DunningStep.ReminderD7] = -7,
            [DunningStep.ReminderD1] = -1,
            [DunningStep.Overdue3] = 3,
            [DunningStep.Overdue14] = 14,
            [DunningStep.Overdue30] = 30,
            [DunningStep.PortalBanner] = 45,
            [DunningStep.StatementLetter] = 60,
            [DunningStep.Escalation] = 90,
        };

        public static DunningStep? Next(
            DateTime dueDate, DateTime today, InstallmentStatus status, bool isTrulyOverdue, bool isPdcCovered,
            IReadOnlyCollection<DunningStep> firedSteps, bool hasBrokenPromise)
        {
            if (status is InstallmentStatus.Paid or InstallmentStatus.Rescheduled or InstallmentStatus.WrittenOff)
            {
                return null;
            }

            if (isPdcCovered)
            {
                return null;
            }

            var highestFired = firedSteps.Count == 0 ? (DunningStep?)null : firedSteps.Max();

            if (hasBrokenPromise && isTrulyOverdue)
            {
                var floor = highestFired.HasValue ? highestFired.Value : DunningStep.ReminderD1;
                var next = ProposedOffsetsDays.Keys.Where(s => s > floor && s >= DunningStep.Overdue3).OrderBy(s => s).Cast<DunningStep?>().FirstOrDefault();
                return next;
            }

            var offset = (today.Date - dueDate.Date).Days;
            var eligible = ProposedOffsetsDays
                .Where(kv => offset >= kv.Value)
                .Where(kv => kv.Key < DunningStep.Overdue3 || isTrulyOverdue)
                .Select(kv => kv.Key)
                .Where(s => !highestFired.HasValue || s > highestFired.Value)
                .ToList();

            if (eligible.Count == 0)
            {
                return null;
            }

            // Reminders only make sense before the due date — once overdue, jump straight to the overdue rung.
            if (offset >= 0 && eligible.All(s => s < DunningStep.Overdue3))
            {
                return null;
            }

            return eligible.Max();
        }
    }
}
