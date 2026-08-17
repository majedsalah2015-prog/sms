namespace Sms.Domain.Certificates
{
    /// <summary>
    /// BR-CRT-008's per-type blocking rule: full clearance / no overdue /
    /// disabled. <see cref="NoOverdue"/> is modeled for schema fidelity but
    /// can't be evaluated yet — E-303's Charge carries no due date
    /// (installment/due-date schedules deferred), so "overdue" is
    /// undefined; CertificateAdmin refuses to define a type with it
    /// rather than silently treating it as one of the other two.
    /// </summary>
    public enum FeeClearanceRule : short
    {
        Disabled = 1,
        NoOverdue = 2,
        FullClearance = 3,
    }
}
