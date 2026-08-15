using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Security;

namespace Sms.Application.Security
{
    /// <summary>
    /// Authentication port (doc 06 §3, BR-SEC-001..005). Every outcome — success,
    /// failure, and lockout — is audited by the implementation via
    /// <see cref="Application.Audit.IAuditEventWriter"/> (AuditAction.Login*).
    /// </summary>
    public interface IAuthenticationService
    {
        /// <summary>Throws <see cref="Common.Exceptions.InvalidCredentialsException"/> or <see cref="Common.Exceptions.AccountLockedOutException"/> on failure.</summary>
        Task<AuthenticationOutcome> AuthenticateAsync(string userName, string password, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default);

        /// <summary>Completes a login left pending by <see cref="AuthenticationOutcome.RequiresTwoFactor"/>; throws <see cref="Common.Exceptions.InvalidTwoFactorCodeException"/> on a bad code.</summary>
        Task<UserSession> CompleteTwoFactorAsync(int userAccountId, string code, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default);

        /// <summary>Self-service change: verifies the current password, then enforces policy + history (BR-SEC-001) on the new one.</summary>
        Task ChangePasswordAsync(int userAccountId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);

        /// <summary>Admin reset (BR-SEC-005): issues a one-time password and forces a change before any other action.</summary>
        Task SetTemporaryPasswordAsync(int userAccountId, string temporaryPassword, CancellationToken cancellationToken = default);

        /// <summary>Issues a fresh TOTP secret; 2FA does not enforce until <see cref="ConfirmTwoFactorEnrollmentAsync"/> succeeds.</summary>
        Task<TwoFactorEnrollment> BeginTwoFactorEnrollmentAsync(int userAccountId, CancellationToken cancellationToken = default);

        /// <summary>Verifies the first code and flips <see cref="UserAccount.TwoFactorEnabled"/> on.</summary>
        Task ConfirmTwoFactorEnrollmentAsync(int userAccountId, string code, CancellationToken cancellationToken = default);

        /// <summary>BR-SEC-004: null when the token is unknown, revoked, or past idle/absolute expiry; otherwise touches activity and returns the session.</summary>
        Task<UserSession?> ValidateSessionAsync(string sessionToken, CancellationToken cancellationToken = default);

        Task LogoutAsync(string sessionToken, CancellationToken cancellationToken = default);
    }
}
