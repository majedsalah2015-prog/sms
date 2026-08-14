using System;
using Sms.Application.Common.Exceptions;
using Sms.Application.Security;
using Sms.Application.Workflow;
using Sms.Domain.Workflow;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Workflow
{
    /// <summary>
    /// E-005 engine rules over an in-memory WF-04-shaped definition (P4
    /// discount grant, doc 05 §4/§5): Draft → Submitted; ≤10% Finance approves
    /// final; &gt;10% routes through Principal review (P3 chain via an
    /// intermediate state — the same shape emulates P5 sequentially).
    /// </summary>
    public class WorkflowEngineTests
    {
        private const int FinanceRole = 5;
        private const int PrincipalRole = 6;
        private const int SubmitterId = 7;
        private const int ApproverId = 8;

        private static class States
        {
            public const int Draft = 1;
            public const int Submitted = 2;
            public const int PrincipalReview = 3;
            public const int Approved = 4;
            public const int Rejected = 5;
            public const int Cancelled = 6;
        }

        private static WorkflowDefinition BuildDefinition()
        {
            var definition = new WorkflowDefinition { Id = 10, SchoolId = 1, Code = "WF-04", Version = 1, EntityTypeName = "DiscountGrant" };

            definition.States.AddRange(new[]
            {
                new WorkflowState { Id = States.Draft, WorkflowDefinitionId = 10, Code = "Draft", IsInitial = true },
                new WorkflowState { Id = States.Submitted, WorkflowDefinitionId = 10, Code = "Submitted" },
                new WorkflowState { Id = States.PrincipalReview, WorkflowDefinitionId = 10, Code = "UnderReview" },
                new WorkflowState { Id = States.Approved, WorkflowDefinitionId = 10, Code = "Approved", IsFinal = true },
                new WorkflowState { Id = States.Rejected, WorkflowDefinitionId = 10, Code = "Rejected", IsFinal = true },
                new WorkflowState { Id = States.Cancelled, WorkflowDefinitionId = 10, Code = "Cancelled", IsFinal = true },
            });

            definition.Transitions.AddRange(new[]
            {
                new WorkflowTransition { Id = 101, WorkflowDefinitionId = 10, FromStateId = States.Draft, ToStateId = States.Submitted, Action = WorkflowActionType.Submit },
                new WorkflowTransition { Id = 102, WorkflowDefinitionId = 10, FromStateId = States.Submitted, ToStateId = States.Approved, Action = WorkflowActionType.Approve, RequiredRoleId = FinanceRole, MaxRoutingValue = 10m, TriggersFinalEffect = true },
                new WorkflowTransition { Id = 103, WorkflowDefinitionId = 10, FromStateId = States.Submitted, ToStateId = States.PrincipalReview, Action = WorkflowActionType.Approve, RequiredRoleId = FinanceRole, MinRoutingValue = 10m },
                new WorkflowTransition { Id = 104, WorkflowDefinitionId = 10, FromStateId = States.PrincipalReview, ToStateId = States.Approved, Action = WorkflowActionType.Approve, RequiredRoleId = PrincipalRole, TriggersFinalEffect = true, PermissionModuleCode = "DIS", PermissionScreenCode = "DiscountGrants", PermissionAction = Sms.Domain.Security.ActionVerb.Approve },
                new WorkflowTransition { Id = 105, WorkflowDefinitionId = 10, FromStateId = States.Submitted, ToStateId = States.Rejected, Action = WorkflowActionType.Reject, RequiredRoleId = FinanceRole },
                new WorkflowTransition { Id = 106, WorkflowDefinitionId = 10, FromStateId = States.Submitted, ToStateId = States.Draft, Action = WorkflowActionType.Return, RequiredRoleId = FinanceRole },
                new WorkflowTransition { Id = 107, WorkflowDefinitionId = 10, FromStateId = States.Draft, ToStateId = States.Cancelled, Action = WorkflowActionType.Cancel },
            });

            return definition;
        }

        private static WorkflowInstance NewInstance(WorkflowDefinition definition, decimal? routingValue = null)
        {
            var initial = definition.States.Find(s => s.IsInitial)!;
            return WorkflowInstance.Start(definition, initial, "DiscountGrant", 55, academicYearId: 2027, businessKey: "DSC-1", routingValue: routingValue);
        }

        private static WorkflowInstance SubmittedInstance(WorkflowDefinition definition, decimal? routingValue = null)
        {
            var instance = NewInstance(definition, routingValue);
            var result = WorkflowEngine.Decide(definition, instance, WorkflowActionType.Submit, new WorkflowActor(SubmitterId, Array.Empty<int>()), reason: null);
            instance.ApplyTransition(result.Transition, result.ToState, SubmitterId);
            return instance;
        }

        [Fact]
        [BusinessRule("BR-WF-001")]
        public void Submit_is_a_defined_movement_and_records_the_submitter()
        {
            var definition = BuildDefinition();
            var instance = SubmittedInstance(definition);

            Assert.Equal(States.Submitted, instance.CurrentStateId);
            Assert.Equal(SubmitterId, instance.SubmittedByUserId);
        }

        [Fact]
        [BusinessRule("BR-WF-001")]
        public void Undefined_movements_are_impossible()
        {
            var definition = BuildDefinition();
            var instance = NewInstance(definition);
            var actor = new WorkflowActor(ApproverId, new[] { FinanceRole });

            // No Approve transition leaves Draft.
            Assert.Throws<WorkflowTransitionNotAllowedException>(
                () => WorkflowEngine.Decide(definition, instance, WorkflowActionType.Approve, actor, null));
        }

        [Fact]
        [BusinessRule("BR-WF-005")]
        public void Threshold_routing_selects_the_chain_by_value()
        {
            var definition = BuildDefinition();
            var actor = new WorkflowActor(ApproverId, new[] { FinanceRole });

            var small = SubmittedInstance(definition, routingValue: 8m);
            var smallResult = WorkflowEngine.Decide(definition, small, WorkflowActionType.Approve, actor, null);
            Assert.Equal(States.Approved, smallResult.ToState.Id);
            Assert.Equal(WorkflowEvents.Approved, smallResult.RaisedEvent);

            var large = SubmittedInstance(definition, routingValue: 15m);
            var largeResult = WorkflowEngine.Decide(definition, large, WorkflowActionType.Approve, actor, null);
            Assert.Equal(States.PrincipalReview, largeResult.ToState.Id);
            Assert.Equal(WorkflowEvents.StepAssigned, largeResult.RaisedEvent);
        }

        [Fact]
        public void Approver_must_hold_the_required_role()
        {
            var definition = BuildDefinition();
            var instance = SubmittedInstance(definition, routingValue: 8m);
            var wrongRole = new WorkflowActor(ApproverId, new[] { PrincipalRole });

            Assert.Throws<WorkflowActorNotAuthorizedException>(
                () => WorkflowEngine.Decide(definition, instance, WorkflowActionType.Approve, wrongRole, null));
        }

        [Fact]
        [BusinessRule("BR-WF-003")]
        public void Self_approval_is_blocked_even_with_the_role()
        {
            var definition = BuildDefinition();
            var instance = SubmittedInstance(definition, routingValue: 8m);
            var submitterAsApprover = new WorkflowActor(SubmitterId, new[] { FinanceRole });

            Assert.Throws<WorkflowSelfApprovalException>(
                () => WorkflowEngine.Decide(definition, instance, WorkflowActionType.Approve, submitterAsApprover, null));

            var other = new WorkflowActor(ApproverId, new[] { FinanceRole });
            var result = WorkflowEngine.Decide(definition, instance, WorkflowActionType.Approve, other, null);
            Assert.Equal(States.Approved, result.ToState.Id);
        }

        [Fact]
        [BusinessRule("BR-WF-004")]
        public void Approver_scope_must_cover_the_record()
        {
            var definition = BuildDefinition();
            var instance = SubmittedInstance(definition, routingValue: 15m);
            var finance = new WorkflowActor(ApproverId, new[] { FinanceRole });
            var route = WorkflowEngine.Decide(definition, instance, WorkflowActionType.Approve, finance, null);
            instance.ApplyTransition(route.Transition, route.ToState, ApproverId);

            var record = new WorkflowRecordScope(SchoolId: 1, AcademicYearId: 2027);

            // Transition 104 binds a permission: no resolved scope = not granted.
            var noGrant = new WorkflowActor(9, new[] { PrincipalRole }, scope: null);
            Assert.Throws<WorkflowActorNotAuthorizedException>(
                () => WorkflowEngine.Decide(definition, instance, WorkflowActionType.Approve, noGrant, null, record));

            var otherSchool = new WorkflowActor(9, new[] { PrincipalRole }, new EffectiveScope { SchoolIds = new[] { 2 } });
            Assert.Throws<WorkflowActorNotAuthorizedException>(
                () => WorkflowEngine.Decide(definition, instance, WorkflowActionType.Approve, otherSchool, null, record));

            var covering = new WorkflowActor(9, new[] { PrincipalRole }, new EffectiveScope());
            var result = WorkflowEngine.Decide(definition, instance, WorkflowActionType.Approve, covering, null, record);
            Assert.Equal(States.Approved, result.ToState.Id);
        }

        [Fact]
        [BusinessRule("BR-WF-004")]
        public void Own_records_only_approvers_have_no_authority()
        {
            var scope = new EffectiveScope { OwnRecordsOnly = true };

            Assert.False(ScopeCoverage.Covers(scope, new WorkflowRecordScope(1)));
        }

        [Fact]
        [BusinessRule("BR-WF-010")]
        public void Reject_and_return_always_require_a_reason()
        {
            var definition = BuildDefinition();
            var finance = new WorkflowActor(ApproverId, new[] { FinanceRole });

            var instance = SubmittedInstance(definition);
            Assert.Throws<WorkflowReasonRequiredException>(
                () => WorkflowEngine.Decide(definition, instance, WorkflowActionType.Reject, finance, reason: "  "));

            var rejected = WorkflowEngine.Decide(definition, instance, WorkflowActionType.Reject, finance, reason: "Not eligible");
            Assert.Equal(WorkflowEvents.Rejected, rejected.RaisedEvent);

            Assert.Throws<WorkflowReasonRequiredException>(
                () => WorkflowEngine.Decide(definition, instance, WorkflowActionType.Return, finance, reason: null));

            var returned = WorkflowEngine.Decide(definition, instance, WorkflowActionType.Return, finance, reason: "Missing documents");
            instance.ApplyTransition(returned.Transition, returned.ToState, ApproverId);
            Assert.Equal(WorkflowEvents.Returned, returned.RaisedEvent);
            Assert.Equal(States.Draft, instance.CurrentStateId);
            Assert.Equal(1, instance.ReturnCount);
        }

        [Fact]
        [BusinessRule("BR-GLB-032")]
        public void Cancel_requires_a_reason_and_closes_the_instance()
        {
            var definition = BuildDefinition();
            var instance = NewInstance(definition);
            var actor = new WorkflowActor(SubmitterId, Array.Empty<int>());

            Assert.Throws<WorkflowReasonRequiredException>(
                () => WorkflowEngine.Decide(definition, instance, WorkflowActionType.Cancel, actor, reason: null));

            var result = WorkflowEngine.Decide(definition, instance, WorkflowActionType.Cancel, actor, reason: "Duplicate request");
            instance.ApplyTransition(result.Transition, result.ToState, SubmitterId);
            Assert.Equal(WorkflowEvents.Cancelled, result.RaisedEvent);
            Assert.True(instance.IsClosed);
        }

        [Fact]
        [BusinessRule("BR-WF-010")]
        public void Reason_policy_required_applies_to_any_action()
        {
            var definition = BuildDefinition();
            definition.Transitions.Find(t => t.Id == 102)!.ReasonPolicy = ReasonPolicy.Required;
            var instance = SubmittedInstance(definition, routingValue: 8m);
            var finance = new WorkflowActor(ApproverId, new[] { FinanceRole });

            Assert.Throws<WorkflowReasonRequiredException>(
                () => WorkflowEngine.Decide(definition, instance, WorkflowActionType.Approve, finance, reason: null));
        }
    }
}
