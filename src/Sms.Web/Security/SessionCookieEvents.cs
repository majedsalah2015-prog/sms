using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sms.Infrastructure.Persistence;
using IAuthenticationService = Sms.Application.Security.IAuthenticationService;

namespace Sms.Web.Security
{
    /// <summary>
    /// Ties the auth cookie to the sec.UserSession row: every request
    /// re-validates the session token through IAuthenticationService, so
    /// BR-SEC-004 idle/absolute timeouts, logout-elsewhere and admin
    /// revocation all take effect immediately instead of when the cookie
    /// happens to expire. A missing/expired session rejects the principal
    /// and clears the cookie. Before validation touches LastActivityAtUtc,
    /// its previous value is stashed in HttpContext.Items so BR-SEC-013's
    /// idle re-auth filter (<see cref="PortalReauthFilter"/>) can measure
    /// the real idle gap.
    /// </summary>
    public sealed class SessionCookieEvents : CookieAuthenticationEvents
    {
        public const string PreviousActivityItem = "sms:previous-activity";

        public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
        {
            var token = context.Principal?.FindFirst(SmsClaimTypes.SessionToken)?.Value;
            if (!string.IsNullOrEmpty(token))
            {
                var services = context.HttpContext.RequestServices;
                var db = services.GetRequiredService<AppDbContext>();
                var previous = await db.UserSessions.AsNoTracking()
                    .Where(s => s.SessionToken == token)
                    .Select(s => (System.DateTime?)s.LastActivityAtUtc)
                    .FirstOrDefaultAsync(context.HttpContext.RequestAborted);
                if (previous != null)
                {
                    context.HttpContext.Items[PreviousActivityItem] = previous.Value;
                }

                var auth = services.GetRequiredService<IAuthenticationService>();
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
