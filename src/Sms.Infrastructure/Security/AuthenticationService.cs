using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Security;
using Sms.Domain.Audit;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Security
{
    /// <summary>
    /// Orchestrates doc 06 §3 (BR-SEC-001..004) over the pure policy engines:
    /// <see cref="LockoutEvaluator"/>, <see cref="PasswordPolicyEvaluator"/>,
    /// <see cref="SessionPolicyEvaluator"/>, <see cref="TwoFactorTotp"/>. Every
    /// outcome — success, failure, lockout — is recorded (LoginAttempt +
    /// AuditAction.Login/LoginFailed/Logout) before the method returns or
    /// throws, so a denied attempt still leaves a trail.
    /// </summary>
    public class AuthenticationService : IAuthenticationService
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher _hasher;
        private readonly IClock _clock;
        private readonly IAuditEventWriter _auditEvents;

        public AuthenticationService(AppDbContext db, IPasswordHasher hasher, IClock clock, IAuditEventWriter auditEvents)
        {
            _db = db;
            _hasher = hasher;
            _clock = clock;
            _auditEvents = auditEvents;
        }

        public async Task<AuthenticationOutcome> AuthenticateAsync(
            string userName, string password, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default)
        {
            var now = _clock.UtcNow;
            var account = await _db.UserAccounts.SingleOrDefaultAsync(u => u.UserName == userName, cancellationToken);

            if (account == null)
            {
                await RecordAttemptAsync(null, userName, succeeded: false, "UnknownUser", ipAddress, cancellationToken);
                throw new InvalidCredentialsException();
            }

            var lockout = LockoutEvaluator.Evaluate(account.AccessFailedCount, account.LockedOutUntilUtc, now, LockoutPolicy.Default);
            if (lockout.IsLockedOut)
            {
                await RecordAttemptAsync(account.Id, userName, succeeded: false, "LockedOut", ipAddress, cancellationToken);
                throw new AccountLockedOutException(lockout.UnlocksAtUtc!.Value);
            }

            if (account.PasswordHash == null || !_hasher.Verify(account.PasswordHash, password))
            {
                var (failedCount, lockedOutUntil) = LockoutEvaluator.RegisterFailure(account.AccessFailedCount, now, LockoutPolicy.Default);
                account.AccessFailedCount = failedCount;
                account.LockedOutUntilUtc = lockedOutUntil;
                await RecordAttemptAsync(account.Id, userName, succeeded: false, "BadPassword", ipAddress, cancellationToken);
                throw new InvalidCredentialsException();
            }

            account.AccessFailedCount = 0;
            account.LockedOutUntilUtc = null;

            if (account.TwoFactorEnabled)
            {
                await RecordAttemptAsync(account.Id, userName, succeeded: true, null, ipAddress, cancellationToken, raiseLoginEvent: false);
                return new AuthenticationOutcome
                {
                    UserAccountId = account.Id,
                    RequiresTwoFactor = true,
                    MustChangePassword = account.MustChangePassword,
                };
            }

            var session = await CreateSessionAsync(account, ipAddress, userAgent, now, cancellationToken);
            await RecordAttemptAsync(account.Id, userName, succeeded: true, null, ipAddress, cancellationToken);

            return new AuthenticationOutcome
            {
                UserAccountId = account.Id,
                MustChangePassword = account.MustChangePassword,
                Session = session,
            };
        }

        public async Task<UserSession> CompleteTwoFactorAsync(
            int userAccountId, string code, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default)
        {
            var now = _clock.UtcNow;
            var enrollment = await _db.TwoFactorEnrollments.SingleOrDefaultAsync(
                e => e.UserAccountId == userAccountId && e.Method == TwoFactorMethod.Totp && e.ConfirmedAtUtc != null, cancellationToken);

            if (enrollment == null || !TwoFactorTotp.ValidateCode(enrollment.SecretKey, code, now))
            {
                _auditEvents.Log(AuditAction.LoginFailed, nameof(UserAccount), userAccountId, reason: "invalid 2FA code");
                await _db.SaveChangesAsync(cancellationToken);
                throw new InvalidTwoFactorCodeException();
            }

            var account = await _db.UserAccounts.SingleAsync(u => u.Id == userAccountId, cancellationToken);
            var session = await CreateSessionAsync(account, ipAddress, userAgent, now, cancellationToken);
            _auditEvents.Log(AuditAction.Login, nameof(UserAccount), userAccountId);
            await _db.SaveChangesAsync(cancellationToken);
            return session;
        }

        public async Task ChangePasswordAsync(int userAccountId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
        {
            var account = await _db.UserAccounts.SingleAsync(u => u.Id == userAccountId, cancellationToken);
            if (account.PasswordHash == null || !_hasher.Verify(account.PasswordHash, currentPassword))
            {
                throw new InvalidCredentialsException();
            }

            await SetPasswordAsync(account, newPassword, requireChangeOnNextLogin: false, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task SetTemporaryPasswordAsync(int userAccountId, string temporaryPassword, CancellationToken cancellationToken = default)
        {
            var account = await _db.UserAccounts.SingleAsync(u => u.Id == userAccountId, cancellationToken);
            await SetPasswordAsync(account, temporaryPassword, requireChangeOnNextLogin: true, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<TwoFactorEnrollment> BeginTwoFactorEnrollmentAsync(int userAccountId, CancellationToken cancellationToken = default)
        {
            var enrollment = await _db.TwoFactorEnrollments.SingleOrDefaultAsync(
                e => e.UserAccountId == userAccountId && e.Method == TwoFactorMethod.Totp, cancellationToken);

            var secret = TwoFactorTotp.GenerateSecretKey();
            if (enrollment == null)
            {
                enrollment = new TwoFactorEnrollment { UserAccountId = userAccountId, Method = TwoFactorMethod.Totp, SecretKey = secret };
                _db.TwoFactorEnrollments.Add(enrollment);
            }
            else
            {
                // Re-enrolling (e.g. lost device) resets confirmation — the old secret stops working immediately.
                enrollment.SecretKey = secret;
                enrollment.ConfirmedAtUtc = null;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return enrollment;
        }

        public async Task ConfirmTwoFactorEnrollmentAsync(int userAccountId, string code, CancellationToken cancellationToken = default)
        {
            var enrollment = await _db.TwoFactorEnrollments.SingleAsync(
                e => e.UserAccountId == userAccountId && e.Method == TwoFactorMethod.Totp, cancellationToken);

            if (!TwoFactorTotp.ValidateCode(enrollment.SecretKey, code, _clock.UtcNow))
            {
                throw new InvalidTwoFactorCodeException();
            }

            enrollment.ConfirmedAtUtc = _clock.UtcNow;

            var account = await _db.UserAccounts.SingleAsync(u => u.Id == userAccountId, cancellationToken);
            account.TwoFactorEnabled = true;

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<UserSession?> ValidateSessionAsync(string sessionToken, CancellationToken cancellationToken = default)
        {
            var session = await _db.UserSessions.SingleOrDefaultAsync(s => s.SessionToken == sessionToken && s.RevokedAtUtc == null, cancellationToken);
            if (session == null)
            {
                return null;
            }

            var account = await _db.UserAccounts.SingleAsync(u => u.Id == session.UserAccountId, cancellationToken);
            var now = _clock.UtcNow;

            if (SessionPolicyEvaluator.IsExpired(session.LastActivityAtUtc, session.ExpiresAtUtc, account.AccountType, now, SessionPolicy.Default))
            {
                session.RevokedAtUtc = now;
                session.RevokedReason = "Expired";
                await _db.SaveChangesAsync(cancellationToken);
                return null;
            }

            session.LastActivityAtUtc = now;
            await _db.SaveChangesAsync(cancellationToken);
            return session;
        }

        public async Task LogoutAsync(string sessionToken, CancellationToken cancellationToken = default)
        {
            var session = await _db.UserSessions.SingleOrDefaultAsync(s => s.SessionToken == sessionToken && s.RevokedAtUtc == null, cancellationToken);
            if (session == null)
            {
                return;
            }

            session.RevokedAtUtc = _clock.UtcNow;
            session.RevokedReason = "Logout";
            _auditEvents.Log(AuditAction.Logout, nameof(UserAccount), session.UserAccountId);
            await _db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>BR-SEC-001 policy + history-reuse check, then rotates the hash and security stamp. Caller saves.</summary>
        private async Task SetPasswordAsync(UserAccount account, string newPassword, bool requireChangeOnNextLogin, CancellationToken cancellationToken)
        {
            var policy = PasswordPolicy.ProductMinimum;
            var violations = PasswordPolicyEvaluator.Validate(newPassword, policy).ToList();

            var recentHashes = await _db.PasswordHistories
                .Where(h => h.UserAccountId == account.Id)
                .OrderByDescending(h => h.CreatedAtUtc)
                .Take(policy.HistoryCount - 1)
                .Select(h => h.PasswordHash)
                .ToListAsync(cancellationToken);

            var reused = (account.PasswordHash != null && _hasher.Verify(account.PasswordHash, newPassword))
                         || recentHashes.Any(h => _hasher.Verify(h, newPassword));
            if (reused)
            {
                violations.Add(PasswordPolicyViolation.ReusesRecentPassword);
            }

            if (violations.Count > 0)
            {
                throw new PasswordPolicyViolationException(violations);
            }

            if (account.PasswordHash != null)
            {
                _db.PasswordHistories.Add(new PasswordHistory { UserAccountId = account.Id, PasswordHash = account.PasswordHash });
            }

            account.PasswordHash = _hasher.Hash(newPassword);
            account.PasswordChangedAtUtc = _clock.UtcNow;
            account.MustChangePassword = requireChangeOnNextLogin;
            account.SecurityStamp = Guid.NewGuid().ToString("N");
        }

        /// <summary>BR-SEC-004: mints the session, revoking prior ones first when any held role enforces single-session.</summary>
        private async Task<UserSession> CreateSessionAsync(UserAccount account, string? ipAddress, string? userAgent, DateTime now, CancellationToken cancellationToken)
        {
            var roleIds = await _db.RoleAssignments
                .Where(a => a.UserAccountId == account.Id)
                .Select(a => a.RoleId)
                .ToListAsync(cancellationToken);

            var enforceSingleSession = roleIds.Count > 0
                && await _db.Roles.AnyAsync(r => roleIds.Contains(r.Id) && r.EnforceSingleSession, cancellationToken);

            if (enforceSingleSession)
            {
                var priorSessions = await _db.UserSessions
                    .Where(s => s.UserAccountId == account.Id && s.RevokedAtUtc == null)
                    .ToListAsync(cancellationToken);
                foreach (var prior in priorSessions)
                {
                    prior.RevokedAtUtc = now;
                    prior.RevokedReason = "SingleSessionPolicy";
                }
            }

            var session = new UserSession
            {
                UserAccountId = account.Id,
                LastActivityAtUtc = now,
                ExpiresAtUtc = SessionPolicyEvaluator.ComputeExpiresAtUtc(now, SessionPolicy.Default),
                IpAddress = ipAddress,
                UserAgent = userAgent,
            };
            _db.UserSessions.Add(session);
            return session;
        }

        private async Task RecordAttemptAsync(
            int? userAccountId, string userNameAttempted, bool succeeded, string? failureReason, string? ipAddress,
            CancellationToken cancellationToken, bool raiseLoginEvent = true)
        {
            _db.LoginAttempts.Add(new LoginAttempt
            {
                UserAccountId = userAccountId,
                UserNameAttempted = userNameAttempted,
                Succeeded = succeeded,
                FailureReason = failureReason,
                IpAddress = ipAddress,
            });

            if (raiseLoginEvent)
            {
                _auditEvents.Log(
                    succeeded ? AuditAction.Login : AuditAction.LoginFailed,
                    nameof(UserAccount), userAccountId, userNameAttempted, failureReason);
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
