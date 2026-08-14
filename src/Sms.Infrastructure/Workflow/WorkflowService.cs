using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Interfaces;
using Sms.Application.Security;
using Sms.Application.Workflow;
using Sms.Domain.Audit;
using Sms.Domain.Workflow;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Workflow
{
    /// <summary>
    /// Runs the workflow catalog: instances start on the active definition
    /// version and stay pinned to it (BR-WF-008); a transition applies state,
    /// step trail, audit event, and final effects in ONE SaveChanges — the
    /// E-004 pipeline wraps it all in a single transaction, so BR-WF-002 and
    /// BR-WF-009 hold or nothing persists.
    /// </summary>
    public class WorkflowService : IWorkflowService
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _currentUser;
        private readonly IWorkingYearContext _workingYear;
        private readonly IClock _clock;
        private readonly IAuditEventWriter _auditEvents;
        private readonly IPermissionService _permissions;
        private readonly IEnumerable<IWorkflowFinalEffect> _finalEffects;

        public WorkflowService(
            AppDbContext db,
            ICurrentUser currentUser,
            IWorkingYearContext workingYear,
            IClock clock,
            IAuditEventWriter auditEvents,
            IPermissionService permissions,
            IEnumerable<IWorkflowFinalEffect> finalEffects)
        {
            _db = db;
            _currentUser = currentUser;
            _workingYear = workingYear;
            _clock = clock;
            _auditEvents = auditEvents;
            _permissions = permissions;
            _finalEffects = finalEffects;
        }

        public async Task<WorkflowInstance> StartAsync(
            string workflowCode,
            string entityTypeName,
            long entityId,
            string? businessKey = null,
            decimal? routingValue = null,
            CancellationToken cancellationToken = default)
        {
            var definition = await _db.WorkflowDefinitions
                .Include(d => d.States)
                .Where(d => d.Code == workflowCode && d.IsActive)
                .OrderByDescending(d => d.Version)
                .FirstOrDefaultAsync(cancellationToken);

            if (definition == null)
            {
                throw new InvalidOperationException($"No active workflow definition for code '{workflowCode}'.");
            }

            var initialState = definition.States.Single(s => s.IsInitial);
            var instance = WorkflowInstance.Start(
                definition, initialState, entityTypeName, entityId, _workingYear.AcademicYearId, businessKey, routingValue);

            _db.WorkflowInstances.Add(instance);
            await _db.SaveChangesAsync(cancellationToken);
            return instance;
        }

        public async Task<WorkflowTransitionResult> ExecuteAsync(
            int instanceId,
            WorkflowActionType action,
            string? reason = null,
            WorkflowRecordScope? recordScope = null,
            CancellationToken cancellationToken = default)
        {
            var instance = await _db.WorkflowInstances.SingleAsync(i => i.Id == instanceId, cancellationToken);

            // The instance's pinned version, not the latest (BR-WF-008).
            var definition = await _db.WorkflowDefinitions
                .Include(d => d.States)
                .Include(d => d.Transitions)
                .SingleAsync(d => d.Id == instance.WorkflowDefinitionId, cancellationToken);

            var transition = WorkflowEngine.FindTransition(definition, instance, action);

            EffectiveScope? scope = null;
            if (transition.PermissionModuleCode != null && transition.PermissionScreenCode != null && transition.PermissionAction != null)
            {
                scope = await _permissions.GetEffectiveScopeAsync(
                    transition.PermissionModuleCode, transition.PermissionScreenCode, transition.PermissionAction.Value, cancellationToken);
            }

            var roleIds = await _db.RoleAssignments
                .Where(a => a.UserAccountId == _currentUser.UserId)
                .Select(a => a.RoleId)
                .ToListAsync(cancellationToken);

            var actor = new WorkflowActor(_currentUser.UserId, roleIds, scope);
            var result = WorkflowEngine.Authorize(definition, instance, transition, actor, reason, recordScope);

            instance.ApplyTransition(result.Transition, result.ToState, actor.UserId);

            _db.WorkflowSteps.Add(new WorkflowStep
            {
                WorkflowInstanceId = instance.Id,
                FromStateId = result.FromState.Id,
                ToStateId = result.ToState.Id,
                Action = action,
                ActorUserId = actor.UserId,
                Reason = reason,
                OccurredAtUtc = _clock.UtcNow,
            });

            _auditEvents.Log(AuditAction.WorkflowStep, instance.EntityTypeName, instance.EntityId, instance.AuditBusinessKey, reason);

            if (result.Transition.TriggersFinalEffect)
            {
                foreach (var effect in _finalEffects.Where(e => e.WorkflowCode == definition.Code))
                {
                    await effect.ApplyAsync(instance, cancellationToken);
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            return result;
        }
    }
}
