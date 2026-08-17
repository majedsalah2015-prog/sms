using Sms.Domain.Activities;

namespace Sms.Application.Activities
{
    /// <summary>Pure doc/Modules/29 §4 spine: proposer -> Coordinator -> VP approval chain not enforced here, same precedent as every other status-only workflow substitution.</summary>
    public static class ProgramStatusTransitions
    {
        public static bool CanTransition(ProgramStatus from, ProgramStatus to)
        {
            return (from, to) switch
            {
                (ProgramStatus.Proposed, ProgramStatus.Approved) => true,
                (ProgramStatus.Approved, ProgramStatus.Active) => true,
                (ProgramStatus.Active, ProgramStatus.Closed) => true,
                _ => false,
            };
        }
    }
}
