using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Calendar
{
    /// <summary>
    /// core.CalendarEvent (doc/Modules/04 §7, BR-CAL-002): bilingual, categorized, audience-targeted.
    /// <para>
    /// <see cref="IActivatable"/> is what cancelling an event means here — BR-GLB-005 leaves no
    /// delete, and a published calendar is the wrong place to make a row vanish: parents were
    /// shown the event, and a version snapshot is supposed to answer what the calendar said
    /// (BR-CAL-007). Deliberately <em>not</em> <c>ISoftActiveFiltered</c>: the event manager has to
    /// keep listing what was cancelled (BR-GLB-006 — gone from selection, still on the record), so
    /// the board filters the active ones where it paints instead of the context hiding them
    /// everywhere.
    /// </para>
    /// </summary>
    [Audited(AuditTier.T2)]
    public class CalendarEvent : AuditableEntity, ISchoolScoped, IYearScoped, IActivatable
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

        /// <summary>False once the event is cancelled: it stops painting the board and stops reaching the portal, and stays on the record.</summary>
        public bool IsActive { get; set; } = true;
    }
}
