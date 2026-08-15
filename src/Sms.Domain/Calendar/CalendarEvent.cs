using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Calendar
{
    /// <summary>core.CalendarEvent (doc/Modules/04 §7, BR-CAL-002): bilingual, categorized, audience-targeted.</summary>
    [Audited(AuditTier.T2)]
    public class CalendarEvent : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public CalendarEventCategory Category { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public CalendarAudience Audience { get; set; } = CalendarAudience.All;

        public bool IsPortalVisible { get; set; }
    }
}
