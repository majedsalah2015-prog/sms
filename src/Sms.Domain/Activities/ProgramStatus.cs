namespace Sms.Domain.Activities
{
    /// <summary>doc/Modules/29 §4: proposer -> Coordinator -> VP (P3; trips + Principal P4-style). Approval-chain roles aren't enforced here, same precedent as every other status-only workflow substitution.</summary>
    public enum ProgramStatus : short
    {
        Proposed = 1,
        Approved = 2,
        Active = 3,
        Closed = 4,
    }
}
