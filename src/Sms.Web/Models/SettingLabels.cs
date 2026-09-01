using System;
using System.Linq;
using Sms.Application.Setup;

namespace Sms.Web.Models
{
    /// <summary>
    /// Bilingual names for the settings hub's keys and for the values they hold.
    /// <para>
    /// A key like <c>Regional.CalendarType</c> is an identifier, not a label — it is what the
    /// business rules and the support conversation both call this setting, so it stays on the
    /// screen. What was missing beside it is the sentence a school administrator reads: the code
    /// answers "which setting is this in the docs", the name answers "what does it do".
    /// </para>
    /// <para>
    /// The values needed the same treatment and needed it more. A row reading
    /// <c>Regional.HijriDisplay = false</c> is two pieces of English in an Arabic screen, and
    /// "false" is the half a reader is most likely to misread.
    /// </para>
    /// </summary>
    public static class SettingLabels
    {
        /// <summary>What the setting is called, in the reader's language.</summary>
        public static string Name(string key, bool arabic) => key switch
        {
            SettingKeys.CalendarType => arabic ? "نوع التقويم" : "Calendar type",
            SettingKeys.WorkingDays => arabic ? "أيام الدوام" : "Working days",
            SettingKeys.HijriDisplay => arabic ? "عرض التاريخ الهجري" : "Show Hijri dates",
            SettingKeys.FirstDayOfWeek => arabic ? "أول أيام الأسبوع" : "First day of the week",
            SettingKeys.MinimumInstructionalDays => arabic ? "الحد الأدنى للأيام الدراسية" : "Minimum instructional days",
            SettingKeys.EnabledLanguages => arabic ? "اللغات المفعّلة" : "Enabled languages",
            SettingKeys.DefaultLanguage => arabic ? "اللغة الافتراضية" : "Default language",
            SettingKeys.VatRate => arabic ? "نسبة ضريبة القيمة المضافة" : "VAT rate",
            SettingKeys.VatRegistrationNumber => arabic ? "الرقم الضريبي" : "VAT registration number",
            SettingKeys.ReceivablesAlertThreshold => arabic ? "حدّ التنبيه للذمم المدينة" : "Receivables alert threshold",
            SettingKeys.PortalSelfRegistration => arabic ? "التسجيل الذاتي في البوابة" : "Portal self-registration",
            SettingKeys.DefaultDiallingCode => arabic ? "رمز الاتصال الدولي" : "Country dialling code",
            SettingKeys.SmsMonthlyBudget => arabic ? "سقف الرسائل النصية شهرياً" : "SMS ceiling per month",
            SettingKeys.WhatsAppMonthlyBudget => arabic ? "سقف رسائل واتساب شهرياً" : "WhatsApp ceiling per month",
            SettingKeys.BudgetHardStop => arabic ? "إيقاف الإرسال عند بلوغ السقف" : "Stop sending at the ceiling",
            _ => key,
        };

        /// <summary>One line saying what changing it actually does, for the row that is about to be edited.</summary>
        public static string? Hint(string key, bool arabic) => key switch
        {
            SettingKeys.CalendarType => arabic
                ? "التقويم الذي تُقرأ به التواريخ في النظام: ميلادي أو هجري أو كلاهما معاً."
                : "The calendar dates are read in across the system: Gregorian, Hijri, or both together.",
            SettingKeys.WorkingDays => arabic
                ? "أيام الدوام الأسبوعي — أربعة أيام على الأقل. ومنها يُحسب الحضور والجدول وأيام العام."
                : "The weekly working days — four at minimum. Attendance, the timetable and the year's day count are all built from these.",
            SettingKeys.HijriDisplay => arabic
                ? "إظهار التاريخ الهجري بجانب الميلادي في الشاشات والتقارير."
                : "Whether Hijri dates are shown beside Gregorian ones on screens and reports.",
            SettingKeys.FirstDayOfWeek => arabic
                ? "اليوم الذي يبدأ به الأسبوع في التقويمات والجداول."
                : "The day the week starts on in calendars and timetables.",
            SettingKeys.MinimumInstructionalDays => arabic
                ? "الحد الأدنى الوزاري لعدد الأيام الدراسية في العام. تُنبِّه لوحة التقويم حين يقلّ العدد عنه ولا تمنع النشر. اتركه فارغاً إن لم يكن هناك حد. قابل للإصدار لكل عام."
                : "The ministry minimum for instructional days in a year. The calendar board warns when the count falls below it; it never blocks publication. Leave it unset if there is no minimum. Versionable per year.",
            SettingKeys.EnabledLanguages => arabic
                ? "اللغات التي يستطيع المستخدم التبديل بينها (ar,en)."
                : "The languages a user may switch between (ar,en).",
            SettingKeys.DefaultLanguage => arabic
                ? "لغة المستخدم الجديد قبل أن يختار."
                : "A new user's language before they choose one.",
            SettingKeys.VatRate => arabic
                ? "النسبة ككسر عشري (0.16 = ‎16%‎). قابلة للإصدار لكل عام دراسي."
                : "The rate as a fraction (0.16 = 16%). Versionable per academic year.",
            SettingKeys.VatRegistrationNumber => arabic
                ? "رقم التسجيل الضريبي للمدرسة كما يظهر على الفواتير."
                : "The school's tax registration number as it appears on invoices.",
            SettingKeys.ReceivablesAlertThreshold => arabic
                ? "المبلغ الذي يُنبَّه عنده على متأخرات الأسرة. قابل للإصدار لكل عام."
                : "The amount at which a family's arrears raise an alert. Versionable per year.",
            SettingKeys.PortalSelfRegistration => arabic
                ? "السماح لولي الأمر بإنشاء حساب البوابة بنفسه بدل أن تُنشئه المدرسة."
                : "Whether a parent may create their own portal account instead of the school creating it.",
            SettingKeys.DefaultDiallingCode => arabic
                ? "الرمز الذي يُكمِل به النظام أرقام الجوال المكتوبة بالصيغة المحلية قبل إرسال واتساب أو رسالة نصية. وبدونه تُعامَل تلك الأرقام كأن لا رقم لها."
                : "The code the system completes national-format mobile numbers with before a WhatsApp or SMS send. Without it, those numbers are treated as no number at all.",
            SettingKeys.SmsMonthlyBudget => arabic
                ? "عدد الرسائل النصية المسموح بها في الشهر قبل التنبيه، والإيقاف إن كان مفعَّلاً. والصفر يعني لا سقف (BR-NTF-004)."
                : "How many SMS messages a month before the alert, and the stop if it is on. Zero means no ceiling (BR-NTF-004).",
            SettingKeys.WhatsAppMonthlyBudget => arabic
                ? "نفس السقف لرسائل واتساب، ويُحسب على حدة لأن الفوترة منفصلة."
                : "The same ceiling for WhatsApp, counted separately because it is billed separately.",
            SettingKeys.BudgetHardStop => arabic
                ? "هل يوقف بلوغ السقف الإرسال فعلاً أم ينبّه فقط. ورسائل صنف السلامة تمرّ في الحالتين."
                : "Whether reaching the ceiling actually stops sending or only warns. Safety-class messages go out either way.",
            _ => null,
        };

