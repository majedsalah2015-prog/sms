using Sms.Application.Audit;

namespace Sms.Infrastructure.Audit
{
    /// <summary>
    /// Scoped ambient audit metadata. The web layer stamps screen + client IP
    /// per request; operations set <see cref="Reason"/> before saving.
    /// </summary>
    public class AuditContext : IAuditContext
    {
        public string? SourceScreen { get; set; }

        public string? ClientIp { get; set; }

        public string? Reason { get; set; }
    }
}
