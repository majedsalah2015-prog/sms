using System;
using System.Linq;
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

                // An API endpoint says for itself whether a family may reach it, and
                // nothing redirects: a phone asking for staff data is refused, never
                // sent to a different screen.
                if (context.Controller is Sms.Web.Api.ApiControllerBase)
                {
                    if (!DeclaresPortalReach(context))
                    {
                        context.Result = new NotFoundResult();
                    }

                    return context.Result == null ? next() : Task.CompletedTask;
                }

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

        /// <summary>
        /// The API's half of BR-SEC-010. Declared on the endpoint with
        /// <see cref="Sms.Web.Api.PortalReachableAttribute"/> rather than listed
        /// by controller name here: a name list is a security decision kept
        /// somewhere other than the code it governs, and the browser half above
        /// already shows how quickly it stops being read.
        /// </summary>
        private static bool DeclaresPortalReach(ActionExecutingContext context)
            => context.ActionDescriptor.EndpointMetadata
                .OfType<Sms.Web.Api.PortalReachableAttribute>().Any();

        private static bool IsPortalReachable(string controller, string action)
            => Eq(controller, "Portal")
               || Eq(controller, "Account")
               // The user guide, which the portal's own top bar links to. It answers a portal
               // account with the portal's four chapters and no screen index at all, so nothing
               // this rule protects is disclosed by reaching it.
               || Eq(controller, "Help")
               || (Eq(controller, "Home") && (Eq(action, "SetLanguage") || Eq(action, "Privacy") || Eq(action, "Error")));

        private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
