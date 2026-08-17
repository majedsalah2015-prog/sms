using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Sms.Application.Health
{
    /// <summary>Pure BR-HLT-004: due/overdue per student age against the pack schedule; a recorded dose satisfies its entry.</summary>
    public static class VaccinationDueEvaluator
    {
        public enum DoseState : short
        {
            Given = 1,
            NotYetDue = 2,
            Due = 3,
            Overdue = 4,
        }

        public sealed record ScheduleEntry(string VaccineCode, int DoseNumber, int DueAgeMonths);

        public sealed record GivenDose(string VaccineCode, int DoseNumber);

        public sealed record DoseStatus(string VaccineCode, int DoseNumber, DateTime DueDate, DoseState State);

        /// <summary>Overdue = more than <paramref name="graceDays"/> past the due date without a record.</summary>
        public static IReadOnlyList<DoseStatus> Evaluate(DateTime dateOfBirth, DateTime asOf, IReadOnlyCollection<ScheduleEntry> schedule, IReadOnlyCollection<GivenDose> given, int graceDays = 30)
        {
            var givenSet = new HashSet<(string, int)>(given.Select(g => (g.VaccineCode, g.DoseNumber)));
            return schedule
                .OrderBy(s => s.DueAgeMonths).ThenBy(s => s.VaccineCode).ThenBy(s => s.DoseNumber)
                .Select(s =>
                {
                    var due = dateOfBirth.Date.AddMonths(s.DueAgeMonths);
                    var state = givenSet.Contains((s.VaccineCode, s.DoseNumber)) ? DoseState.Given
                        : asOf.Date < due ? DoseState.NotYetDue
                        : asOf.Date <= due.AddDays(graceDays) ? DoseState.Due
                        : DoseState.Overdue;
                    return new DoseStatus(s.VaccineCode, s.DoseNumber, due, state);
                })
                .ToList();
        }
    }

    /// <summary>Pure BR-HLT-006 / doc §9: administration only within the authorization's date window, dosage and schedule; anything else is a deviation needing a reason.</summary>
    public static class MedicationAdministrationPolicy
    {
        public static bool IsWithinWindow(DateTime atLocal, DateTime startDate, DateTime endDate) => atLocal.Date >= startDate.Date && atLocal.Date <= endDate.Date;

        public static bool IsScheduledTime(TimeSpan atTimeOfDay, string scheduleTimes, int toleranceMinutes = 30)
        {
            var times = ParseScheduleTimes(scheduleTimes);
            return times.Any(t => Math.Abs((atTimeOfDay - t).TotalMinutes) <= toleranceMinutes);
        }

        public static bool IsAuthorizedDose(decimal doseGiven, decimal authorizedDose) => doseGiven == authorizedDose;

        public static bool IsDeviation(DateTime atLocal, DateTime startDate, DateTime endDate, string scheduleTimes, decimal doseGiven, decimal authorizedDose, int toleranceMinutes = 30)
            => !IsWithinWindow(atLocal, startDate, endDate) || !IsScheduledTime(atLocal.TimeOfDay, scheduleTimes, toleranceMinutes) || !IsAuthorizedDose(doseGiven, authorizedDose);

        public static IReadOnlyList<TimeSpan> ParseScheduleTimes(string scheduleTimes)
            => scheduleTimes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => TimeSpan.ParseExact(s, "hh\\:mm", CultureInfo.InvariantCulture))
                .ToList();
    }

    /// <summary>Pure BR-HLT-005: sent-home needs a verified pickup OR a documented exception.</summary>
    public static class SentHomePolicy
    {
        public static bool IsAcceptable(bool pickupVerified, string? exceptionNote) => pickupVerified || !string.IsNullOrWhiteSpace(exceptionNote);
    }

    /// <summary>Pure BR-HLT-008: anonymized aggregate — counts only, no identities.</summary>
    public static class ScreeningStatsCalculator
    {
        public sealed record Stats(int Screened, int Abnormal, int Referred, int FollowedUp)
        {
            public decimal AbnormalRate => Screened == 0 ? 0m : Math.Round((decimal)Abnormal / Screened * 100m, 1);
        }

        public static Stats Compute(IReadOnlyCollection<(bool IsAbnormal, bool Referred, bool FollowedUp)> results)
            => new(results.Count, results.Count(r => r.IsAbnormal), results.Count(r => r.Referred), results.Count(r => r.FollowedUp));
    }
}
