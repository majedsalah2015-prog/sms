using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sms.Web.Api;

namespace Sms.Web.Security
{
    /// <summary>
    /// BR-SEC-005: a first login or an admin reset forces a password change
    /// "before any other action". Registered globally; while the principal
    /// carries the must-change claim every request except the change-password
    /// and logout endpoints is redirected there.
    /// <para>
    /// The mobile API is held to the same rule by the same filter, and answers
    /// it differently: a redirect to an HTML form is not something a phone can
    /// act on, so an API caller is refused with <c>must_change_password</c> and
    /// pointed at the endpoint that clears it.
    /// </para>
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
                context.Result = context.Controller is ApiControllerBase
                    ? ApiResults.Error(StatusCodes.Status403Forbidden, ApiProblem.MustChangePassword())
                    : new RedirectToActionResult("ChangePassword", "Account", null);
                return Task.CompletedTask;
            }

            return next();
        }

        private static bool IsExempt(ActionExecutingContext context)
        {
            if (context.ActionDescriptor.EndpointMetadata.OfType<PasswordChangeExemptAttribute>().Any())
            {
                return true;
            }

            var controller = context.RouteData.Values["controller"] as string;
            var action = context.RouteData.Values["action"] as string;
            return string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(action, "ChangePassword", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(action, "Logout", StringComparison.OrdinalIgnoreCase));
        }
    }
}
