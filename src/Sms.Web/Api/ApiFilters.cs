using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Sms.Web.Api
{
    /// <summary>
    /// Domain refusals out of an API action, as the client's language.
    /// <para>
    /// Only what <see cref="ApiProblem.TryTranslate"/> recognises is handled.
    /// Anything else stays unhandled and becomes a 500 the platform logs —
    /// which is the point: a fault that arrives dressed as a business rule is a
    /// fault nobody fixes.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ApiExceptionFilterAttribute : Attribute, IAsyncExceptionFilter
    {
        public Task OnExceptionAsync(ExceptionContext context)
        {
            if (ApiProblem.TryTranslate(context.Exception, out var status, out var error))
            {
                context.Result = ApiResults.Error(status, error);
                context.ExceptionHandled = true;
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Gives a body to the bare status results this application produces.
    /// <para>
    /// <c>RequirePermissionFilter</c> answers a missing permission with
    /// <see cref="NotFoundResult"/> — no content at all, which over HTML is
    /// invisible and over JSON is a client parsing an empty string. This turns
    /// every bare 4xx into the same <see cref="ApiErrorResponse"/> as everything
    /// else, without changing what the status code says.
    /// </para>
    /// <para>
    /// <see cref="IAlwaysRunResultFilter"/> deliberately: an action filter that
    /// short-circuits (which is exactly what the permission guard does) skips
    /// ordinary result filters.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class ApiStatusEnvelopeAttribute : Attribute, IAlwaysRunResultFilter
    {
        public void OnResultExecuting(ResultExecutingContext context)
        {
            if (context.Result is not StatusCodeResult bare || bare.StatusCode < 400)
            {
                return;
            }

            // Only the three an action or a guard here actually raises bare. A
            // status this does not recognise is left exactly as it was rather
            // than given a message invented for it.
            var error = bare.StatusCode switch
            {
                401 => ApiProblem.Unauthenticated(),
                403 => ApiProblem.Forbidden(),
                404 => ApiProblem.NotFound(),
                _ => null,
            };

            if (error != null)
            {
                context.Result = ApiResults.Error(bare.StatusCode, error);
            }
        }

        public void OnResultExecuted(ResultExecutedContext context)
        {
        }
    }

    /// <summary>
    /// Marks an endpoint a portal account (parent / student) may reach.
    /// <para>
    /// BR-SEC-010 keeps portal accounts off staff surface, and
    /// <c>PortalAreaFilter</c> enforces it by controller name — a list that
    /// works while there are five portal screens and stops working the moment a
    /// second transport adds controllers of its own. An API controller says so
    /// with this instead of being added to a string list in a security filter.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class PortalReachableAttribute : Attribute
    {
    }

    /// <summary>
    /// Marks the handful of endpoints reachable while BR-SEC-005 is still
    /// demanding a password change — changing it, and signing out. Everything
    /// else is refused until it is done, on the API exactly as in the browser.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class PasswordChangeExemptAttribute : Attribute
    {
    }
}
