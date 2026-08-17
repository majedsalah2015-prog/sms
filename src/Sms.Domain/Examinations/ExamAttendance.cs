using Sms.Domain.Attendance;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Examinations
{
    /// <summary>
    /// core.ExamAttendance (doc/Modules/16 §7, BR-EXM-006): student x
    /// sitting. Reuses E-301's <see cref="AttendanceStatus"/> rather than a
    /// parallel taxonomy (doc explicitly ties exam absence classification
    /// to "Module 14 taxonomy") — only Present/AbsentExcused/
    /// AbsentUnexcused/MedicalLeave are meaningful here; Late/Permission/
    /// EarlyLeave/Exempted are unused by this entity but kept for the one
    /// shared enum rather than forking a subset type.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class ExamAttendance : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int ExamSittingId { get; set; }

        public int EnrollmentId { get; set; }

        public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    }
}
