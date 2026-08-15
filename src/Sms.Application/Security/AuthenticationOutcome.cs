using Sms.Domain.Security;

namespace Sms.Application.Security
{
    /// <summary>
    /// Result of <see cref="IAuthenticationService.AuthenticateAsync"/>.
    /// <see cref="Session"/> is null exactly when <see cref="RequiresTwoFactor"/>
    /// is true — the session is only minted once BR-SEC-003 completes.
    /// </summary>
    public sealed class AuthenticationOutcome
    {
        public int UserAccountId { get; init; }

        public bool RequiresTwoFactor { get; init; }

        public bool MustChangePassword { get; init; }

        public UserSession? Session { get; init; }
    }
}
