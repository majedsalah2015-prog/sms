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
