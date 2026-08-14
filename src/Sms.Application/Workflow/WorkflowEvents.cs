namespace Sms.Application.Workflow
{
    /// <summary>
    /// Standard events the engine raises for notifications (doc 05 §8).
    /// Dispatch arrives with E-007; until then the names travel on the
    /// transition result. StepOverdue/Escalated are raised by the SLA job
    /// (BR-WF-007, later slice with E-011).
    /// </summary>
    public static class WorkflowEvents
    {
        public const string StepAssigned = "StepAssigned";
        public const string StepOverdue = "StepOverdue";
        public const string Escalated = "Escalated";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Returned = "Returned";
        public const string Cancelled = "Cancelled";
    }
}
