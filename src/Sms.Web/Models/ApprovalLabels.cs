using Sms.Domain.Workflow;

namespace Sms.Web.Models
{
    /// <summary>
    /// Bilingual names for the workflow vocabulary (doc 05 §2/§3). Kept out of
    /// <c>Labels.cs</c> because that file is shared by every module and this one
    /// belongs to the workflow framework's own screens.
    /// <para>
    /// State names are not here: a state is a seeded row carrying its own
    /// <c>LocalizedName</c>, so the school's wording wins over ours.
    /// </para>
    /// </summary>
    public static class ApprovalLabels
    {
        public static string Action(WorkflowActionType action, bool ar) => action switch
        {
            WorkflowActionType.Submit => ar ? "تقديم" : "Submit",
            WorkflowActionType.Approve => ar ? "اعتماد" : "Approve",
            WorkflowActionType.Reject => ar ? "رفض" : "Reject",
            WorkflowActionType.Return => ar ? "إعادة للتصحيح" : "Return",
            WorkflowActionType.Cancel => ar ? "إلغاء" : "Cancel",
            WorkflowActionType.Complete => ar ? "إنهاء" : "Complete",
            _ => action.ToString(),
        };

        /// <summary>Past tense, for a history line that reads as a sentence.</summary>
        public static string ActionPast(WorkflowActionType action, bool ar) => action switch
        {
            WorkflowActionType.Submit => ar ? "قُدِّم" : "submitted",
            WorkflowActionType.Approve => ar ? "اعتُمد" : "approved",
            WorkflowActionType.Reject => ar ? "رُفض" : "rejected",
            WorkflowActionType.Return => ar ? "أُعيد للتصحيح" : "returned",
            WorkflowActionType.Cancel => ar ? "أُلغي" : "cancelled",
            WorkflowActionType.Complete => ar ? "أُنهي" : "completed",
            _ => action.ToString(),
        };

        public static string ActionBadge(WorkflowActionType action) => action switch
        {
            WorkflowActionType.Approve => "text-bg-success",
            WorkflowActionType.Reject => "text-bg-danger",
            WorkflowActionType.Return => "text-bg-warning",
            WorkflowActionType.Cancel => "text-bg-secondary",
            _ => "text-bg-light border",
        };

        /// <summary>How long an item has been waiting, said in words rather than a bare number.</summary>
        public static string Age(int days, bool ar)
        {
            if (days <= 0)
            {
                return ar ? "اليوم" : "today";
            }

            if (days == 1)
            {
                return ar ? "منذ يوم" : "1 day";
            }

            return ar ? $"منذ {days} يوماً" : $"{days} days";
        }
    }
}
