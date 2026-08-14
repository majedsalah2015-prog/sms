using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Workflow
{
    /// <summary>
    /// One running workflow on one record, pinned to the definition version it
    /// started on (BR-WF-008). State moves only through
    /// <see cref="ApplyTransition"/> after the engine authorizes it — there is
    /// no public status setter for any caller (BR-WF-001). T2-audited, so every
    /// state change lands field-level in the tamper-evident store (BR-WF-002).
    /// </summary>
    [Audited(AuditTier.T2)]
    public class WorkflowInstance : AuditableEntity, ISchoolScoped, IYearScoped, IAuditBusinessKey
    {
        private WorkflowInstance()
        {
        }

        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int WorkflowDefinitionId { get; private set; }

        public string EntityTypeName { get; private set; } = string.Empty;

        public long EntityId { get; private set; }

        public string? BusinessKey { get; private set; }

        public int CurrentStateId { get; private set; }

        public int? SubmittedByUserId { get; private set; }

        /// <summary>P4 routing value (e.g. discount %), fixed at start (BR-WF-005).</summary>
        public decimal? RoutingValue { get; private set; }

        /// <summary>Return loops are allowed but counted for audit (doc 05 §3).</summary>
        public int ReturnCount { get; private set; }

        public bool IsClosed { get; private set; }

        public string AuditBusinessKey => BusinessKey ?? $"{EntityTypeName}#{EntityId}";

        public static WorkflowInstance Start(
            WorkflowDefinition definition,
            WorkflowState initialState,
            string entityTypeName,
            long entityId,
            int academicYearId,
            string? businessKey = null,
            decimal? routingValue = null)
        {
            return new WorkflowInstance
            {
                WorkflowDefinitionId = definition.Id,
                CurrentStateId = initialState.Id,
                EntityTypeName = entityTypeName,
                EntityId = entityId,
                AcademicYearId = academicYearId,
                BusinessKey = businessKey,
                RoutingValue = routingValue,
            };
        }

        public void ApplyTransition(WorkflowTransition transition, WorkflowState toState, int actorUserId)
        {
            CurrentStateId = toState.Id;
            IsClosed = toState.IsFinal;

            if (transition.Action == WorkflowActionType.Submit)
            {
                SubmittedByUserId = actorUserId;
            }

            if (transition.Action == WorkflowActionType.Return)
            {
                ReturnCount++;
            }
        }
    }
}
