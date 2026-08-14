using Sms.Domain.Common;

namespace Sms.Domain.Security
{
    /// <summary>sec.RolePermission — grant of a catalog permission to a role.</summary>
    public class RolePermission : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int RoleId { get; set; }

        public int PermissionId { get; set; }

        public Permission? Permission { get; set; }
    }
}
