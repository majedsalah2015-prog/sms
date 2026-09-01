using System;

using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sms.Web.Security;
using IAuthenticationService = Sms.Application.Security.IAuthenticationService;

namespace Sms.Web.Api.Auth
{
    /// <summary>
    /// The mobile transport of the sign-in this product already has (doc 06 §3).
    /// <para>
    /// <c>sec.UserSession.SessionToken</c> has always been an opaque bearer
    /// token — the auth cookie carries nothing else, and
    /// <see cref="IAuthenticationService.ValidateSessionAsync"/> is what decides
    /// on every single request whether it is still good. So the phone sends the
    /// same token in <c>Authorization: Bearer</c> and gets the same answer: idle
    /// and absolute expiry (BR-SEC-004), logout-elsewhere and administrator
    /// revocation all take effect on the next call, exactly as they do in the
    /// browser. No second credential format, no signing key, and nothing that
    /// keeps working after a session is revoked.
    /// </para>
    /// <para>
    /// <b>The cost of that choice, stated plainly:</b> a session's absolute
    /// ceiling is the school's <c>SessionPolicy</c> (12 hours by default and
    /// never extended by activity), so a mobile user signs in again once a day.
    /// A longer-lived per-device refresh token would remove that, and is
    /// deliberately not built here — it is a new credential with its own
    /// revocation surface, and it was not asked for.
    /// </para>
    /// </summary>
    public sealed class SessionTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private const string BearerPrefix = "Bearer ";

        private readonly IAuthenticationService _auth;
        private readonly SessionPrincipalFactory _principals;

        public SessionTokenAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            IAuthenticationService auth,
            SessionPrincipalFactory principals)
            : base(options, logger, encoder, clock)
        {
            _auth = auth;
            _principals = principals;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrWhiteSpace(header) || !header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                // Not "wrong token" — no token at all. NoResult lets the challenge
                // below answer with a 401 the client can act on, rather than an
                // authentication failure the logs would fill up with.
                return AuthenticateResult.NoResult();
            }

            var token = header.Substring(BearerPrefix.Length).Trim();
            if (token.Length == 0)
            {
                return AuthenticateResult.NoResult();
            }

            var session = await _auth.ValidateSessionAsync(token, Context.RequestAborted);
            if (session == null)
            {
                return AuthenticateResult.Fail("The session token is unknown, revoked, or expired.");
            }

            var mustChange = await _principals.MustChangePasswordAsync(session.UserAccountId, Context.RequestAborted);
            var identity = await _principals.BuildAsync(session, mustChange, Scheme.Name, Context.RequestAborted);

            // The session row is the lifetime; the ticket carries no expiry of its own,
            // for the same reason the cookie's is advisory (see AccountController).
            return AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
        }

        /// <summary>
        /// 401 as JSON. The cookie handler would redirect to <c>/Account/Login</c>
        /// here, and an HTML login page arriving where a phone expected a
        /// refusal is indistinguishable from a server fault — which is how a
        /// mobile client ends up showing "something went wrong" for "your
        /// session ended".
        /// </summary>
        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = 401;
            Response.Headers["WWW-Authenticate"] = "Bearer";
            await ApiResults.WriteAsync(
                Response,
                401,
                ApiProblem.Unauthenticated(),
                Context.RequestAborted);
        }

        /// <summary>
        /// 403 as JSON, for an authenticated caller a policy turned away. Note
        /// that a <em>missing screen permission</em> does not come through here:
        /// <see cref="RequirePermissionAttribute"/> answers 404 by design
        /// (BR-SEC-010, doc 06 §1) so unauthorized surface disappears rather
        /// than announcing itself, and the API keeps that promise.
        /// </summary>
        protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            await ApiResults.WriteAsync(
                Response,
                403,
                ApiProblem.Forbidden(),
                Context.RequestAborted);
        }
    }

    /// <summary>Names and registration for <see cref="SessionTokenAuthenticationHandler"/>.</summary>
    public static class SessionTokenDefaults
    {
        /// <summary>The scheme every API controller authorizes against.</summary>
        public const string Scheme = "Sms.Bearer";

        /// <summary>Shown on the OpenAPI page's Authorize dialog.</summary>
        public const string DisplayName = "SMS session token";

        /// <summary>The header value a client builds, for documentation and tests.</summary>
        public static string Header(string sessionToken) => "Bearer " + sessionToken;
    }
}
