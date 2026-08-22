using System.Collections.Generic;
using Sms.Application.Security;

namespace Sms.Web.Models
{
    /// <summary>The role list at <c>/security</c>.</summary>
    public sealed class RoleListViewModel
    {
        public IReadOnlyList<RoleSummary> Roles { get; set; } = new List<RoleSummary>();

        public bool IncludeInactive { get; set; }
    }

    /// <summary>The assignment screen at <c>/security/users</c>: who holds what, and the roles on offer.</summary>
    public sealed class UserRoleListViewModel
    {
        public IReadOnlyList<UserRoleSummary> Users { get; set; } = new List<UserRoleSummary>();

        public IReadOnlyList<RoleSummary> Roles { get; set; } = new List<RoleSummary>();

        public string? Search { get; set; }
    }
}
