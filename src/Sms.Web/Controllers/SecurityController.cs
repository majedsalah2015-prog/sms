using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Sms.Application.Audit;
using Sms.Application.Common.Exceptions;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// Module 36's role designer — the screen doc 06 §4.3 assumed and
    /// <c>RoleTemplateSeedContributor</c> deferred. Until it existed, the 21 seeded roles and every
    /// grant on them could only be changed with SQL.
    /// <para>
    /// Note the verbs: <c>Edit</c> renames a role and sets its 2FA/session policy;
    /// <c>Configure</c> is what changes the grants themselves. They are separate permissions because
    /// they are separate authorities — and because Configure here can reach every other permission
    /// in the product, which is exactly the one worth being able to withhold on its own.
    /// </para>
    /// </summary>
    [Route("security")]
    public class SecurityController : Controller
    {
        private static bool IsArabic => System.Globalization.CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        private readonly ISecurityAdmin _security;
        private readonly IUserAccountAdmin _accounts;
        private readonly IPermissionService _permissions;
        private readonly IAuditContext _audit;

        public SecurityController(
            ISecurityAdmin security, IUserAccountAdmin accounts, IPermissionService permissions, IAuditContext audit)
        {
            _security = security;
            _accounts = accounts;
            _permissions = permissions;
            _audit = audit;
        }

        // ------------------------------------------------------------------ roles

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.SystemAdministration, ScreenCatalog.SystemAdministration.Roles, ActionVerb.View)]
        public async Task<IActionResult> Index(bool includeInactive = false)
        {
            var roles = await _security.ListRolesAsync(includeInactive, HttpContext.RequestAborted);
            return View(new RoleListViewModel { Roles = roles, IncludeInactive = includeInactive });
        }

        [HttpGet("roles/{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.SystemAdministration, ScreenCatalog.SystemAdministration.Roles, ActionVerb.View)]
        public async Task<IActionResult> Role(int id)
        {
            try
            {
                return View(await _security.GetRoleAsync(id, HttpContext.RequestAborted));
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }

        [HttpPost("roles")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.SystemAdministration, ScreenCatalog.SystemAdministration.Roles, ActionVerb.Create)]
        public async Task<IActionResult> Create(string code, string nameAr, string nameEn, bool requireTwoFactor, bool enforceSingleSession)
        {
            try
            {
                var role = await _security.CreateRoleAsync(
                    new RoleDefinition(code, nameAr, nameEn, requireTwoFactor, enforceSingleSession),
                    HttpContext.RequestAborted);
                return RedirectToAction(nameof(Role), new { id = role.Id });
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost("roles/{id:int}")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.SystemAdministration, ScreenCatalog.SystemAdministration.Roles, ActionVerb.Edit)]
        public async Task<IActionResult> Update(int id, string code, string nameAr, string nameEn, bool requireTwoFactor, bool enforceSingleSession, string? reason)
        {
            _audit.Reason = reason;
            try
            {
                await _security.UpdateRoleAsync(
                    id, new RoleDefinition(code, nameAr, nameEn, requireTwoFactor, enforceSingleSession), HttpContext.RequestAborted);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Role), new { id });
        }

        [HttpPost("roles/{id:int}/deactivate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.SystemAdministration, ScreenCatalog.SystemAdministration.Roles, ActionVerb.Deactivate)]
        public async Task<IActionResult> Deactivate(int id, string? reason)
        {
            _audit.Reason = reason;
            try
            {
                await _security.DeactivateRoleAsync(id, HttpContext.RequestAborted);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("roles/{id:int}/reactivate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.SystemAdministration, ScreenCatalog.SystemAdministration.Roles, ActionVerb.Deactivate)]
        public async Task<IActionResult> Reactivate(int id)
        {
            try
            {
                await _security.ReactivateRoleAsync(id, HttpContext.RequestAborted);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Index), new { includeInactive = true });
        }

        /// <summary>
        /// The permission grid posts its whole state, not a diff: <c>granted</c> carries every ticked
        /// box as "MODULE/Screen/Verb". An unticked box is absent, which is what makes the post
        /// idempotent and a concurrent edit visible rather than merged.
        /// </summary>
        [HttpPost("roles/{id:int}/permissions")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.SystemAdministration, ScreenCatalog.SystemAdministration.Roles, ActionVerb.Configure)]
        public async Task<IActionResult> SetPermissions(int id, string[]? granted, string? reason)
        {
            _audit.Reason = reason;
            try
            {
                await _security.SetRolePermissionsAsync(id, PermissionGrid.Parse(granted), HttpContext.RequestAborted);
                TempData["Message"] = "Permissions saved.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Role), new { id });
        }

        // ------------------------------------------------------------------ assignments

        [HttpGet("users")]
        [RequirePermission(ScreenCatalog.Modules.SystemAdministration, ScreenCatalog.SystemAdministration.UserRoles, ActionVerb.View)]
        public async Task<IActionResult> Users(string? q = null)
        {
            var users = await _security.ListUserRolesAsync(q, HttpContext.RequestAborted);
            var roles = await _security.ListRolesAsync(false, HttpContext.RequestAborted);

            // Shown once, immediately after provisioning, and never again: the password is not stored
            // anywhere it could be read back from (BR-SEC-005). It rides the redirect in TempData —
            // an encrypted, HttpOnly cookie consumed on this read — rather than in the URL, and the
            // redirect is what stops a refresh from re-posting the form.
            ProvisionedAccount? provisioned = null;
            if (TempData["ProvisionedUserName"] is string name
                && TempData["ProvisionedPassword"] is string password
                && TempData["ProvisionedUserId"] is int id)
            {
                provisioned = new ProvisionedAccount(id, name, password);
            }

            return View(new UserRoleListViewModel
            {
                Users = users,
                Roles = roles,
                Search = q,
                JustProvisioned = provisioned,
                CanProvision = await _permissions.HasPermissionAsync(
                    ScreenCatalog.Modules.SystemAdministration,
                    ScreenCatalog.SystemAdministration.Users,
                    ActionVerb.Create,
                    HttpContext.RequestAborted),
                CanResetPassword = await _permissions.HasPermissionAsync(
                    ScreenCatalog.Modules.SystemAdministration,
                    ScreenCatalog.SystemAdministration.Users,
                    ActionVerb.Edit,
                    HttpContext.RequestAborted),
            });
        }

        // ------------------------------------------------------------------ provisioning

        /// <summary>
        /// doc 06 §8's "users list &amp; lifecycle", the create half of it. An account exists only
        /// against a person (BR-GLB-002), so the form picks one from the people who have none rather
        /// than offering an empty name field — there is no free-standing login in this product.
        /// <para>
        /// <b>Not built here:</b> the directory itself with its lifecycle actions — deactivate,
        /// reactivate, reset a password, clear a lockout, end a session, the dormant queue. The port
        /// behind this screen (<see cref="IUserAccountAdmin"/>) implements all of them; what is
        /// missing is their screen, and <c>SYS/Users</c> catalogues only <c>Create</c> until it
        /// exists.
        /// </para>
        /// </summary>
        [HttpGet("users/new")]
        [RequirePermission(ScreenCatalog.Modules.SystemAdministration, ScreenCatalog.SystemAdministration.Users, ActionVerb.Create)]
        public async Task<IActionResult> NewUser(
            ProvisionableAccountType accountType = ProvisionableAccountType.Staff,
            string? personSearch = null,
            int? personId = null)
            => View(await BuildNewUserAsync(new NewUserViewModel
            {
                AccountType = accountType,
                PersonSearch = personSearch,
                PersonId = personId,
            }));

        [HttpPost("users/new")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.SystemAdministration, ScreenCatalog.SystemAdministration.Users, ActionVerb.Create)]
        public async Task<IActionResult> CreateUser(NewUserViewModel form)
        {
            if (!Enum.IsDefined(typeof(ProvisionableAccountType), form.AccountType))
            {
                form.AccountType = ProvisionableAccountType.Staff;
            }

            if (form.PersonId is not { } personId)
            {
                ModelState.AddModelError(
                    nameof(form.PersonId),
                    T("Choose the person this account belongs to.", "اختر الشخص الذي يعود إليه هذا الحساب."));
                return View(nameof(NewUser), await BuildNewUserAsync(form));
            }

            // A blank name is not an error: the screen proposes one from the person's own reference
            // number, and a clerk who does not want to argue with it should be able to leave it be.
            var userName = form.UserName;
            if (string.IsNullOrWhiteSpace(userName))
            {
                // The same list the person was chosen from, so the proposal is the one the screen
                // showed. It is empty when the person's reference number yields nothing typeable —
                // UserNameRules.Propose says so rather than offering a bare prefix everyone collides
                // on — and then the screen asks for a name instead of inventing one.
                userName = (await _accounts.ListProvisionableAsync(
                        form.AccountType, form.PersonSearch, HttpContext.RequestAborted))
                    .FirstOrDefault(p => p.PersonId == personId)?.SuggestedUserName;
            }

            if (string.IsNullOrWhiteSpace(userName))
            {
                ModelState.AddModelError(
                    nameof(form.UserName),
                    T("This person has no reference number to build a user name from — type one.",
                      "لا يوجد رقم مرجعي لهذا الشخص يُبنى منه اسم مستخدم — اكتب اسماً."));
                return View(nameof(NewUser), await BuildNewUserAsync(form));
            }

            try
            {
                var provisioned = await _accounts.ProvisionAsync(
                    new NewUserAccount(form.AccountType, personId, userName),
                    HttpContext.RequestAborted);

                TempData["ProvisionedUserId"] = provisioned.UserAccountId;
                TempData["ProvisionedUserName"] = provisioned.UserName;
                TempData["ProvisionedPassword"] = provisioned.TemporaryPassword;
                TempData["Message"] = T(
                    $"Account {provisioned.UserName} created. Give it a role below — an account with no role reaches nothing.",
                    $"أُنشئ الحساب {provisioned.UserName}. امنحه دوراً أدناه — فالحساب بلا دور لا يصل إلى شيء.");

                return RedirectToAction(nameof(Users), new { q = provisioned.UserName });
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                ModelState.AddModelError(string.Empty, UserMessage.For(ex, IsArabic));
                return View(nameof(NewUser), await BuildNewUserAsync(form));
            }
        }

        /// <summary>
        /// BR-SEC-005's other half: the password an administrator issues when somebody cannot get
        /// in. It is minted here, shown once on the list, and forces a change at the next sign-in —
        /// there is no screen anywhere that can show it a second time, and no field an administrator
        /// can type one into.
        /// <para>
        /// <c>Edit</c> rather than <c>Create</c>: handing a colleague a new password is an everyday
        /// act of a front office, and deciding who has an account is not.
        /// </para>
        /// </summary>
        [HttpPost("users/{userAccountId:int}/reset-password")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.SystemAdministration, ScreenCatalog.SystemAdministration.Users, ActionVerb.Edit)]
        public async Task<IActionResult> ResetPassword(int userAccountId, string? q)
        {
            try
            {
                var password = await _accounts.ResetPasswordAsync(userAccountId, HttpContext.RequestAborted);
                var account = await _accounts.GetAsync(userAccountId, HttpContext.RequestAborted);

                TempData["ProvisionedUserId"] = userAccountId;
                TempData["ProvisionedUserName"] = account?.Account.UserName ?? string.Empty;
                TempData["ProvisionedPassword"] = password;
                TempData["Message"] = T(
                    "A new one-time password was issued. The holder must change it at their next sign-in.",
                    "صدرت كلمة مرور جديدة لمرة واحدة. وعلى صاحبها تغييرها عند دخوله التالي.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Users), new { q });
        }

        private async Task<NewUserViewModel> BuildNewUserAsync(NewUserViewModel form)
        {
            // A hand-made request can carry an account type the enum does not define, and the port
            // would answer that with an English ArgumentOutOfRangeException. The screen only ever
            // offers three, so anything else is read as the default rather than surfaced as a fault.
            if (!Enum.IsDefined(typeof(ProvisionableAccountType), form.AccountType))
            {
                form.AccountType = ProvisionableAccountType.Staff;
            }

            form.People = await _accounts.ListProvisionableAsync(
                form.AccountType, form.PersonSearch, HttpContext.RequestAborted);

            // The port caps the picker rather than returning a register of two thousand students, so
            // a full page is a page that may be hiding somebody. Say so instead of implying it is all.
            form.PickerIsCapped = form.People.Count >= 50;
            return form;
        }

        [HttpPost("users/{userAccountId:int}/roles")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.SystemAdministration, ScreenCatalog.SystemAdministration.UserRoles, ActionVerb.Create)]
        public async Task<IActionResult> Assign(int userAccountId, int roleId, string? q)
        {
            try
            {
                await _security.AssignRoleAsync(userAccountId, roleId, HttpContext.RequestAborted);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Users), new { q });
        }

        [HttpPost("users/{userAccountId:int}/roles/{roleId:int}/revoke")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.SystemAdministration, ScreenCatalog.SystemAdministration.UserRoles, ActionVerb.Deactivate)]
        public async Task<IActionResult> Revoke(int userAccountId, int roleId, string? q)
        {
            try
            {
                await _security.RevokeRoleAsync(userAccountId, roleId, HttpContext.RequestAborted);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Users), new { q });
        }
    }
}
