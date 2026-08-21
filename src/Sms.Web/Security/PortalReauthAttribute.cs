using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Sms.Application.Common.Interfaces;

namespace Sms.Web.Security
{
    /// <summary>
    /// BR-SEC-013: payment and personal-data pages require re-authentication
    /// after 15 idle minutes. <see cref="SessionCookieEvents"/> stashes the
    /// session's <i>previous</i> LastActivityAtUtc (before this request
    /// touched it) in HttpContext.Items; when that gap exceeds the limit the
    /// user is signed out and sent to Login with a return URL.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class PortalReauthAttribute : TypeFilterAttribute
    {
        public const int IdleMinutes = 15;

        public PortalReauthAttribute() : base(typeof(PortalReauthFilter)) { }
    }

    public sealed class PortalReauthFilter : IAsyncActionFilter
    {
        private readonly IClock _clock;

        public PortalReauthFilter(IClock clock) => _clock = clock;

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var http = context.HttpContext;
            if (http.User.Identity?.IsAuthenticated == true
                && http.Items[SessionCookieEvents.PreviousActivityItem] is DateTime previous
                && (_clock.UtcNow - previous).TotalMinutes > PortalReauthAttribute.IdleMinutes)
            {
                await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                var tempData = http.RequestServices.GetRequiredService<ITempDataDictionaryFactory>().GetTempData(http);
                // Request localization has already run, so the UI culture is the user's — single language, not the concatenated pair (WCAG 3.1.2).
                tempData["Error"] = System.Globalization.CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft
                    ? "يرجى تسجيل الدخول مجدداً لفتح هذه الصفحة (خمول أكثر من 15 دقيقة — BR-SEC-013)."
                    : "Please sign in again to open this page (idle for more than 15 minutes — BR-SEC-013).";
                var returnUrl = http.Request.Path + http.Request.QueryString;
                context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
                return;
            }
            await next();
        }
    }
}
