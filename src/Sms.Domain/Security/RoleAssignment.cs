using System.Collections.Generic;
using Sms.Domain.Common;

namespace Sms.Domain.Security
{
    /// <summary>
    /// sec.RoleAssignment (docs/Database/03 A11): user ↔ role, unique pair,
    /// carrying the assignment's scope grants (doc 06 §4.2).
    /// </summary>
    public class RoleAssignment : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public int UserAccountId { get; set; }

        public int RoleId { get; set; }

        public Role? Role { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<ScopeGrant> ScopeGrants { get; set; } = new List<ScopeGrant>();
    }
}
