using Sms.Domain.Audit;

namespace Sms.Application.Audit
{
    /// <summary>
    /// Port for record-level audit events outside the data-change pipeline:
    /// T0 read audit (view/print/export), security events (doc 07 §2 — E-003
    /// slices), workflow steps (E-005) and job runs (E-011). The entry is
    /// attached to the ambient unit of work and persists with the caller's
    /// save — atomic with the business transaction (BR-AUD-003).
    /// </summary>
    public interface IAuditEventWriter
    {
        AuditEntry Log(AuditAction action, string entityType, long? entityId = null, string? businessKey = null, string? reason = null);
    }
}
