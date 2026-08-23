using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.Setup;
using Sms.Domain.Setup;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Setup
{
    /// <summary>E-101 pure engines: working week (doc/Modules/01 §9), setting resolution (BR-SET-005), feature dependencies (BR-SET-006), wizard readiness (BR-SET-003).</summary>
    public class SetupEnginesTests
    {
        // --- WorkingWeek -----------------------------------------------------------

        [Theory]
        [InlineData("Sunday,Monday,Tuesday,Wednesday,Thursday")]
        [InlineData("monday, tuesday, wednesday, thursday")]
        [BusinessRule("BR-SET-005")]
        public void Working_week_with_four_or_more_days_is_valid(string value)
        {
            Assert.Null(WorkingWeek.Validate(value));
        }

        [Theory]
        [InlineData("")]
        [InlineData("Sunday,Monday,Tuesday")]
        [InlineData("Sunday,Sunday,Sunday,Sunday")]
        [InlineData("Sunday,Monday,Tuesday,Funday")]
        [BusinessRule("BR-SET-005")]
        public void Working_week_below_four_distinct_days_or_with_bad_names_is_rejected(string value)
        {
            Assert.NotNull(WorkingWeek.Validate(value));
        }

        [Fact]
        [BusinessRule("BR-SET-005")]
        public void Weekend_days_are_the_complement_of_the_working_days()
        {
            var weekend = WorkingWeek.WeekendDays("Sunday,Monday,Tuesday,Wednesday,Thursday");
            Assert.Equal(new[] { DayOfWeek.Friday, DayOfWeek.Saturday }, weekend);
        }

        // --- SettingKeys catalog validators ------------------------------------------

        [Theory]
        [InlineData(SettingKeys.VatRate, "0.15", true)]
        [InlineData(SettingKeys.VatRate, "15", false)]
        [InlineData(SettingKeys.CalendarType, "Both", true)]
        [InlineData(SettingKeys.CalendarType, "Lunar", false)]
        [InlineData(SettingKeys.EnabledLanguages, "ar,en", true)]
        [InlineData(SettingKeys.EnabledLanguages, "ar,fr", false)]
        [InlineData(SettingKeys.HijriDisplay, "true", true)]
        [InlineData(SettingKeys.HijriDisplay, "yes", false)]
        [BusinessRule("BR-GLB-110")]
        public void Setting_values_validate_server_side(string key, string value, bool valid)
        {
            Assert.True(SettingKeys.TryGet(key, out var definition));
            Assert.Equal(valid, definition.Validate(value) == null);
        }

        [Fact]
        [BusinessRule("BR-SET-005")]
        public void Only_year_versionable_keys_are_flagged_as_such()
        {
            Assert.True(SettingKeys.TryGet(SettingKeys.VatRate, out var vat) && vat.YearVersionable);
            Assert.True(SettingKeys.TryGet(SettingKeys.WorkingDays, out var week) && week.YearVersionable);
            Assert.True(SettingKeys.TryGet(SettingKeys.DefaultLanguage, out var lang) && !lang.YearVersionable);
        }

        // --- SettingResolver -----------------------------------------------------------

        [Fact]
        [BusinessRule("BR-SET-005")]
        public void Year_row_wins_over_school_default_and_default_backs_other_years()
        {
            var rows = new[]
            {
                new SchoolSetting { Key = SettingKeys.VatRate, Value = "0.05" },
                new SchoolSetting { Key = SettingKeys.VatRate, Value = "0.15", AcademicYearId = 2020 },
            };

            Assert.Equal("0.15", SettingResolver.Resolve(rows, 2020)!.Value);
            Assert.Equal("0.05", SettingResolver.Resolve(rows, 2019)!.Value);
            Assert.Equal("0.05", SettingResolver.Resolve(rows, null)!.Value);
            Assert.Null(SettingResolver.Resolve(Array.Empty<SchoolSetting>(), 2020));
        }

        // --- FeatureDependencyEvaluator ------------------------------------------------

        [Fact]
        [BusinessRule("BR-SET-006")]
        public void Enabling_a_dependent_feature_requires_its_dependency_on()
        {
            var states = new Dictionary<string, bool> { [FeatureCatalog.Transport] = false };
            Assert.Equal(new[] { FeatureCatalog.Transport }, FeatureDependencyEvaluator.Blockers(FeatureCatalog.TransportFees, enable: true, states));

            states[FeatureCatalog.Transport] = true;
            Assert.Empty(FeatureDependencyEvaluator.Blockers(FeatureCatalog.TransportFees, enable: true, states));
        }

        [Fact]
        [BusinessRule("BR-SET-006")]
        public void Disabling_a_dependency_is_blocked_while_dependents_are_on()
        {
            var states = new Dictionary<string, bool>(); // catalog defaults: both on
            Assert.Equal(new[] { FeatureCatalog.TransportFees }, FeatureDependencyEvaluator.Blockers(FeatureCatalog.Transport, enable: false, states));

            states[FeatureCatalog.TransportFees] = false;
            Assert.Empty(FeatureDependencyEvaluator.Blockers(FeatureCatalog.Transport, enable: false, states));
        }

        [Fact]
        [BusinessRule("BR-SET-006")]
        public void Absent_toggle_falls_back_to_the_catalog_default()
        {
            var none = new Dictionary<string, bool>();
            Assert.True(FeatureDependencyEvaluator.IsOn(FeatureCatalog.Cafeteria, none));
            Assert.False(FeatureDependencyEvaluator.IsOn(FeatureCatalog.StudentAccounts, none));
        }

        // --- SetupWizardEvaluator --------------------------------------------------------

        private static SetupSnapshot AllReady(IReadOnlyCollection<SetupChecklist>? checklist = null) => new()
        {
            SchoolExists = true, ProfileComplete = true, CountryPackBound = true, CurrencyValid = true, TimeZoneValid = true,
            WorkingWeekDefined = true, LanguagesDefined = true, CalendarTypeDefined = true, NumberingSeriesDefined = true, StageStructureDefined = true,
            Checklist = checklist ?? Array.Empty<SetupChecklist>(),
        };

        [Fact]
        [BusinessRule("BR-SET-003")]
        public void Every_mandatory_step_must_be_completed_before_setup_can_be_declared_complete()
        {
            var partial = SetupWizardSteps.All.Take(8).Select(s => new SetupChecklist { StepCode = s.Code, Status = SetupStepStatus.Completed }).ToList();
            var states = SetupWizardEvaluator.Evaluate(AllReady(partial));

            Assert.False(SetupWizardEvaluator.CanDeclareComplete(states));
            Assert.Equal(89, SetupWizardEvaluator.CompletionPercent(states));

            partial.Add(new SetupChecklist { StepCode = SetupWizardSteps.StageStructure, Status = SetupStepStatus.Completed });
            states = SetupWizardEvaluator.Evaluate(AllReady(partial));
            Assert.True(SetupWizardEvaluator.CanDeclareComplete(states));
            Assert.Equal(100, SetupWizardEvaluator.CompletionPercent(states));
        }

        [Fact]
        [BusinessRule("BR-SET-003")]
        public void Step_readiness_reflects_the_data_it_governs()
        {
            var snapshot = new SetupSnapshot { SchoolExists = true, ProfileComplete = true, CountryPackBound = false, WorkingWeekDefined = true };
            Assert.True(SetupWizardEvaluator.IsReady(SetupWizardSteps.Profile, snapshot));
            Assert.False(SetupWizardEvaluator.IsReady(SetupWizardSteps.CountryPack, snapshot));
            Assert.True(SetupWizardEvaluator.IsReady(SetupWizardSteps.WorkingWeek, snapshot));
            Assert.False(SetupWizardEvaluator.IsReady(SetupWizardSteps.Currency, snapshot));
        }

        [Fact]
        [BusinessRule("BR-SET-003")]
        public void Wizard_has_the_nine_mandatory_steps_in_stepper_order()
        {
            var codes = SetupWizardSteps.All.OrderBy(s => s.Order).Select(s => s.Code).ToList();
            Assert.Equal(new[]
            {
                SetupWizardSteps.Profile, SetupWizardSteps.CountryPack, SetupWizardSteps.Currency, SetupWizardSteps.TimeZone,
                SetupWizardSteps.WorkingWeek, SetupWizardSteps.Languages, SetupWizardSteps.CalendarType,
                SetupWizardSteps.NumberingSeries, SetupWizardSteps.StageStructure,
            }, codes);
            Assert.All(SetupWizardSteps.All, s => Assert.True(s.IsMandatory));
        }

        [Fact]
        [BusinessRule("BR-SET-003")]
        public void Saving_a_step_moves_to_the_one_after_it_and_the_last_step_ends_the_walk()
        {
            Assert.Equal(SetupWizardSteps.CountryPack, SetupWizardEvaluator.NextStep(SetupWizardSteps.Profile)!.Code);
            Assert.Equal(SetupWizardSteps.StageStructure, SetupWizardEvaluator.NextStep(SetupWizardSteps.NumberingSeries)!.Code);
            Assert.Null(SetupWizardEvaluator.NextStep(SetupWizardSteps.StageStructure));

            Assert.Equal(SetupWizardSteps.NumberingSeries, SetupWizardEvaluator.PreviousStep(SetupWizardSteps.StageStructure)!.Code);
            Assert.Null(SetupWizardEvaluator.PreviousStep(SetupWizardSteps.Profile));
        }

        [Fact]
        [BusinessRule("BR-SET-003")]
        public void Where_a_save_goes_does_not_depend_on_which_other_steps_are_still_open()
        {
            // The defect this replaced: "next" was read as "the first step still incomplete", so a
            // save on step 7 with step 2 open carried the operator backwards to step 2 — and on a
            // finished setup there was no incomplete step at all, so the walk stopped entirely.
            var onlyProfileDone = new List<SetupChecklist> { new() { StepCode = SetupWizardSteps.Profile, Status = SetupStepStatus.Completed } };
            var everythingDone = SetupWizardSteps.All.Select(s => new SetupChecklist { StepCode = s.Code, Status = SetupStepStatus.Completed }).ToList();

            foreach (var checklist in new[] { onlyProfileDone, everythingDone })
            {
                _ = SetupWizardEvaluator.Evaluate(AllReady(checklist));
                Assert.Equal(SetupWizardSteps.Languages, SetupWizardEvaluator.NextStep(SetupWizardSteps.WorkingWeek)!.Code);
            }
        }

        [Fact]
        [BusinessRule("BR-SET-003")]
        public void Resume_points_at_the_first_step_still_open_and_at_nothing_once_all_are_done()
        {
            var twoDone = SetupWizardSteps.All.Take(2).Select(s => new SetupChecklist { StepCode = s.Code, Status = SetupStepStatus.Completed }).ToList();
            Assert.Equal(SetupWizardSteps.Currency, SetupWizardEvaluator.ResumeAt(SetupWizardEvaluator.Evaluate(AllReady(twoDone)))!.Code);

            Assert.Equal(SetupWizardSteps.Profile, SetupWizardEvaluator.ResumeAt(SetupWizardEvaluator.Evaluate(AllReady()))!.Code);

            var allDone = SetupWizardSteps.All.Select(s => new SetupChecklist { StepCode = s.Code, Status = SetupStepStatus.Completed }).ToList();
            Assert.Null(SetupWizardEvaluator.ResumeAt(SetupWizardEvaluator.Evaluate(AllReady(allDone))));
        }

        [Fact]
        [BusinessRule("BR-SET-003")]
        public void An_unknown_step_code_is_refused_rather_than_answered_with_the_first_step()
        {
            Assert.Throws<ArgumentException>(() => SetupWizardEvaluator.NextStep("NOT_A_STEP"));
            Assert.Throws<ArgumentException>(() => SetupWizardEvaluator.PreviousStep("NOT_A_STEP"));
        }
    }
}
