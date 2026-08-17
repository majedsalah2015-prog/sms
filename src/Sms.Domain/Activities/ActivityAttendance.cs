using Sms.Domain.Attendance;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Activities
{
    /// <summary>ppl.ActivityAttendance (doc/Modules/29 §7, BR-ACT-003): reuses E-301's AttendanceStatus rather than a parallel taxonomy, same reuse call as E-402's ExamAttendance. Reconciliation with Module 14's in-school attendance (present-at-school but absent-at-activity flag) is deferred.</summary>
    [Audited(AuditTier.T2)]
    public class ActivityAttendance : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int ActivitySessionId { get; set; }

        public int ProgramEnrollmentId { get; set; }

        public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    }
}
