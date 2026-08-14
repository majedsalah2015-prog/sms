using Sms.Domain.Workflow;

namespace Sms.Application.Workflow
{
    /// <summary>An authorized transition, ready to apply.</summary>
    public sealed class WorkflowTransitionResult
    {
        public WorkflowTransitionResult(WorkflowTransition transition, WorkflowState fromState, WorkflowState toState, string? raisedEvent)
        {
            Transition = transition;
            FromState = fromState;
            ToState = toState;
            RaisedEvent = raisedEvent;
        }

        public WorkflowTransition Transition { get; }

        public WorkflowState FromState { get; }

        public WorkflowState ToState { get; }

        /// <summary>Doc 05 §8 event name for notification routing; null when none applies.</summary>
        public string? RaisedEvent { get; }

        public bool IsFinal => ToState.IsFinal;
    }
}
