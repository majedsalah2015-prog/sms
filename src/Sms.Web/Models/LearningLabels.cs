using Sms.Domain.Attachments;
using Sms.Domain.Learning;

namespace Sms.Web.Models
{
    /// <summary>
    /// Module 37's enum wording. Enum values never reach a screen through
    /// ToString() — a teacher reading "Retired" in an Arabic interface is a
    /// bilingual defect, not a cosmetic one.
    /// </summary>
    public static class LearningLabels
    {
        public static string LessonStatusName(LessonStatus status, bool arabic) => status switch
        {
            LessonStatus.Draft => arabic ? "مسوّدة" : "Draft",
            LessonStatus.Published => arabic ? "منشور" : "Published",
            LessonStatus.Retired => arabic ? "مسحوب" : "Retired",
            _ => status.ToString(),
        };

        /// <summary>Bootstrap chip colour: a draft is quiet, a published lesson is live, a retired one is spent.</summary>
        public static string LessonStatusChip(LessonStatus status) => status switch
        {
            LessonStatus.Draft => "bg-secondary",
            LessonStatus.Published => "bg-success",
            LessonStatus.Retired => "bg-dark",
            _ => "bg-secondary",
        };

        /// <summary>BR-LRN-006 / BR-ATT-009 — why a file is or is not servable, said plainly.</summary>
        public static string ScanStateName(ScanStatus? status, bool arabic) => status switch
        {
            ScanStatus.Clean => arabic ? "مفحوص" : "Scanned",
            ScanStatus.Pending => arabic ? "قيد الفحص" : "Scan pending",
            ScanStatus.Infected => arabic ? "مرفوض — مصاب" : "Rejected — infected",
            _ => arabic ? "لا ملف" : "No file",
        };
    }
}
