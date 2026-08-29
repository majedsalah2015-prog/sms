using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Security;

namespace Sms.Application.Security
{
    /// <summary>
    /// Module 36's role designer (doc 06 §4): what each role may do, and who holds it.
    /// <para>
    /// Until this existed, <see cref="ScreenCatalog"/> was catalogued into <c>sec.Permission</c> and
    /// granted by a seeder, and there was no way to change any of it without SQL —
    /// <c>RoleTemplateSeedContributor</c> shipped 21 roles and said the role designer was deferred.
    /// Everything below is that screen's engine.
    /// </para>
    /// <para>
    /// <b>The one invariant this service enforces beyond ordinary validation:</b> no operation may
    /// leave the school with nobody able to administer permissions. Every path that could —
    /// narrowing a role, deactivating one, revoking an assignment — is checked against the state it
    /// would produce, and refused before it is saved. A permission system that can lock every
    /// administrator out is one SQL script away from an outage, and it is the one mistake nobody can
    /// undo from inside the product.
    /// </para>
    /// </summary>
    public interface ISecurityAdmin
    {
        /// <summary>Every role, active first, with how many grants each carries and how many people hold it.</summary>
        Task<IReadOnlyList<RoleSummary>> ListRolesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// One role with the full catalogue beside it — every screen the product defines, each verb
        /// marked granted or not. The catalogue is the source of what <i>can</i> be granted, so a
        /// screen added to <see cref="ScreenCatalog"/> appears here without a migration.
        /// </summary>
        Task<RoleDetail> GetRoleAsync(int roleId, CancellationToken cancellationToken = default);

        Task<Role> CreateRoleAsync(RoleDefinition definition, CancellationToken cancellationToken = default);

        Task UpdateRoleAsync(int roleId, RoleDefinition definition, CancellationToken cancellationToken = default);

        /// <summary>
        /// Soft-deactivates the role (BR-GLB-005 — there is no delete). Refuses if it would leave
        /// nobody able to administer permissions.
        /// </summary>
        Task DeactivateRoleAsync(int roleId, CancellationToken cancellationToken = default);

        Task ReactivateRoleAsync(int roleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces this role's grants with exactly <paramref name="granted"/>. Whole-set rather than
        /// add/remove because that is what the screen posts — a checkbox grid submits its state, not
        /// its diff, and reconstructing a diff from a form is how a concurrent edit silently wins.
        /// <para>
        /// A triple the catalogue does not define is refused rather than ignored: it would create a
        /// <c>sec.Permission</c> row no screen can ever check, which reads on the role designer as
        /// access that does not exist.
        /// </para>
        /// </summary>
        Task SetRolePermissionsAsync(
            int roleId, IReadOnlyCollection<PermissionKey> granted, CancellationToken cancellationToken = default);

        /// <summary>
        /// Accounts and the roles each one holds. <paramref name="search"/> matches the user name,
        /// the person's name in either language, and the file number they are registered under.
        /// <para>
        /// <paramref name="includeInactive"/> reads past the soft-active filter. Off by default,
        /// because the everyday question here is who can reach what today; on, it is the only place
        /// in the product that shows a deactivated account at all.
        /// </para>
        /// </summary>
        Task<IReadOnlyList<UserRoleSummary>> ListUserRolesAsync(
            string? search = null, bool includeInactive = false, CancellationToken cancellationToken = default);

        /// <summary>Idempotent: an assignment that exists but was revoked is reactivated rather than duplicated.</summary>
        Task<RoleAssignment> AssignRoleAsync(int userAccountId, int roleId, CancellationToken cancellationToken = default);

        /// <summary>Refuses if it would leave nobody able to administer permissions.</summary>
        Task RevokeRoleAsync(int userAccountId, int roleId, CancellationToken cancellationToken = default);
    }

    /// <summary>One catalogued permission: the triple <c>sec.Permission</c> is keyed on.</summary>
    public sealed record PermissionKey(string ModuleCode, string ScreenCode, ActionVerb Action);

    /// <summary>The editable properties of a role. Code is set at creation and never changes — grants, assignments and the seeder all key on it.</summary>
    public sealed record RoleDefinition(
        string Code, string NameAr, string NameEn, bool RequireTwoFactor, bool EnforceSingleSession);

    public sealed record RoleSummary(
        int Id, string Code, string NameAr, string NameEn, bool IsActive,
        bool RequireTwoFactor, bool EnforceSingleSession,
        int GrantCount, int HolderCount,
        bool CanAdministerPermissions);

    /// <summary>A screen as the designer shows it: its catalogue entry, and which of its verbs this role holds.</summary>
    public sealed record RoleScreenGrants(
        string ModuleCode, string ScreenCode, string TitleEn, string TitleAr,
        IReadOnlyList<ActionVerb> AvailableVerbs, IReadOnlyList<ActionVerb> GrantedVerbs);

    public sealed record RoleDetail(RoleSummary Role, IReadOnlyList<RoleScreenGrants> Screens);

    /// <summary>
    /// One account as the assignment screen lists it.
    /// <para>
    /// The person's own name and file number travel with it because a user name answers nobody's
    /// question — a school office does not know who <c>emp-1042</c> is, and an administrator handing
    /// out a role has a person in mind rather than a login. They are null for an
    /// <see cref="AccountType.System"/> account, which belongs to no person by design.
    /// </para>
    /// </summary>
    public sealed record UserRoleSummary(
        int UserAccountId, string UserName, AccountType AccountType, bool IsActive,
        string? PersonNameAr, string? PersonNameEn, string? PersonReference,
        IReadOnlyList<UserRoleGrant> Roles);

    public sealed record UserRoleGrant(int RoleId, string Code, string NameAr, string NameEn, bool CanAdministerPermissions);
}
