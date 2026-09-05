namespace Sms.Domain.Learning
{
    /// <summary>
    /// doc/Modules/37 §4 paper lifecycle: draft -> blueprint reconciliation
    /// (BR-LRN-008) -> approved (P2, Head of Department) -> scheduled to a
    /// sitting.
    ///
    /// <para>
    /// Reconciliation is not a state here, and that is deliberate: it is a
    /// condition checked at the moment approval is asked for, not a place a paper
    /// waits. A status the paper could sit in while "reconciling" would be a
    /// status nothing ever moved it out of.
    /// </para>
    ///
    /// <para>
    /// <see cref="Approved"/> is not terminal — §8.8 schedules an approved paper
    /// to a sitting, and BR-LRN-016 lets one be withdrawn. What it does close is
    /// editing: the item list of an approved paper is what the head of department
    /// approved, and changing it afterwards would make that approval a signature
    /// on a different document.
    /// </para>
    ///
    /// Starts at 1 per the SMALLINT convention (docs/Database/01).
    /// </summary>
    public enum OnlinePaperStatus
    {
        /// <summary>Being built. Items are added, removed and reordered freely.</summary>
        Draft = 1,

        /// <summary>Sent to the head of department. Still editable back to Draft by withdrawing the request.</summary>
        PendingApproval = 2,

        /// <summary>P2 approved (§4). The item list is frozen from here.</summary>
        Approved = 3,

        /// <summary>BR-LRN-016: withdrawn with a stated reason, never deleted.</summary>
        Withdrawn = 4,
    }
}
