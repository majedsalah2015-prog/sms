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

        /// <summary>
        /// Where a save on <paramref name="currentStepCode"/> goes — the step after it in ordinal
        /// order, or <c>null</c> when it is the last one and the wizard's index is the destination.
        /// <para>
        /// It used to be "the first step still incomplete, anywhere in the list", which is a
        /// different question and gave two wrong answers to this one: saving step 7 while step 2
        /// was still open threw the operator back to step 2, and on a setup already complete —
        /// every step green — there was no incomplete step to find, so every save landed on the
        /// index and the wizard could not be walked through at all.
        /// </para>
        /// <para>
        /// Ordinal, not readiness-based, on purpose. A wizard that silently reorders itself around
        /// what you have not filled in yet is one whose "next" button cannot be predicted, and the
        /// steps here are ordered by dependency already: currency and time zone need the school
        /// profile to exist, so walking forward is also walking in dependency order.
        /// </para>
        /// </summary>
        public static SetupWizardSteps.Step? NextStep(string currentStepCode)
        {
            if (!SetupWizardSteps.TryGet(currentStepCode, out var current))
            {
                throw new ArgumentException($"Unknown setup step '{currentStepCode}'.", nameof(currentStepCode));
            }

            return SetupWizardSteps.All.OrderBy(s => s.Order).FirstOrDefault(s => s.Order > current.Order);
        }

        /// <summary>The step before this one, so the wizard can be walked backwards. Null on the first.</summary>
        public static SetupWizardSteps.Step? PreviousStep(string currentStepCode)
        {
            if (!SetupWizardSteps.TryGet(currentStepCode, out var current))
            {
                throw new ArgumentException($"Unknown setup step '{currentStepCode}'.", nameof(currentStepCode));
            }

            return SetupWizardSteps.All.OrderByDescending(s => s.Order).FirstOrDefault(s => s.Order < current.Order);
        }

        /// <summary>
        /// Where the wizard's index sends someone who just wants to get on with it: the first step
        /// not yet completed, or <c>null</c> when there is nothing left to do. This *is* the
        /// "first incomplete" question — it just is not the one a save is asking.
        /// </summary>
        public static SetupWizardSteps.Step? ResumeAt(IEnumerable<StepState> states) =>
            states.OrderBy(s => s.Step.Order).FirstOrDefault(s => s.Status != SetupStepStatus.Completed)?.Step;

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
