using Sms.Domain.Students;

namespace Sms.Web.Models
{
    /// <summary>
    /// Bilingual labels for the student social-profile enums (same shape and
    /// placement rationale as <see cref="InstallmentLabels"/>: the display text
    /// of a domain enum is presentation, so it lives beside the screens rather
    /// than in the domain that must not know about a language toggle).
    /// </summary>
    public static class SocialProfileLabels
    {
        public static string Financial(FinancialStatus s, bool ar) => s switch
        {
            FinancialStatus.Normal => ar ? "عادي" : "Normal",
            FinancialStatus.Medium => ar ? "متوسط" : "Medium",
            FinancialStatus.Good => ar ? "جيد" : "Good",
            FinancialStatus.Excellent => ar ? "ممتاز" : "Excellent",
            _ => s.ToString(),
        };

        public static string ParentStatus(ParentLifeStatus s, bool ar) => s switch
        {
            ParentLifeStatus.Alive => ar ? "على قيد الحياة" : "Alive",
            ParentLifeStatus.Deceased => ar ? "متوفى" : "Deceased",
            ParentLifeStatus.Martyr => ar ? "شهيد" : "Martyr",
            ParentLifeStatus.Missing => ar ? "مفقود" : "Missing",
            ParentLifeStatus.Other => ar ? "غير ذلك" : "Other",
            _ => s.ToString(),
        };

        /// <summary>Anything but Alive is worth a glance on a list — these are the cases that carry entitlements.</summary>
        public static string ParentStatusBadge(ParentLifeStatus s) => s switch
        {
            ParentLifeStatus.Alive => "text-bg-light border",
            ParentLifeStatus.Martyr => "text-bg-dark",
            ParentLifeStatus.Missing => "text-bg-warning",
            ParentLifeStatus.Deceased => "text-bg-secondary",
            _ => "text-bg-light border",
        };

        public static string Religion(Religion r, bool ar) => r switch
        {
            Sms.Domain.Students.Religion.Muslim => ar ? "مسلم" : "Muslim",
            Sms.Domain.Students.Religion.Christian => ar ? "مسيحي" : "Christian",
            Sms.Domain.Students.Religion.Other => ar ? "غير ذلك" : "Other",
            _ => r.ToString(),
        };

        public static string Residency(ResidencyStatus s, bool ar) => s switch
        {
            ResidencyStatus.Citizen => ar ? "مواطن" : "Citizen",
            ResidencyStatus.Refugee => ar ? "لاجئ" : "Refugee",
            _ => s.ToString(),
        };
    }
}
