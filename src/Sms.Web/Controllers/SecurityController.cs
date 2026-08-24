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

        private readonly ISecurityAdmin _security;
        private readonly IAuditContext _audit;

        public SecurityController(ISecurityAdmin security, IAuditContext audit)
        {
            _security = security;
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
            return View(new UserRoleListViewModel { Users = users, Roles = roles, Search = q });
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
