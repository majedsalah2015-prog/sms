namespace Sms.Erp.Bridge
{
    /// <summary>
    /// Type anchor for assembly scanning and for the architecture tests that
    /// assert the direction of the dependency: <c>Sms.Domain</c>,
    /// <c>Sms.Application</c> and <c>Sms.Infrastructure</c> must never reference
    /// an <c>ERP2028.*</c> assembly. Only this project and the composition root
    /// may (docs/Integration/01-Embedded-Accounting-Plan.md §3).
    /// </summary>
    public static class AssemblyMarker
    {
    }
}
