using System;
using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Setup
{
    /// <summary>
    /// BR-SET-003's mandatory wizard steps, in stepper order: profile,
    /// country pack, currency, time zone, working week, languages, calendar
    /// type, numbering series, stage structure. All are mandatory in v1;
    /// the flag exists so a future country pack can relax one.
    /// </summary>
    public static class SetupWizardSteps
    {
        public sealed record Step(string Code, int Order, string TitleEn, string TitleAr, bool IsMandatory);

        public const string Profile = "PROFILE";
        public const string CountryPack = "COUNTRY_PACK";
        public const string Currency = "CURRENCY";
        public const string TimeZone = "TIME_ZONE";
        public const string WorkingWeek = "WORKING_WEEK";
        public const string Languages = "LANGUAGES";
        public const string CalendarType = "CALENDAR_TYPE";
        public const string NumberingSeries = "NUMBERING_SERIES";
        public const string StageStructure = "STAGE_STRUCTURE";

        public static readonly IReadOnlyList<Step> All = new[]
        {
            new Step(Profile, 1, "School profile", "ملف المدرسة", true),
            new Step(CountryPack, 2, "Country pack", "حزمة الدولة", true),
            new Step(Currency, 3, "Currency", "العملة", true),
            new Step(TimeZone, 4, "Time zone", "المنطقة الزمنية", true),
            new Step(WorkingWeek, 5, "Working week", "أسبوع العمل", true),
            new Step(Languages, 6, "Languages", "اللغات", true),
            new Step(CalendarType, 7, "Calendar type", "نوع التقويم", true),
            new Step(NumberingSeries, 8, "Numbering series", "سلاسل الترقيم", true),
            new Step(StageStructure, 9, "Stage structure", "الهيكل الدراسي", true),
        };

        public static bool TryGet(string code, out Step step)
        {
            step = All.FirstOrDefault(s => string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase))!;
            return step != null;
        }
    }
}
