namespace Sms.Domain.Reports
{
    /// <summary>BR-RPT-005: heavy reports run Queued with notification-on-ready (the dispatch/notification itself isn't wired — CompleteQueuedRunAsync simulates the queue processor finishing).</summary>
    public enum ReportExecutionStatus : short
    {
        Completed = 1,
        Queued = 2,
        Failed = 3,
    }
}
