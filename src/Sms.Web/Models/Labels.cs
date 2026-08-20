using Sms.Domain.Grades;
using Sms.Domain.Schools;

namespace Sms.Web.Models
{
    /// <summary>
    /// Shared bilingual display labels for enums that many screens print
    /// (year status in year pickers, gender policy, …) so the Arabic UI never
    /// shows raw enum names.
    /// </summary>
    public static class Labels
    {
        public static string YearStatus(AcademicYearStatus s, bool arabic) => s switch
        {
            AcademicYearStatus.Preparation => arabic ? "إعداد" : "Preparation",
            AcademicYearStatus.Active => arabic ? "نشط" : "Active",
            AcademicYearStatus.Closing => arabic ? "قيد الإغلاق" : "Closing",
            AcademicYearStatus.Closed => arabic ? "مغلق" : "Closed",
            AcademicYearStatus.Archived => arabic ? "مؤرشف" : "Archived",
            _ => s.ToString(),
        };

        public static string ApplicationStatus(Sms.Domain.Admissions.ApplicationStatus s, bool arabic) => s switch
        {
            Sms.Domain.Admissions.ApplicationStatus.Draft => arabic ? "مسودة" : "Draft",
            Sms.Domain.Admissions.ApplicationStatus.Submitted => arabic ? "مقدَّم" : "Submitted",
            Sms.Domain.Admissions.ApplicationStatus.UnderReview => arabic ? "قيد المراجعة" : "Under review",
            Sms.Domain.Admissions.ApplicationStatus.Recommended => arabic ? "موصى به" : "Recommended",
            Sms.Domain.Admissions.ApplicationStatus.Approved => arabic ? "معتمد" : "Approved",
            Sms.Domain.Admissions.ApplicationStatus.Waitlisted => arabic ? "قائمة الانتظار" : "Waitlisted",
            Sms.Domain.Admissions.ApplicationStatus.Registered => arabic ? "مسجَّل" : "Registered",
            Sms.Domain.Admissions.ApplicationStatus.Rejected => arabic ? "مرفوض" : "Rejected",
            Sms.Domain.Admissions.ApplicationStatus.Lapsed => arabic ? "ساقط" : "Lapsed",
            _ => s.ToString(),
        };

        public static string Gender(GenderPolicy g, bool arabic) => g switch
        {
            GenderPolicy.Boys => arabic ? "بنين" : "Boys",
            GenderPolicy.Girls => arabic ? "بنات" : "Girls",
            _ => arabic ? "مختلط" : "Mixed",
        };
    }
}
