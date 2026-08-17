using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Examinations
{
    /// <summary>core.ExamSitting (doc/Modules/16 §7, BR-EXM-004): exam x room. Seat allocation is simplified to "which students sit here" via ExamAttendance rows rather than individual numbered seats.</summary>
    [Audited(AuditTier.T2)]
    public class ExamSitting : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int ExamId { get; set; }

        public int RoomId { get; set; }
    }
}
