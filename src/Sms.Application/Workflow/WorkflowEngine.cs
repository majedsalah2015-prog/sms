using System.Linq;
using Sms.Application.Common.Exceptions;
using Sms.Domain.Workflow;

namespace Sms.Application.Workflow
{
    /// <summary>
    /// The pure transition rules of doc 05, evaluated against a loaded
    /// definition (states + transitions): allowed movements only (BR-WF-001),
    /// P4 threshold routing (BR-WF-005), role + scope authorization (BR-WF-004),
    /// self-approval block (BR-WF-003), and reason enforcement (BR-WF-010,
    /// BR-GLB-032). Persistence and final effects are the service's job.
    /// </summary>
    public static class WorkflowEngine
    {
        /// <summary>
        /// Selects the transition for the action from the current state,
        /// applying threshold routing. Throws when no defined movement matches
        /// (BR-WF-001) — there is no other way to change state.
        /// </summary>
        public static WorkflowTransition FindTransition(WorkflowDefinition definition, WorkflowInstance instance, WorkflowActionType action)
        {
            var candidates = definition.Transitions
                .Where(t => t.FromStateId == instance.CurrentStateId && t.Action == action)
                .ToList();

            // P4 (BR-WF-005): the range containing the routing value wins;
            // unbounded transitions apply to any value.
            var transition = candidates.FirstOrDefault(t => RoutesTo(t, instance.RoutingValue));
            if (transition == null)
            {
                var fromCode = definition.States.FirstOrDefault(s => s.Id == instance.CurrentStateId)?.Code ?? instance.CurrentStateId.ToString();
                throw new WorkflowTransitionNotAllowedException(definition.Code, fromCode, action);
            }

            return transition;
        }

        /// <summary>
        /// Authorizes the actor for a selected transition and returns the
        /// applicable result. The caller resolves <see cref="WorkflowActor.Scope"/>
        /// from the transition's bound permission before calling.
        /// </summary>
        public static WorkflowTransitionResult Authorize(
            WorkflowDefinition definition,
            WorkflowInstance instance,
            WorkflowTransition transition,
            WorkflowActor actor,
            string? reason,
            WorkflowRecordScope? recordScope = null)
        {
            if (transition.RequiredRoleId is int requiredRole && !actor.RoleIds.Contains(requiredRole))
            {
                throw new WorkflowActorNotAuthorizedException(definition.Code, transition.Action, "the required approver role is not held");
            }

            if (transition.PermissionModuleCode != null)
            {
                // Deny by default (BR-GLB-070): no resolved scope = permission not granted.
                if (actor.Scope == null)
                {
                    throw new WorkflowActorNotAuthorizedException(definition.Code, transition.Action, "the bound permission is not granted");
                }

                var record = recordScope ?? new WorkflowRecordScope(instance.SchoolId, instance.AcademicYearId);
                if (!ScopeCoverage.Covers(actor.Scope, record))
                {
                    throw new WorkflowActorNotAuthorizedException(definition.Code, transition.Action, "the approver's data scopes do not cover the record (BR-WF-004)");
                }
            }

            if (transition.Action == WorkflowActionType.Approve && instance.SubmittedByUserId == actor.UserId)
            {
                throw new WorkflowSelfApprovalException(definition.Code);
            }

            var reasonRequired = transition.ReasonPolicy != ReasonPolicy.Optional
                                 || transition.Action == WorkflowActionType.Reject
                                 || transition.Action == WorkflowActionType.Return
                                 || transition.Action == WorkflowActionType.Cancel;
            if (reasonRequired && string.IsNullOrWhiteSpace(reason))
            {
                throw new WorkflowReasonRequiredException(definition.Code, transition.Action);
            }

            var fromState = definition.States.Single(s => s.Id == transition.FromStateId);
            var toState = definition.States.Single(s => s.Id == transition.ToStateId);

            return new WorkflowTransitionResult(transition, fromState, toState, MapEvent(transition.Action, toState));
        }

        /// <summary>Convenience for callers holding a fully resolved actor.</summary>
        public static WorkflowTransitionResult Decide(
            WorkflowDefinition definition,
            WorkflowInstance instance,
            WorkflowActionType action,
            WorkflowActor actor,
            string? reason,
            WorkflowRecordScope? recordScope = null)
        {
            var transition = FindTransition(definition, instance, action);
            return Authorize(definition, instance, transition, actor, reason, recordScope);
        }

        private static bool RoutesTo(WorkflowTransition transition, decimal? routingValue)
        {
            if (transition.MinRoutingValue == null && transition.MaxRoutingValue == null)
            {
                return true;
            }

            if (routingValue is not decimal value)
            {
                return false;
            }

            return (transition.MinRoutingValue == null || value > transition.MinRoutingValue)
                   && (transition.MaxRoutingValue == null || value <= transition.MaxRoutingValue);
        }

        private static string? MapEvent(WorkflowActionType action, WorkflowState toState)
        {
            return action switch
            {
                WorkflowActionType.Submit => WorkflowEvents.StepAssigned,
                WorkflowActionType.Approve => toState.IsFinal ? WorkflowEvents.Approved : WorkflowEvents.StepAssigned,
                WorkflowActionType.Reject => WorkflowEvents.Rejected,
                WorkflowActionType.Return => WorkflowEvents.Returned,
                WorkflowActionType.Cancel => WorkflowEvents.Cancelled,
                _ => null,
            };
        }
    }
}
