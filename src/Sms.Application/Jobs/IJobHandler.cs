using System.Threading;
using System.Threading.Tasks;

namespace Sms.Application.Jobs
{
    /// <summary>
    /// One implementation per job type, resolved by <see cref="IJobRunner"/>
    /// from an injected collection (same fan-out shape as
    /// IWorkflowFinalEffect/IChannelSender/ISeedContributor). Throwing is the
    /// correct way to signal failure — the runner catches it and records
    /// JobRun.Failed; a job must never swallow its own errors.
    /// </summary>
    public interface IJobHandler
    {
        string JobCode { get; }

        Task RunAsync(CancellationToken cancellationToken = default);
    }
}
