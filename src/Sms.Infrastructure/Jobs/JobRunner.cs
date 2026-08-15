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

            var run = new JobRun
            {
                JobDefinitionId = definition.Id,
                TriggerType = triggerType,
                Status = JobStatus.Running,
                StartedAtUtc = _clock.UtcNow,
            };
            _db.JobRuns.Add(run);
            // Persisted before the handler runs, so a hard crash mid-job still
            // leaves a visible "stuck at Running" row instead of nothing at all.
            await _db.SaveChangesAsync(cancellationToken);

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
