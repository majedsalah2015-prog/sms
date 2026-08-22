using System;
using System.Collections.Generic;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>The key isn't in SettingKeys (doc/Modules/01 §7 — settings are a closed catalog).</summary>
    public class UnknownSettingKeyException : InvalidOperationException
    {
        public UnknownSettingKeyException(string key)
            : base($"Unknown setting key '{key}'.")
        {
        }
    }

    /// <summary>doc/Modules/01 §9: server-side validation of a setting value failed.</summary>
    public class InvalidSettingValueException : InvalidOperationException
    {
        public InvalidSettingValueException(string key, string reason)
            : base($"Invalid value for setting '{key}': {reason}.")
        {
            Key = key;
        }

        /// <summary>
        /// Which setting was refused. The message stays English because a log entry should read the
        /// same everywhere; a screen that has to say this in Arabic needs the key, not the sentence.
        /// </summary>
        public string Key { get; }
    }

    /// <summary>BR-SET-005: only year-versionable keys may be pinned to an academic year, and a financial value can't be pinned to a year that has already ended (doc §9 "effective date ≥ today").</summary>
    public class SettingEffectiveDateException : InvalidOperationException
    {
        public SettingEffectiveDateException(string key, string reason)
            : base($"Setting '{key}' cannot be effective-dated as requested: {reason} (BR-SET-005).")
        {
        }
    }

    public class UnknownFeatureException : InvalidOperationException
    {
        public UnknownFeatureException(string code)
            : base($"Unknown feature '{code}' (BR-SET-006).")
        {
        }
    }

    /// <summary>BR-SET-006 dependency warning turned hard: the change is blocked by the listed features.</summary>
    public class FeatureDependencyException : InvalidOperationException
    {
        public FeatureDependencyException(string code, bool enable, IReadOnlyList<string> blockers)
            : base(enable
                ? $"Feature '{code}' requires {string.Join(", ", blockers)} to be enabled first (BR-SET-006)."
                : $"Feature '{code}' cannot be disabled while {string.Join(", ", blockers)} depend on it (BR-SET-006).")
        {
            Blockers = blockers;
        }

        public IReadOnlyList<string> Blockers { get; }
    }

    public class UnknownCountryPackException : InvalidOperationException
    {
        public UnknownCountryPackException(string code)
            : base($"No active country pack '{code}' (BR-SET-004).")
        {
        }
    }

    /// <summary>BR-SET-004: after go-live, changing the country pack is support-gated and must carry a reason (T1).</summary>
    public class CountryPackChangeRequiresReasonException : InvalidOperationException
    {
        public CountryPackChangeRequiresReasonException()
            : base("Changing the country pack after go-live requires a reason (BR-SET-004).")
        {
        }
    }

    public class UnknownSetupStepException : InvalidOperationException
    {
        public UnknownSetupStepException(string code)
            : base($"Unknown setup step '{code}' (BR-SET-003).")
        {
        }
    }

    /// <summary>BR-SET-003: the step's data isn't in place, so it can't be marked complete.</summary>
    public class SetupStepNotReadyException : InvalidOperationException
    {
        public SetupStepNotReadyException(string code)
            : base($"Setup step '{code}' cannot be completed until its data is in place (BR-SET-003).")
        {
        }
    }

    /// <summary>BR-SET-003: setup isn't complete — either "Setup Complete" can't be declared yet, or the first academic year can't be activated.</summary>
    public class SetupIncompleteException : InvalidOperationException
    {
        public SetupIncompleteException(IReadOnlyList<string> pendingSteps)
            : base(pendingSteps.Count == 0
                ? "Setup Wizard has not been declared complete (BR-SET-003)."
                : $"Setup Wizard is not complete; pending steps: {string.Join(", ", pendingSteps)} (BR-SET-003).")
        {
            PendingSteps = pendingSteps;
        }

        public IReadOnlyList<string> PendingSteps { get; }
    }
}
