using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Jobs;
using Sms.Domain.Audit;
using Sms.Domain.Jobs;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Jobs
{
    /// <summary>Standalone operation — saves itself, no larger transaction to ride.</summary>
    public class JobRunner : IJobRunner
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly IAuditEventWriter _auditEvents;
        private readonly IEnumerable<IJobHandler> _handlers;

        public JobRunner(AppDbContext db, IClock clock, IAuditEventWriter auditEvents, IEnumerable<IJobHandler> handlers)
        {
            _db = db;
            _clock = clock;
            _auditEvents = auditEvents;
            _handlers = handlers;
        }

        public async Task<JobRun> RunAsync(string jobCode, JobTriggerType triggerType, CancellationToken cancellationToken = default)
        {
            var definition = await _db.JobDefinitions.SingleOrDefaultAsync(j => j.Code == jobCode, cancellationToken);
            if (definition == null)
            {
                throw new UnknownJobException(jobCode);
            }

            await ReapAbandonedRunsAsync(definition.Id, cancellationToken);

            var run = new JobRun
            {
                JobDefinitionId = definition.Id,
                TriggerType = triggerType,
                Status = JobStatus.Running,
                StartedAtUtc = _clock.UtcNow,
            };
            _db.JobRuns.Add(run);

            try
            {
                // Persisted before the handler runs, so a hard crash mid-job still
                // leaves a visible "stuck at Running" row instead of nothing at all.
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // UX_JobRun_InFlight refused it: another run of this job is already going. Report
                // that run rather than starting a second one. This is the ordinary case after
                // downtime, when Hangfire enqueues one job per occurrence it missed — and running
                // two dispatches at once would send every queued notification twice.
                _db.Entry(run).State = EntityState.Detached;
                return await _db.JobRuns.AsNoTracking()
                    .Where(r => r.JobDefinitionId == definition.Id && r.Status == JobStatus.Running)
                    .OrderByDescending(r => r.Id)
                    .FirstAsync(cancellationToken);
            }

            if (!definition.IsEnabled)
            {
                await CompleteAsync(run, JobStatus.Failed, "Job is disabled.", cancellationToken);
                return run;
            }

            var handler = _handlers.FirstOrDefault(h => h.JobCode == jobCode);
            if (handler == null)
            {
                await CompleteAsync(run, JobStatus.Failed, $"No handler registered for job '{jobCode}'.", cancellationToken);
                return run;
            }

            try
            {
                await handler.RunAsync(cancellationToken);
                await CompleteAsync(run, JobStatus.Succeeded, null, cancellationToken);
            }
            catch (Exception ex)
            {
                // Intentionally broad: one failing job must never take the scheduler down (doc 02 T-6).
                await CompleteAsync(run, JobStatus.Failed, ex.Message, cancellationToken);
            }

            return run;
        }

        /// <summary>
        /// How long a run may sit at Running before it is presumed dead. Longer than any job here
        /// takes and short enough that a crash does not block the schedule for a working day —
        /// without this, one hard kill mid-job would stop that job forever, since the in-flight
        /// index would keep refusing every successor.
        /// </summary>
        private static readonly TimeSpan AbandonedAfter = TimeSpan.FromHours(6);

        private async Task ReapAbandonedRunsAsync(int jobDefinitionId, CancellationToken cancellationToken)
        {
            var cutoff = _clock.UtcNow - AbandonedAfter;
            var abandoned = await _db.JobRuns
                .Where(r => r.JobDefinitionId == jobDefinitionId && r.Status == JobStatus.Running && r.StartedAtUtc < cutoff)
                .ToListAsync(cancellationToken);

            if (abandoned.Count == 0)
            {
                return;
            }

            foreach (var run in abandoned)
            {
                run.Status = JobStatus.Failed;
                run.ErrorMessage = "Abandoned: the run never reported an outcome, most likely a host restart mid-job.";
                run.CompletedAtUtc = _clock.UtcNow;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        private async Task CompleteAsync(JobRun run, JobStatus status, string? errorMessage, CancellationToken cancellationToken)
        {
            run.Status = status;
            run.ErrorMessage = errorMessage;
            run.CompletedAtUtc = _clock.UtcNow;
            _auditEvents.Log(AuditAction.JobRun, nameof(JobDefinition), run.JobDefinitionId, reason: errorMessage);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
