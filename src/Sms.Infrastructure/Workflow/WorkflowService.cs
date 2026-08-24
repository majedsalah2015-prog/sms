using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Exceptions;
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
    /// step trail, audit event, and the module's effect inside ONE owned
    /// transaction, so BR-WF-002 and BR-WF-009 hold or nothing persists.
    /// <para>
    /// The transaction is owned here rather than left to a single
    /// <c>SaveChanges</c> because a module's effect is its own operation — a
    /// discount issues numbered documents and then recomputes an installment
    /// schedule — and those save more than once. <c>SmsDbContext</c> joins an
    /// ambient transaction instead of opening its own, so every save inside
    /// commits with the transition or rolls back with it.
    /// </para>
    /// <para>
    /// Two effect ports, and the difference matters: <see cref="IWorkflowFinalEffect"/>
    /// runs on the transition that completes the chain (BR-WF-009's "approved
    /// but not applied must be impossible"), and <see cref="IWorkflowClosureEffect"/>
    /// runs when a transition ends the instance any other way, so a rejected
    /// request does not leave the module's own row saying "pending" forever.
    /// </para>
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
        private readonly IEnumerable<IWorkflowClosureEffect> _closureEffects;

        public WorkflowService(
            AppDbContext db,
            ICurrentUser currentUser,
            IWorkingYearContext workingYear,
            IClock clock,
            IAuditEventWriter auditEvents,
            IPermissionService permissions,
            IEnumerable<IWorkflowFinalEffect> finalEffects,
            IEnumerable<IWorkflowClosureEffect>? closureEffects = null)
        {
            _db = db;
            _currentUser = currentUser;
            _workingYear = workingYear;
            _clock = clock;
            _auditEvents = auditEvents;
            _permissions = permissions;
            _finalEffects = finalEffects;
            _closureEffects = closureEffects ?? Array.Empty<IWorkflowClosureEffect>();
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
                throw new WorkflowDefinitionMissingException(workflowCode);
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

            // BR-WF-009: the state change, the step trail and the module's effect
            // commit together or not at all. Effects call the owning module's own
            // operation, which saves more than once (a discount issues documents and
            // then reduces a schedule), so one SaveChanges cannot carry them —
            // an owned transaction can, and SmsDbContext joins it rather than
            // opening its own.
            var ownsTransaction = _db.Database.IsRelational() && _db.Database.CurrentTransaction == null;
            var transaction = ownsTransaction ? await _db.Database.BeginTransactionAsync(cancellationToken) : null;
            try
            {
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

                // The instance must be visible to the effect as it will be after the
                // transition — an effect reads the record it is about, and a module
                // operation that saves would otherwise commit around a stale state.
                await _db.SaveChangesAsync(cancellationToken);

                if (result.Transition.TriggersFinalEffect)
                {
                    foreach (var effect in _finalEffects.Where(e => e.WorkflowCode == definition.Code))
                    {
                        await effect.ApplyAsync(instance, cancellationToken);
                    }
                }
                else if (result.ToState.IsFinal)
                {
                    // Rejected or cancelled: the module has to stop calling the record
                    // pending, or the register and the request disagree.
                    foreach (var effect in _closureEffects.Where(e => e.WorkflowCode == definition.Code))
                    {
                        await effect.ApplyAsync(instance, action, reason, cancellationToken);
                    }
                }

                await _db.SaveChangesAsync(cancellationToken);
                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return result;
            }
            catch
            {
                // The transition and its ids are gone with the rollback; leaving them
                // tracked would let a later save in the same scope try to write a row
                // the database never kept.
                _db.ChangeTracker.Clear();
                throw;
            }
            finally
            {
                transaction?.Dispose();
            }
        }
    }
}
