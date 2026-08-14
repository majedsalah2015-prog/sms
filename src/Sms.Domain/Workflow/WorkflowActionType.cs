namespace Sms.Domain.Workflow
{
    /// <summary>
    /// The standard transition actions of doc 05 §2. Modules bind transitions
    /// to these; new actions require justification in the module doc (§3 rules
    /// of the vocabulary). SMALLINT-mapped (DB/01 §5).
    /// </summary>
    public enum WorkflowActionType : short
    {
        Submit = 1,
        Approve = 2,
        Reject = 3,
        Return = 4,
        Cancel = 5,
        Complete = 6,
    }
}
