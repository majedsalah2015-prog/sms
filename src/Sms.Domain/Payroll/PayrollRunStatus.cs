namespace Sms.Domain.Payroll
{
    /// <summary>
    /// مسير الرواتب — the life of one month's payroll (owner request, 2026-08-28).
    /// <para>
    /// Draft is the only state in which lines may be generated, adjusted or removed. Approved
    /// freezes the arithmetic — the amounts a school signs off on are the amounts it pays.
    /// Paid is terminal and is the moment the advance instalments this run carried are actually
    /// consumed: money left the school, so the repayment it recovered is real.
    /// </para>
    /// <para>
    /// Cancelled exists because BR-GLB-005 forbids deleting a run that was opened by mistake.
    /// A cancelled run keeps its number and its lines and is reachable forever; it simply stops
    /// counting, and frees the month so a correct run can be opened for it.
    /// </para>
    /// </summary>
    public enum PayrollRunStatus : short
    {
        Draft = 1,
        Approved = 2,
        Paid = 3,
        Cancelled = 4,
    }
}
