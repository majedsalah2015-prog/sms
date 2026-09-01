using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sms.Domain.Setup;

namespace Sms.Application.Setup
{
    /// <summary>
    /// The product's setting keys (doc/Modules/01 §8.3 hub groups: Regional,
    /// Financial, Languages, Portal). Every key declares its value type,
    /// whether it is year-versionable (BR-SET-005 — only those may carry an
    /// AcademicYearId), and a validator. Unknown keys are rejected so the
    /// table never becomes a free-form bag (BR-GLB-112 spirit).
    /// </summary>
    public static class SettingKeys
    {
        public sealed record Definition(string Key, SettingValueType ValueType, bool YearVersionable, string Group, Func<string, string?> Validate);

        // Regional
        public const string CalendarType = "Regional.CalendarType";           // Gregorian | Hijri | Both
        public const string WorkingDays = "Regional.WorkingDays";             // DayOfWeek names, ≥ 4 (doc §9)
        public const string HijriDisplay = "Regional.HijriDisplay";           // bool
        public const string FirstDayOfWeek = "Regional.FirstDayOfWeek";       // DayOfWeek name

        /// <summary>BR-CAL-006: the ministry minimum the calendar board measures its instructional-day count against. Unset means no minimum is enforced.</summary>
        public const string MinimumInstructionalDays = "Regional.MinimumInstructionalDays"; // whole days, year-versionable

        // Languages
        public const string EnabledLanguages = "Languages.Enabled";           // ar,en
        public const string DefaultLanguage = "Languages.Default";            // ar | en

        // Financial
        public const string VatRate = "Financial.VatRate";                    // fraction, year-versionable
        public const string VatRegistrationNumber = "Financial.VatRegistrationNumber";
        public const string ReceivablesAlertThreshold = "Financial.ReceivablesAlertThreshold"; // decimal, year-versionable

        // Portal
        public const string PortalSelfRegistration = "Portal.SelfRegistration"; // bool

        // Notifications (doc/Modules/33 §8.5, BR-NTF-004). Not year-versionable: a
        // messaging budget is an operating decision a school revises mid-year, and
        // pinning it to an academic year would make last July's ceiling govern this
        // October's sends.

        /// <summary>The dialling code a national-format parent mobile is completed with — see <c>PhoneNumberRules</c>. Digits, with or without the plus.</summary>
        public const string DefaultDiallingCode = "Notifications.DefaultDiallingCode";

        /// <summary>Messages per calendar month on the SMS channel before BR-NTF-004's 80% alert and optional hard stop. 0 = no ceiling.</summary>
        public const string SmsMonthlyBudget = "Notifications.SmsMonthlyBudget";

        /// <summary>The same ceiling for WhatsApp, counted separately because the two are billed separately.</summary>
        public const string WhatsAppMonthlyBudget = "Notifications.WhatsAppMonthlyBudget";

        /// <summary>BR-NTF-004: whether reaching the ceiling stops sending or only warns. Safety-class events are exempt either way.</summary>
        public const string BudgetHardStop = "Notifications.BudgetHardStop"; // bool

        public static readonly string[] CalendarTypes = { "Gregorian", "Hijri", "Both" };

        public static readonly string[] SupportedLanguages = { "ar", "en" };

        private static readonly Dictionary<string, Definition> Catalog = new[]
        {
            new Definition(CalendarType, SettingValueType.String, false, "Regional", v => OneOf(v, CalendarTypes)),
            new Definition(WorkingDays, SettingValueType.CodeList, true, "Regional", WorkingWeek.Validate),
            new Definition(HijriDisplay, SettingValueType.Boolean, false, "Regional", Bool),
            new Definition(FirstDayOfWeek, SettingValueType.String, false, "Regional", v => Enum.TryParse<DayOfWeek>(v, true, out _) ? null : "not a day of week"),
            new Definition(MinimumInstructionalDays, SettingValueType.Integer, true, "Regional", v =>
                int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) && days is >= 0 and <= 366 ? null : "must be a whole number of days in [0,366]"),
            new Definition(EnabledLanguages, SettingValueType.CodeList, false, "Languages", v =>
            {
                var codes = SplitCodes(v);
                if (codes.Count == 0) return "at least one language";
                var bad = codes.FirstOrDefault(c => !SupportedLanguages.Contains(c));
                return bad == null ? null : $"unsupported language '{bad}'";
            }),
            new Definition(DefaultLanguage, SettingValueType.String, false, "Languages", v => OneOf(v, SupportedLanguages)),
            new Definition(VatRate, SettingValueType.Decimal, true, "Financial", v =>
                decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) && d >= 0m && d < 1m ? null : "VAT rate must be a fraction in [0,1)"),
            new Definition(VatRegistrationNumber, SettingValueType.String, false, "Financial", v => string.IsNullOrWhiteSpace(v) ? "required" : null),
            new Definition(ReceivablesAlertThreshold, SettingValueType.Decimal, true, "Financial", v =>
                decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) && d >= 0m ? null : "must be a non-negative amount"),
            new Definition(PortalSelfRegistration, SettingValueType.Boolean, false, "Portal", Bool),
            new Definition(DefaultDiallingCode, SettingValueType.String, false, "Notifications", v =>
            {
                var digits = new string((v ?? string.Empty).Where(char.IsDigit).ToArray());
                return digits.Length is >= 1 and <= 4 ? null : "a country dialling code is one to four digits, e.g. 970 or +966";
            }),
            new Definition(SmsMonthlyBudget, SettingValueType.Integer, false, "Notifications", NonNegativeCount),
            new Definition(WhatsAppMonthlyBudget, SettingValueType.Integer, false, "Notifications", NonNegativeCount),
            new Definition(BudgetHardStop, SettingValueType.Boolean, false, "Notifications", Bool),
        }.ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyCollection<Definition> All => Catalog.Values;

        public static bool TryGet(string key, out Definition definition) => Catalog.TryGetValue(key, out definition!);

        public static IReadOnlyList<string> SplitCodes(string value) =>
            (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

        private static string? OneOf(string value, string[] allowed) =>
            allowed.Contains(value, StringComparer.OrdinalIgnoreCase) ? null : $"must be one of {string.Join("|", allowed)}";

        private static string? Bool(string value) => bool.TryParse(value, out _) ? null : "must be true or false";

        /// <summary>A count of messages: whole, not negative, and zero meaning "no ceiling" rather than "send nothing".</summary>
        private static string? NonNegativeCount(string value) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count >= 0
                ? null
                : "must be a whole number of messages, 0 for no limit";
    }
}
