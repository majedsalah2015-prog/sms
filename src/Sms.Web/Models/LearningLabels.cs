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

        /// <summary>doc/Modules/37 §4 homework lifecycle, in the reader's language.</summary>
        public static string HomeworkStatus(Domain.Learning.HomeworkStatus status, bool arabic) => status switch
        {
            Domain.Learning.HomeworkStatus.Draft => arabic ? "مسوّدة" : "Draft",
            Domain.Learning.HomeworkStatus.Issued => arabic ? "مُكلَّف به" : "Issued",
            Domain.Learning.HomeworkStatus.Collecting => arabic ? "قيد التسليم" : "Collecting",
            Domain.Learning.HomeworkStatus.Marking => arabic ? "قيد التصحيح" : "Marking",
            Domain.Learning.HomeworkStatus.Released => arabic ? "رُصدت درجاته" : "Released",
            Domain.Learning.HomeworkStatus.Withdrawn => arabic ? "مسحوب" : "Withdrawn",
            _ => status.ToString(),
        };

        /// <summary>
        /// BR-LRN-005. Both options accept the work — the wording says so, because
        /// a teacher scanning a dropdown should not have to infer that the
        /// missing third option ("refuse late work") was left out on purpose.
        /// </summary>
        public static string LatenessPolicy(Domain.Learning.LatenessPolicy policy, bool arabic) => policy switch
        {
            Domain.Learning.LatenessPolicy.AcceptWithoutPenalty => arabic ? "يُقبل المتأخر بلا خصم" : "Accepted, no penalty",
            Domain.Learning.LatenessPolicy.AcceptWithPenalty => arabic ? "يُقبل المتأخر مع خصم" : "Accepted, with a penalty",
            _ => policy.ToString(),
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
