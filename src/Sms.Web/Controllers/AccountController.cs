using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Web.Security;
using ERP2028.Application.Abstractions.Identity;
using Sms.Erp.Bridge.Identity;
using IAuthenticationService = Sms.Application.Security.IAuthenticationService;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// Login / 2FA / forced password change / logout over E-003's
    /// IAuthenticationService (doc 06 §3). The service owns every rule —
    /// lockout (BR-SEC-002), TOTP (BR-SEC-003), session policy (BR-SEC-004),
    /// first-login change (BR-SEC-005) and the audit events; this controller
    /// only translates outcomes into a cookie principal and screens.
    /// </summary>
    public class AccountController : Controller
    {
        // The half-finished login between password and TOTP is kept in a
        // short-lived separate cookie scheme, never in the main principal.
        private const string TwoFactorScheme = "Sms.TwoFactor";

        private readonly IAuthenticationService _auth;
        private readonly AppDbContext _db;
        private readonly IPermissionService _permissions;

        public AccountController(IAuthenticationService auth, AppDbContext db, IPermissionService permissions)
        {
            _auth = auth;
            _db = db;
            _permissions = permissions;
        }

        [HttpGet]
        [AllowAnonymous]
        [NoPermissionRequired("Signing in is what happens before there is anyone to check permissions for.")]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return LocalRedirect(SafeReturnUrl(returnUrl));
            }

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [NoPermissionRequired("Signing in is what happens before there is anyone to check permissions for.")]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            AuthenticationOutcome outcome;
            try
            {
                outcome = await _auth.AuthenticateAsync(model.UserName.Trim(), model.Password, ClientIp, UserAgent, HttpContext.RequestAborted);
            }
            catch (InvalidCredentialsException)
            {
                ModelState.AddModelError(string.Empty, T("Invalid username or password.", "اسم المستخدم أو كلمة المرور غير صحيحة."));
                return View(model);
            }
            catch (AccountLockedOutException ex)
            {
                var minutes = Math.Max(1, (int)Math.Ceiling((ex.UnlocksAtUtc - DateTime.UtcNow).TotalMinutes));
                ModelState.AddModelError(string.Empty, T($"Account is temporarily locked. Try again in {minutes} minute(s).", $"الحساب مقفل مؤقتاً. حاول مرة أخرى بعد {minutes} دقيقة."));
                return View(model);
            }

            if (outcome.RequiresTwoFactor)
            {
                var pending = new ClaimsIdentity(TwoFactorScheme);
                pending.AddClaim(new Claim(ClaimTypes.NameIdentifier, outcome.UserAccountId.ToString(CultureInfo.InvariantCulture)));
                pending.AddClaim(new Claim("remember", model.RememberMe ? "1" : "0"));
                await HttpContext.SignInAsync(TwoFactorScheme, new ClaimsPrincipal(pending),
                    new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5), IsPersistent = false });
                return RedirectToAction(nameof(TwoFactor), new { returnUrl = model.ReturnUrl });
            }

            await SignInSessionAsync(outcome.Session!, outcome.MustChangePassword, model.RememberMe);
            return outcome.MustChangePassword
                ? RedirectToAction(nameof(ChangePassword))
                : LocalRedirect(SafeReturnUrl(model.ReturnUrl));
        }

        [HttpGet]
        [AllowAnonymous]
        [NoPermissionRequired("Second factor of the sign-in itself.")]
        public async Task<IActionResult> TwoFactor(string? returnUrl = null)
        {
            var pending = await HttpContext.AuthenticateAsync(TwoFactorScheme);
            if (!pending.Succeeded)
            {
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            return View(new TwoFactorViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [NoPermissionRequired("Second factor of the sign-in itself.")]
        public async Task<IActionResult> TwoFactor(TwoFactorViewModel model)
        {
            var pending = await HttpContext.AuthenticateAsync(TwoFactorScheme);
            if (!pending.Succeeded)
            {
                return RedirectToAction(nameof(Login), new { returnUrl = model.ReturnUrl });
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userAccountId = int.Parse(pending.Principal!.FindFirstValue(ClaimTypes.NameIdentifier), CultureInfo.InvariantCulture);
            var remember = pending.Principal!.FindFirst("remember")?.Value == "1";

            UserSession session;
            try
            {
                session = await _auth.CompleteTwoFactorAsync(userAccountId, model.Code.Trim(), ClientIp, UserAgent, HttpContext.RequestAborted);
            }
            catch (InvalidTwoFactorCodeException)
            {
                ModelState.AddModelError(string.Empty, T("The verification code is not valid.", "رمز التحقق غير صحيح."));
                return View(model);
            }

            await HttpContext.SignOutAsync(TwoFactorScheme);
            var mustChange = await _db.UserAccounts.Where(u => u.Id == userAccountId).Select(u => u.MustChangePassword).SingleAsync();
            await SignInSessionAsync(session, mustChange, remember);
            return mustChange ? RedirectToAction(nameof(ChangePassword)) : LocalRedirect(SafeReturnUrl(model.ReturnUrl));
        }

        [HttpGet]
        [NoPermissionRequired("Self-service on one's own credentials; BR-SEC-005 forces it before anything else.")]
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel { IsForced = User.FindFirst(SmsClaimTypes.MustChangePassword)?.Value == "1" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [NoPermissionRequired("Self-service on one's own credentials; BR-SEC-005 forces it before anything else.")]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            model.IsForced = User.FindFirst(SmsClaimTypes.MustChangePassword)?.Value == "1";
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userAccountId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier), CultureInfo.InvariantCulture);
            try
            {
                await _auth.ChangePasswordAsync(userAccountId, model.CurrentPassword, model.NewPassword, HttpContext.RequestAborted);
            }
            catch (InvalidCredentialsException)
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), T("The current password is not correct.", "كلمة المرور الحالية غير صحيحة."));
                return View(model);
            }
            catch (PasswordPolicyViolationException ex)
            {
                foreach (var violation in ex.Violations)
                {
                    ModelState.AddModelError(nameof(model.NewPassword), Describe(violation));
                }

                return View(model);
            }

            // Re-issue the principal without the must-change flag; the session row is unchanged.
            var token = User.FindFirst(SmsClaimTypes.SessionToken)!.Value;
            var session = await _db.UserSessions.SingleAsync(s => s.SessionToken == token);
            var persistent = (await HttpContext.AuthenticateAsync()).Properties?.IsPersistent ?? false;
            await SignInSessionAsync(session, mustChangePassword: false, persistent);

            TempData["Flash"] = T("Password changed.", "تم تغيير كلمة المرور.");
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [NoPermissionRequired("Ending one's own session is never something to be denied.")]
        public async Task<IActionResult> Logout()
        {
            var token = User.FindFirst(SmsClaimTypes.SessionToken)?.Value;
            if (!string.IsNullOrEmpty(token))
            {
                await _auth.LogoutAsync(token, HttpContext.RequestAborted);
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        [AllowAnonymous]
        [NoPermissionRequired("The page shown when something was denied.")]
        public IActionResult AccessDenied() => View();

        private async Task SignInSessionAsync(UserSession session, bool mustChangePassword, bool persistent)
        {
            var account = await _db.UserAccounts.AsNoTracking().SingleAsync(u => u.Id == session.UserAccountId);
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, account.Id.ToString(CultureInfo.InvariantCulture)),
                new(ClaimTypes.Name, account.UserName),
                new(SmsClaimTypes.SessionToken, session.SessionToken),
                new(SmsClaimTypes.SchoolId, account.SchoolId.ToString(CultureInfo.InvariantCulture)),
                new(SmsClaimTypes.AccountType, account.AccountType.ToString()),
            };
            if (mustChangePassword)
            {
                claims.Add(new Claim(SmsClaimTypes.MustChangePassword, "1"));
            }

            // The embedded ERP modules authorize by claim, not by a service call, so every accounting
            // permission this account holds has to be on the principal before it is signed in. They are
            // ordinary sec.RolePermission grants under the reserved "ERP" module code
            // (IExternalPermissionCatalog); an account with none simply carries none, and every
            // accounting screen denies it — the correct deny-by-default answer, not a gap to patch.
            var erpPermissions = await _permissions.GetGrantedScreenCodesAsync(
                account.Id, ErpPermissionCatalog.ErpModuleCode, ActionVerb.View, HttpContext.RequestAborted);
            claims.AddRange(erpPermissions.Select(p => new Claim(AppClaimTypes.Permission, p)));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = persistent,
                    // The cookie may outlive nothing: the sec.UserSession row (BR-SEC-004) is
                    // the real lifetime and is re-checked on every request.
                    ExpiresUtc = session.ExpiresAtUtc,
                    AllowRefresh = true,
                });
        }

        private string SafeReturnUrl(string? returnUrl) =>
            !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Action("Index", "Home")!;

        private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

        private string? UserAgent => Request.Headers["User-Agent"].ToString();

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        private static string Describe(PasswordPolicyViolation violation) => violation switch
        {
            PasswordPolicyViolation.TooShort => T("At least 10 characters.", "10 أحرف على الأقل."),
            PasswordPolicyViolation.MissingUppercase => T("Include an uppercase letter.", "يجب أن تحتوي على حرف كبير."),
            PasswordPolicyViolation.MissingLowercase => T("Include a lowercase letter.", "يجب أن تحتوي على حرف صغير."),
            PasswordPolicyViolation.MissingDigit => T("Include a digit.", "يجب أن تحتوي على رقم."),
            PasswordPolicyViolation.MissingSymbol => T("Include a symbol.", "يجب أن تحتوي على رمز."),
            PasswordPolicyViolation.ReusesRecentPassword => T("Cannot reuse one of your last 5 passwords.", "لا يمكن إعادة استخدام إحدى كلمات المرور الخمس الأخيرة."),
            _ => violation.ToString(),
        };
    }
}
