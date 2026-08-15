using System.Collections.Generic;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Notifications
{
    /// <summary>
    /// msg.Template (doc 09 §2). The editable pointer for one (event, channel)
    /// pair; actual bilingual content lives in <see cref="TemplateVersion"/>
    /// rows so an edit never rewrites what was already sent (BR-NOT-008).
    /// </summary>
    [Audited(AuditTier.T2)]
    public class Template : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        /// <summary>doc 09 §3 catalog code (e.g. "Attendance.StudentAbsent").</summary>
        public string EventCode { get; set; } = string.Empty;

        public NotificationChannel Channel { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>Convenience pointer to the latest version's number; the content itself is on that TemplateVersion row.</summary>
        public int CurrentVersionNumber { get; set; } = 1;

        public List<TemplateVersion> Versions { get; } = new();
    }
}
