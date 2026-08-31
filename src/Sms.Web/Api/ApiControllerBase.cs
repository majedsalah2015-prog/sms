using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sms.Domain.Security;
using Sms.Web.Api.Auth;
using Sms.Web.Security;

namespace Sms.Web.Api
{
    /// <summary>
    /// What every endpoint of the mobile API shares (docs/Integration/03-Mobile-API.md).
    /// <para>
    /// The API is a second transport over the screens this product already has,
    /// not a second product: the same <c>ScreenCatalog</c> permissions guard it,
    /// the same <c>sec.UserSession</c> authenticates it, the same ports do the
    /// work, and the same rule about translating a refusal applies. Where an
    /// endpoint has no browser equivalent that is said in its own summary.
    /// </para>
    /// <para>
    /// <b>Not part of approved Analysis v1.0.</b> The docs put native mobile
    /// apps in <c>Future/</c> (GAP register G5, roadmap R2) and the module docs
    /// therefore specify no API screens. This layer is built to the same rules
    /// as the screens it mirrors, but no numbered requirement covers it, and a
    /// reader looking for one should not conclude they missed it.
    /// </para>
    /// </summary>
    [ApiController]
    [Produces("application/json")]
    [Authorize(AuthenticationSchemes = SessionTokenDefaults.Scheme)]
    [ApiExceptionFilter]
    [ApiStatusEnvelope]
    public abstract class ApiControllerBase : ControllerBase
    {
        /// <summary>Everything hangs off one versioned root so a breaking change can be a second one.</summary>
        public const string V1 = "api/v1";

        protected static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        /// <summary>
        /// The same helper every controller and view in this product uses. The
        /// culture is the caller's: request localization already reads
        /// <c>Accept-Language</c>, so a phone set to Arabic gets Arabic without
        /// sending anything else.
        /// </summary>
        protected static string T(string en, string ar) => IsArabic ? ar : en;

        protected CancellationToken Ct => HttpContext.RequestAborted;

        /// <summary>The signed-in <c>sec.UserAccount</c>. Never 0 here — the class is <c>[Authorize]</c>d.</summary>
        protected int CurrentUserAccountId
            => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                ? id
                : 0;

        protected AccountType CurrentAccountType
            => Enum.TryParse<AccountType>(User.FindFirst(SmsClaimTypes.AccountType)?.Value, out var t) ? t : AccountType.Staff;

        protected bool IsPortalAccount
            => CurrentAccountType is AccountType.Parent or AccountType.Student;

        /// <summary>One page of a list, in the shape every list endpoint returns.</summary>
        protected static ApiPage<T> Page<T>(IReadOnlyList<T> items, int page, int pageSize, int total)
            => new(items, page, pageSize, total);

        /// <summary>
        /// A record this caller may not see or that is not there — the same
        /// answer for both, which is BR-SEC-010's whole point.
        /// </summary>
        protected ObjectResult NotFoundError() => ApiResults.Error(404, ApiProblem.NotFound());

        /// <summary>A refusal this action decided itself, rather than one an engine threw.</summary>
        protected ObjectResult Refuse(int status, string code, string en, string ar)
            => ApiResults.Error(status, new ApiError(code, T(en, ar)));
    }
}
