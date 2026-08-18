using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Sms.Application.Common.Interfaces;

namespace Sms.Web.Security
{
    /// <summary>
    /// ICurrentUser off the authenticated cookie principal — the "session
    /// context slice" E-003 left pending. Outside an HTTP request (Hangfire
    /// jobs, seeding) or for anonymous requests it yields UserId 0, the
    /// reserved system actor, exactly like the SystemUser placeholder did.
    /// </summary>
    public sealed class HttpContextCurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor _accessor;

        public HttpContextCurrentUser(IHttpContextAccessor accessor)
        {
            _accessor = accessor;
        }

        public int UserId
        {
            get
            {
                var value = _accessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                return int.TryParse(value, out var id) ? id : 0;
            }
        }
    }
}
