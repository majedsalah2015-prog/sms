using System;
using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Setup
{
    /// <summary>
    /// BR-SET-006 feature toggles: the optional modules/capabilities a school
    /// may switch off, each with its default and its dependencies (doc §8.4
    /// "dependency warnings, e.g. Transport fees require Transport"). Codes
    /// double as the sidebar's gate — a module whose feature is off vanishes
    /// from the menu (BR-SET-006 composes with deny-by-default permissions
    /// in doc 06; the permission side is the deferred RequirePermission
    /// wiring). Core modules (Schools, Students, Fees…) are not toggleable.
    /// </summary>
    public static class FeatureCatalog
    {
        public sealed record Feature(string Code, string TitleEn, string TitleAr, bool DefaultEnabled, IReadOnlyList<string> DependsOn, string? ModuleCode);

        public const string Portal = "PORTAL";
        public const string StudentAccounts = "STUDENT_ACCOUNTS";
        public const string Admissions = "ADMISSIONS";
        public const string Timetable = "TIMETABLE";
        public const string Examinations = "EXAMINATIONS";
        public const string Certificates = "CERTIFICATES";
        public const string Installments = "INSTALLMENTS";
        public const string Discounts = "DISCOUNTS";
        public const string Transport = "TRANSPORT";
        public const string TransportFees = "TRANSPORT_FEES";
        public const string Health = "HEALTH";
        public const string Discipline = "DISCIPLINE";
        public const string Library = "LIBRARY";
        public const string Cafeteria = "CAFETERIA";
        public const string CafeteriaWallet = "CAFETERIA_WALLET";
        public const string Store = "STORE";
        public const string Activities = "ACTIVITIES";
        public const string Messaging = "MESSAGING";

        private static readonly Feature[] All =
        {
            F(Portal, "Parent/Student portal", "بوابة أولياء الأمور والطلاب", true, null),
            F(StudentAccounts, "Student user accounts", "حسابات الطلاب", false, null, Portal),
            F(Admissions, "Admissions", "القبول والتسجيل", true, "ADM"),
            F(Timetable, "Timetable", "الجدول الدراسي", true, "TTB"),
            F(Examinations, "Examinations", "الاختبارات", true, "EXM"),
            F(Certificates, "Certificates", "الشهادات", true, "CRT"),
            F(Installments, "Installment plans", "خطط التقسيط", true, "INS"),
            F(Discounts, "Discounts & scholarships", "الخصومات والمنح", true, "DSC"),
            F(Transport, "Transportation", "النقل المدرسي", true, "TRN"),
            F(TransportFees, "Transport fees", "رسوم النقل", true, null, Transport),
            F(Health, "Health", "الصحة المدرسية", true, "HLT"),
            F(Discipline, "Discipline", "السلوك والانضباط", true, "DIS"),
            F(Library, "Library", "المكتبة", true, "LIB"),
            F(Cafeteria, "Cafeteria", "المقصف", true, "CAF"),
            F(CafeteriaWallet, "Cafeteria wallets", "محافظ المقصف", true, null, Cafeteria),
            F(Store, "School store", "المتجر المدرسي", true, "STO"),
            F(Activities, "Activities", "الأنشطة", true, "ACT"),
            F(Messaging, "Messaging", "المراسلات", true, "MSG"),
        };

        private static readonly Dictionary<string, Feature> ByCode = All.ToDictionary(f => f.Code, StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<Feature> Features => All;

        public static bool TryGet(string code, out Feature feature) => ByCode.TryGetValue(code, out feature!);

        /// <summary>Feature gating a module code, or null when the module is core (never hidden).</summary>
        public static Feature? ForModule(string moduleCode) =>
            All.FirstOrDefault(f => string.Equals(f.ModuleCode, moduleCode, StringComparison.OrdinalIgnoreCase));

        /// <summary>Features that declare a dependency on <paramref name="code"/> (direct only).</summary>
        public static IReadOnlyList<Feature> Dependents(string code) =>
            All.Where(f => f.DependsOn.Contains(code, StringComparer.OrdinalIgnoreCase)).ToList();

        private static Feature F(string code, string en, string ar, bool defaultEnabled, string? moduleCode, params string[] dependsOn) =>
            new(code, en, ar, defaultEnabled, dependsOn, moduleCode);
    }
}
