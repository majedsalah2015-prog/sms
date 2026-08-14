namespace Sms.Domain.Audit
{
    /// <summary>
    /// Supplies the human-readable business key captured on every audit entry
    /// (doc 07 §4) — e.g. a student number — so history stays readable even if
    /// the row is later deactivated.
    /// </summary>
    public interface IAuditBusinessKey
    {
        string AuditBusinessKey { get; }
    }
}
