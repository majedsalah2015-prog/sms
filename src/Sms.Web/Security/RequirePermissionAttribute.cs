using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sms.Application.Security;
using Sms.Domain.Security;

namespace Sms.Web.Security
{
    /// <summary>
    /// Deny-by-default screen guard (BR-GLB-070). A missing permission returns
    /// NotFound, never AccessDenied — unauthorized surface disappears rather
    /// than errors (doc 06 §1, §6 BR-SEC-010).
    /// </summary>
    public sealed class RequirePermissionAttribute : TypeFilterAttribute
    {
        public RequirePermissionAttribute(string moduleCode, string screenCode, ActionVerb action)
            : base(typeof(RequirePermissionFilter))
        {
            Arguments = new object[] { moduleCode, screenCode, action };
        }
    }

    public sealed class RequirePermissionFilter : IAsyncActionFilter
    {
        private readonly IPermissionService _permissions;
        private readonly string _moduleCode;
        private readonly string _screenCode;
        private readonly ActionVerb _action;

        public RequirePermissionFilter(IPermissionService permissions, string moduleCode, string screenCode, ActionVerb action)
        {
            _permissions = permissions;
            _moduleCode = moduleCode;
            _screenCode = screenCode;
            _action = action;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!await _permissions.HasPermissionAsync(_moduleCode, _screenCode, _action, context.HttpContext.RequestAborted))
            {
                context.Result = new NotFoundResult();
                return;
            }

            await next();
        }
    }
}
