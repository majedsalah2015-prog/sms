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

        /// <summary>
        /// The permission verbs (doc 06 §4.1) as the role designer prints them. The English is the
        /// enum name deliberately — an administrator reading a permission here should see the same
        /// word the catalogue and the <c>[RequirePermission]</c> attributes use, so a support answer
        /// and the screen agree.
        /// </summary>
        public static string Verb(Sms.Domain.Security.ActionVerb v, bool arabic) => !arabic ? v.ToString() : v switch
        {
            Sms.Domain.Security.ActionVerb.View => "عرض",
            Sms.Domain.Security.ActionVerb.Create => "إضافة",
            Sms.Domain.Security.ActionVerb.Edit => "تعديل",
            Sms.Domain.Security.ActionVerb.Deactivate => "تعطيل",
            Sms.Domain.Security.ActionVerb.Submit => "رفع",
            Sms.Domain.Security.ActionVerb.Approve => "اعتماد",
            Sms.Domain.Security.ActionVerb.Post => "ترحيل",
            Sms.Domain.Security.ActionVerb.Print => "طباعة",
            Sms.Domain.Security.ActionVerb.Export => "تصدير",
            Sms.Domain.Security.ActionVerb.Import => "استيراد",
            Sms.Domain.Security.ActionVerb.Configure => "ضبط",
            _ => v.ToString(),
        };
    }
}
