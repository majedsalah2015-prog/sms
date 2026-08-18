using System;
using Sms.Domain.Common;

namespace Sms.Domain.SysAdmin
{
    /// <summary>
    /// ops.DiagnosticsBundle (BR-SYS-008): metadata record of a support-
    /// diagnostics generation event — the logs excerpt/config snapshot/
    /// health metrics themselves (no personal data) are an infra concern;
    /// this only tracks that a bundle was generated, by whom, and its
    /// reference.
    /// </summary>
    public class DiagnosticsBundle : AuditableEntity
    {
        public int? SchoolId { get; set; }

        public string Reference { get; set; } = string.Empty;

        public DateTime GeneratedAtUtc { get; set; }
    }
}
