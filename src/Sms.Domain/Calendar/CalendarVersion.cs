using System;
using Sms.Domain.Common;

namespace Sms.Domain.Calendar
{
    /// <summary>
    /// core.CalendarVersion (BR-CAL-007): a publish-event marker — "subsequent
    /// edits require re-publication." Does not snapshot CalendarDay/CalendarEvent
    /// rows themselves (that's portal-rendering work, no portal exists yet);
    /// records only that a publish happened and when.
    /// </summary>
    public class CalendarVersion : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int VersionNumber { get; set; }

        public DateTime PublishedAtUtc { get; set; }

        public int PublishedByUserId { get; set; }
    }
}
