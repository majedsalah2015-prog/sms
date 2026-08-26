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

        /// <summary>
        /// The account just provisioned, carried across the redirect so its one-time password can be
        /// shown once. Nothing stores it and no screen can show it again (BR-SEC-005).
        /// </summary>
        public ProvisionedAccount? JustProvisioned { get; set; }

        /// <summary>Whether this user may open the provisioning form — the button is hidden otherwise (BR-SEC-010).</summary>
        public bool CanProvision { get; set; }

        /// <summary>Whether this user may issue a new one-time password. A separate right, so a front office can do it without being able to create accounts.</summary>
        public bool CanResetPassword { get; set; }
    }

    /// <summary>
    /// The provisioning form at <c>/security/users/new</c>. An account exists only against a person
    /// (BR-GLB-002), so the form is a person picker first and a user name second.
    /// </summary>
    public sealed class NewUserViewModel
    {
        public ProvisionableAccountType AccountType { get; set; } = ProvisionableAccountType.Staff;

        /// <summary>Narrows the picker. Matches the person's name in either language or their reference number.</summary>
        public string? PersonSearch { get; set; }

        public int? PersonId { get; set; }

        /// <summary>Left blank, the chosen person's proposed name is used — so the screen works without typing one.</summary>
        public string? UserName { get; set; }

        public IReadOnlyList<PersonWithoutAccount> People { get; set; } = new List<PersonWithoutAccount>();

        /// <summary>True when the picker is capped rather than complete, so the screen can say so instead of implying the rest do not exist.</summary>
        public bool PickerIsCapped { get; set; }
    }
}
