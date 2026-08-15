using System.Threading;
using System.Threading.Tasks;
using Sms.Application.Common.Interfaces;
using Sms.Application.Jobs;
using Sms.Infrastructure.Audit;

namespace Sms.Infrastructure.Jobs
{
    /// <summary>
    /// E-004's deferred "daily checkpoint job" (BR-AUD-007) — the trigger
    /// this epic was waiting for. Computes the checkpoint over the just-
    /// completed UTC day.
    /// </summary>
    public class AuditCheckpointJobHandler : IJobHandler
    {
        private readonly IntegrityCheckpointService _checkpoints;
        private readonly IClock _clock;

        public AuditCheckpointJobHandler(IntegrityCheckpointService checkpoints, IClock clock)
        {
            _checkpoints = checkpoints;
            _clock = clock;
        }

        public string JobCode => "AuditIntegrityCheckpoint";

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            var periodEnd = _clock.UtcNow.Date;
            var periodStart = periodEnd.AddDays(-1);
            await _checkpoints.ComputeAsync(periodStart, periodEnd, cancellationToken);
        }
    }
}
