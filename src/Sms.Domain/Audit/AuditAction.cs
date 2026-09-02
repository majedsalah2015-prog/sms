namespace Sms.Domain.Audit
{
    /// <summary>
    /// Audit event kinds across the four domains of doc 07 §2 (data, security,
    /// process, system). Stored SMALLINT per DB conventions; values are grouped
    /// per domain so later additions keep their block.
    /// </summary>
    public enum AuditAction : short
    {
        // Data audit
        Create = 1,
        Update = 2,
        StatusChange = 3,

        /// <summary>
        /// A row that was removed rather than deactivated. Rare by design — BR-GLB-005 leaves this
        /// to the few records nothing has ever referenced (an enrollment keyed in error, a section
        /// that never had a member) — and recorded explicitly because <c>AuditCaptor</c> diffs
        /// <c>Added</c> and <c>Modified</c> only: a deleted row otherwise leaves the trail with no
        /// trace that it was ever there, which is the one case where an audit log must not be quiet.
        /// <para>
        /// Extends the open-ended vocabulary in docs/Database/03 §"Action" (Create/Update/
        /// StatusChange/View/Export/Login…), keeping the data-audit block contiguous.
        /// </para>
        /// </summary>
        Delete = 4,

        // T0 read audit (doc 07 §3, BR-SEC-021 exports)
        View = 10,
        Print = 11,
        Export = 12,

        // Security audit (populated by E-003 authentication slices)
        Login = 20,
        LoginFailed = 21,
        Logout = 22,

        // Process audit (populated by E-005 workflow engine, BR-WF-002)
        WorkflowStep = 30,

        // System audit (populated by E-011 jobs infrastructure)
        JobRun = 40,
    }
}
