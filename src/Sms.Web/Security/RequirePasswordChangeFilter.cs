using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Sms.Web.Security
{
    /// <summary>
    /// BR-SEC-005: a first login or an admin reset forces a password change
    /// "before any other action". Registered globally; while the principal
    /// carries the must-change claim every request except the change-password
    /// and logout endpoints is redirected there.
    /// </summary>
    public sealed class RequirePasswordChangeFilter : IAsyncActionFilter
    {
        public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;
            if (user.Identity?.IsAuthenticated == true
                && user.FindFirst(SmsClaimTypes.MustChangePassword)?.Value == "1"
                && !IsExempt(context))
            {
                context.Result = new RedirectToActionResult("ChangePassword", "Account", null);
                return Task.CompletedTask;
            }

            return next();
        }

        private static bool IsExempt(ActionExecutingContext context)
        {
            var controller = context.RouteData.Values["controller"] as string;
            var action = context.RouteData.Values["action"] as string;
            return string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(action, "ChangePassword", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(action, "Logout", StringComparison.OrdinalIgnoreCase));
        }
    }
}
