using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Attendance
{
    /// <summary>
    /// core.AttendanceDay (doc/Modules/14 §7, BR-ATD-001..003): one row per
    /// enrolled student per working day — **Daily mode only** in this
    /// slice. Period mode (BR-ATD-001's per-session variant) needs Module
    /// 15's timetable sessions, which don't exist yet — deferred, same
    /// "needs a not-yet-built module" category as this codebase's other
    /// deferrals, not silently skipped. SectionId is captured at record
    /// time from the enrollment's section-membership-as-of-date
    /// (BR-ATD-003) rather than re-derived live, so a later section
    /// transfer doesn't rewrite historical attendance. T1 so that
    /// RequiresAuditReason actually fires on Status corrections
    /// (BR-ATD-007's WF-14 "P2 + reason") — the mechanism only enforces on
    /// T1 classes (Sms.Infrastructure.Audit.AuditCaptor.CollectModified).
    /// </summary>
    [Audited(AuditTier.T1)]
    public class AttendanceDay : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int EnrollmentId { get; set; }

        public int SectionId { get; set; }

        public DateTime Date { get; set; }

        [RequiresAuditReason]
        public AttendanceStatus Status { get; set; }

        public int CapturedByUserId { get; set; }

        /// <summary>BR-ATD-007: locks at day-end closure; post-closure Status edits still go through this same field but the admin service demands a reason once locked.</summary>
        public bool IsLocked { get; set; }
    }
}
