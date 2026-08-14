using Sms.Domain.Common;

namespace Sms.Domain.Security
{
    /// <summary>
    /// sec.ScopeGrant (docs/Database/03 A11). ScopeValueId NULL = dynamic
    /// resolution: "own sections/subjects" from Teacher Assignments per year
    /// (doc 06 §4.2), or "active(+preparation) year" for the year dimension —
    /// no manual re-scoping at rollover.
    /// </summary>
    public class ScopeGrant : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int RoleAssignmentId { get; set; }

        public ScopeDimension Dimension { get; set; }

        public int? ScopeValueId { get; set; }
    }
}