        /// <summary>
        /// The stored value as a reader should see it. Booleans, day names and calendar types become
        /// words; a comma-separated list becomes a list of words; anything else is shown as stored,
        /// because a number or a registration code is already what it means.
        /// </summary>
        public static string Value(string key, string? raw, bool arabic)
        {
            var text = (raw ?? string.Empty).Trim();
            if (text.Length == 0) { return "—"; }

            return key switch
            {
                SettingKeys.HijriDisplay or SettingKeys.PortalSelfRegistration or SettingKeys.BudgetHardStop => Boolean(text, arabic),
                SettingKeys.CalendarType => Calendar(text, arabic),
                SettingKeys.FirstDayOfWeek => Day(text, arabic),
                SettingKeys.WorkingDays => string.Join("، ", text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(d => Day(d.Trim(), arabic))),
                SettingKeys.EnabledLanguages => string.Join("، ", text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(l => Language(l.Trim(), arabic))),
                SettingKeys.DefaultLanguage => Language(text, arabic),
                SettingKeys.VatRate => Percent(text),
                _ => text,
            };
        }

        /// <summary>True when the rendered value differs from the stored one and the raw form is worth showing beside it.</summary>
        public static bool IsTranslated(string key) => key is
            SettingKeys.HijriDisplay or SettingKeys.PortalSelfRegistration or SettingKeys.BudgetHardStop or SettingKeys.CalendarType
            or SettingKeys.FirstDayOfWeek or SettingKeys.WorkingDays or SettingKeys.EnabledLanguages
            or SettingKeys.DefaultLanguage or SettingKeys.VatRate;

        /// <summary>
        /// The values this key will actually accept, so the editor can offer them instead of leaving
        /// an administrator to discover by rejection that the box wanted the English word "Gregorian".
        /// Empty where the value is free text or a number.
        /// </summary>
        public static string[] Options(string key) => key switch
        {
            SettingKeys.CalendarType => SettingKeys.CalendarTypes,
            SettingKeys.HijriDisplay or SettingKeys.PortalSelfRegistration => new[] { "true", "false" },
            SettingKeys.FirstDayOfWeek => Enum.GetNames(typeof(DayOfWeek)),
            SettingKeys.DefaultLanguage => SettingKeys.SupportedLanguages,
            SettingKeys.EnabledLanguages => new[] { "ar,en", "ar", "en" },
            SettingKeys.WorkingDays => new[] { "Sunday,Monday,Tuesday,Wednesday,Thursday", "Monday,Tuesday,Wednesday,Thursday,Friday" },
            _ => Array.Empty<string>(),
        };

        private static string Boolean(string v, bool arabic) =>
            v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1"
                ? (arabic ? "نعم" : "Yes")
                : (arabic ? "لا" : "No");

        private static string Calendar(string v, bool arabic) => v.ToLowerInvariant() switch
        {
            "gregorian" => arabic ? "ميلادي" : "Gregorian",
            "hijri" => arabic ? "هجري" : "Hijri",
            "both" => arabic ? "ميلادي وهجري" : "Gregorian and Hijri",
            _ => v,
        };

        private static string Language(string v, bool arabic) => v.ToLowerInvariant() switch
        {
            "ar" => arabic ? "العربية" : "Arabic",
            "en" => arabic ? "الإنجليزية" : "English",
            _ => v,
        };

        /// <summary>A fraction is how it is stored and a percentage is how it is understood.</summary>
        private static string Percent(string v) =>
            decimal.TryParse(v, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d)
                ? (d * 100m).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "%"
                : v;

        public static string Day(string v, bool arabic)
        {
            if (!Enum.TryParse<DayOfWeek>(v, true, out var day)) { return v; }
            if (!arabic) { return day.ToString(); }

            return day switch
            {
                DayOfWeek.Sunday => "الأحد",
                DayOfWeek.Monday => "الاثنين",
                DayOfWeek.Tuesday => "الثلاثاء",
                DayOfWeek.Wednesday => "الأربعاء",
                DayOfWeek.Thursday => "الخميس",
                DayOfWeek.Friday => "الجمعة",
                _ => "السبت",
            };
        }
    }
}
