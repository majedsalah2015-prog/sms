using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using IAuthenticationService = Sms.Application.Security.IAuthenticationService;

namespace Sms.Web.Security
{
    /// <summary>
    /// Ties the auth cookie to the sec.UserSession row: every request
    /// re-validates the session token through IAuthenticationService, so
    /// BR-SEC-004 idle/absolute timeouts, logout-elsewhere and admin
    /// revocation all take effect immediately instead of when the cookie
    /// happens to expire. A missing/expired session rejects the principal
    /// and clears the cookie.
    /// </summary>
    public sealed class SessionCookieEvents : CookieAuthenticationEvents
    {
        public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
        {
            var token = context.Principal?.FindFirst(SmsClaimTypes.SessionToken)?.Value;
            if (!string.IsNullOrEmpty(token))
            {
                var auth = context.HttpContext.RequestServices.GetRequiredService<IAuthenticationService>();
                var session = await auth.ValidateSessionAsync(token, context.HttpContext.RequestAborted);
                if (session != null)
                {
                    return;
                }
            }

            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
