using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sms.Domain.Security;

namespace Sms.Web.Security
{
    /// <summary>
    /// BR-SEC-010: portal accounts (Parent / Student) reach only portal
    /// areas; staff URLs return <b>not-found</b> (not access-denied) to a
    /// portal session. Registered globally. Staff accounts are untouched
    /// (they may open /portal to preview — an empty family view).
    /// </summary>
    public sealed class PortalAreaFilter : IAsyncActionFilter
    {
        public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;
            if (user.Identity?.IsAuthenticated == true && IsPortalAccount(user.FindFirst(SmsClaimTypes.AccountType)?.Value))
            {
                var controller = context.RouteData.Values["controller"] as string ?? "";
                var action = context.RouteData.Values["action"] as string ?? "";
                if (Eq(controller, "Home") && Eq(action, "Index"))
                {
                    context.Result = new RedirectToActionResult("Index", "Portal", null);
                    return Task.CompletedTask;
                }
                if (!IsPortalReachable(controller, action))
                {
                    context.Result = new NotFoundResult();
                    return Task.CompletedTask;
                }
            }
            return next();
        }

        public static bool IsPortalAccount(string? accountTypeClaim)
            => Enum.TryParse<AccountType>(accountTypeClaim, out var t) && (t == AccountType.Parent || t == AccountType.Student);

        private static bool IsPortalReachable(string controller, string action)
            => Eq(controller, "Portal")
               || Eq(controller, "Account")
               || (Eq(controller, "Home") && (Eq(action, "SetLanguage") || Eq(action, "Privacy") || Eq(action, "Error")));

        private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
