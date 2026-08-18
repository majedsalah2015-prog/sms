using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Domain.Setup;

namespace Sms.Application.Setup
{
    /// <summary>
    /// What the wizard needs to know about the tenant to judge each step —
    /// gathered by SystemSetupAdmin, judged here so the readiness rules are
    /// unit-testable without a database.
    /// </summary>
    public sealed class SetupSnapshot
    {
        public bool SchoolExists { get; init; }

        public bool ProfileComplete { get; init; }

        public bool CountryPackBound { get; init; }

        public bool CurrencyValid { get; init; }

        public bool TimeZoneValid { get; init; }

        public bool WorkingWeekDefined { get; init; }

        public bool LanguagesDefined { get; init; }

        public bool CalendarTypeDefined { get; init; }

        public bool NumberingSeriesDefined { get; init; }

        public bool StageStructureDefined { get; init; }

        public IReadOnlyCollection<SetupChecklist> Checklist { get; init; } = Array.Empty<SetupChecklist>();
    }

    public sealed record StepState(SetupWizardSteps.Step Step, bool IsReady, SetupStepStatus Status);

    /// <summary>
    /// Pure BR-SET-003 rules: a step is *ready* when the data it governs is
    /// in place (server-side validation, BR-GLB-110); it is *completed* when
    /// the operator marked it so — completion requires readiness. Setup may
    /// be declared complete only when every mandatory step is Completed.
    /// </summary>
    public static class SetupWizardEvaluator
    {
        public static bool IsReady(string stepCode, SetupSnapshot s) => stepCode switch
        {
            SetupWizardSteps.Profile => s.SchoolExists && s.ProfileComplete,
            SetupWizardSteps.CountryPack => s.CountryPackBound,
            SetupWizardSteps.Currency => s.CurrencyValid,
            SetupWizardSteps.TimeZone => s.TimeZoneValid,
            SetupWizardSteps.WorkingWeek => s.WorkingWeekDefined,
            SetupWizardSteps.Languages => s.LanguagesDefined,
            SetupWizardSteps.CalendarType => s.CalendarTypeDefined,
            SetupWizardSteps.NumberingSeries => s.NumberingSeriesDefined,
            SetupWizardSteps.StageStructure => s.StageStructureDefined,
            _ => throw new ArgumentException($"Unknown setup step '{stepCode}'.", nameof(stepCode)),
        };

        public static IReadOnlyList<StepState> Evaluate(SetupSnapshot snapshot) =>
            SetupWizardSteps.All
                .Select(step => new StepState(
                    step,
                    IsReady(step.Code, snapshot),
                    snapshot.Checklist.FirstOrDefault(c => string.Equals(c.StepCode, step.Code, StringComparison.OrdinalIgnoreCase))?.Status ?? SetupStepStatus.Pending))
                .ToList();

        public static bool CanDeclareComplete(IEnumerable<StepState> states) =>
            states.Where(s => s.Step.IsMandatory).All(s => s.Status == SetupStepStatus.Completed);

        /// <summary>doc §11 dashboard widget: completed mandatory steps / mandatory steps.</summary>
        public static int CompletionPercent(IEnumerable<StepState> states)
        {
            var mandatory = states.Where(s => s.Step.IsMandatory).ToList();
            return mandatory.Count == 0 ? 100 : (int)Math.Round(100.0 * mandatory.Count(s => s.Status == SetupStepStatus.Completed) / mandatory.Count);
        }
    }
}
