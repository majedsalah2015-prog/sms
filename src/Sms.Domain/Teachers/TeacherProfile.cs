using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Teachers
{
    /// <summary>
    /// core.TeacherProfile (doc/Modules/13 §7, BR-TCH-001): the academic
    /// overlay flag — an employee holding one of these row is "a teacher".
    /// Presence of the row IS the flag; no separate IsTeaching bool.
    /// </summary>
    [Audited(AuditTier.T3)]
    public class TeacherProfile : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int EmployeeId { get; set; }

        /// <summary>BR-TCH-004: e.g. 24 for a full-time contract — school-configurable, no default enforced here.</summary>
        public int MaxWeeklyPeriods { get; set; }
    }
}
