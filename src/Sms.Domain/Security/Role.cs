using System.Collections.Generic;
using Sms.Domain.Common;

namespace Sms.Domain.Security
{
    /// <summary>
    /// sec.Role — a named set of permissions (doc 06 §4.3). Seeded templates are
    /// adjustable per school, so roles are tenant-owned master data.
    /// </summary>
    public class Role : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public string Code { get; set; } = string.Empty;

        public LocalizedName Name { get; set; } = new();

        public bool IsActive { get; set; } = true;

        public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
    }
}
