using System;
using Sms.Domain.Common;

namespace Sms.Domain.SysAdmin
{
    /// <summary>
    /// ops.MaintenanceWindow (BR-SYS-007): planned-downtime banner + portal
    /// notice scheduling. Platform-wide, not ISchoolScoped — deployment
    /// maintenance affects every tenant on the shared instance.
    /// </summary>
    public class MaintenanceWindow : AuditableEntity
    {
        public DateTime StartUtc { get; set; }

        public DateTime EndUtc { get; set; }

        public string MessageAr { get; set; } = string.Empty;

        public string MessageEn { get; set; } = string.Empty;

        public bool IsEmergency { get; set; }

        public bool IsReadOnlyMode { get; set; }
    }
}
