namespace Sms.Application.Audit
{
    /// <summary>
    /// Ambient per-operation audit metadata (doc 07 §4): the source screen and
    /// client IP set by the web layer, and the user-supplied reason set by the
    /// operation before saving. Correlation ids are generated per save by the
    /// capture pipeline, not carried here.
    /// </summary>
    public interface IAuditContext
    {
        string? SourceScreen { get; set; }

        string? ClientIp { get; set; }

        /// <summary>Mandatory for changes to <c>[RequiresAuditReason]</c> fields on T1 entities.</summary>
        string? Reason { get; set; }
    }
}
