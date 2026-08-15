using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Jobs;

namespace Sms.Application.Jobs
{
    /// <summary>
    /// Executes one job by code: records a JobRun, runs the matching
    /// IJobHandler, and captures success/failure — the single path Hangfire
    /// (or a manual admin trigger) calls into. Throws only when the code
    /// doesn't map to any JobDefinition at all (a caller bug). A disabled
    /// definition or a not-yet-registered handler is a benign scheduling
    /// race (e.g. an admin turned the job off between Hangfire's cron firing
    /// and this call) — recorded as a Failed JobRun with an explanatory
    /// ErrorMessage, not thrown, so the scheduler keeps running.
    /// </summary>
    public interface IJobRunner
    {
        Task<JobRun> RunAsync(string jobCode, JobTriggerType triggerType, CancellationToken cancellationToken = default);
    }
}
