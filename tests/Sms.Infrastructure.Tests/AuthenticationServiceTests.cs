using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Security;
using Sms.Domain.Audit;
using Sms.Domain.Common;
using Sms.Domain.Security;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Security;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// E-003 authentication slice over a real Sqlite-backed AppDbContext, so
    /// the BR-AUD-003 atomicity of "deny still leaves a trail" is exercised
    /// end to end, not just against mocks.
    /// </summary>
    public sealed class AuthenticationServiceTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 8, 15, 8, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; }
        }

        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;

            public int AcademicYearId => 2027;
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();
        private readonly IPasswordHasher _hasher = new PasswordHasher();
        private int _accountId;

        public AuthenticationServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            using var db = CreateContext();
            db.Database.EnsureCreated();

            var account = new UserAccount { UserName = "t.ahmad", AccountType = AccountType.Staff };
            db.UserAccounts.Add(account);
            db.SaveChanges();
            _accountId = account.Id;
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private AuthenticationService CreateService(AppDbContext db)
            => new(db, _hasher, _clock, new AuditEventWriter(db, _tenant, _tenant, _user, _clock, _audit));

        // --- BR-SEC-001: password policy + history --------------------------

        [Fact]
        [BusinessRule("BR-SEC-001")]
        public async Task Change_password_rejects_a_policy_violating_candidate()
        {
            using var db = CreateContext();
            var service = CreateService(db);
            await service.SetTemporaryPasswordAsync(_accountId, "Temp0rary!Pass");

            var ex = await Assert.ThrowsAsync<PasswordPolicyViolationException>(
                () => service.ChangePasswordAsync(_accountId, "Temp0rary!Pass", "weak"));
            Assert.Contains(PasswordPolicyViolation.TooShort, ex.Violations);
        }

        [Fact]
        [BusinessRule("BR-SEC-001")]
        public async Task Change_password_rejects_reusing_a_recent_password()
        {
            using var db = CreateContext();
            var service = CreateService(db);
            await service.SetTemporaryPasswordAsync(_accountId, "First!Passw0rd");
            await service.ChangePasswordAsync(_accountId, "First!Passw0rd", "Second!Passw0rd");

            // Reusing the very first password should be blocked (history 5 keeps it).
            var ex = await Assert.ThrowsAsync<PasswordPolicyViolationException>(
                () => service.ChangePasswordAsync(_accountId, "Second!Passw0rd", "First!Passw0rd"));
            Assert.Contains(PasswordPolicyViolation.ReusesRecentPassword, ex.Violations);
        }

        [Fact]
        [BusinessRule("BR-SEC-001")]
        public async Task Change_password_with_the_wrong_current_password_is_denied()
        {
            using var db = CreateContext();
            var service = CreateService(db);
            await service.SetTemporaryPasswordAsync(_accountId, "Temp0rary!Pass");

            await Assert.ThrowsAsync<InvalidCredentialsException>(
                () => service.ChangePasswordAsync(_accountId, "WrongCurrent1!", "New!Passw0rd"));
        }

        // --- BR-SEC-005: forced change on first login / admin reset ---------

        [Fact]
        [BusinessRule("BR-SEC-005")]
        public async Task A_temporary_password_forces_a_change_and_a_self_service_change_clears_it()
        {
            using var db = CreateContext();
            var service = CreateService(db);
            await service.SetTemporaryPasswordAsync(_accountId, "Temp0rary!Pass");
            Assert.True(db.UserAccounts.Single(a => a.Id == _accountId).MustChangePassword);

            await service.ChangePasswordAsync(_accountId, "Temp0rary!Pass", "Perm4nent!Pass");
            Assert.False(db.UserAccounts.Single(a => a.Id == _accountId).MustChangePassword);
        }

        // --- BR-SEC-002: lockout ---------------------------------------------

        [Fact]
        [BusinessRule("BR-SEC-002")]
        public async Task Unknown_username_is_denied_and_still_leaves_an_audit_trail()
        {
            using var db = CreateContext();
            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidCredentialsException>(
                () => service.AuthenticateAsync("nobody", "whatever"));

            var attempt = Assert.Single(db.LoginAttempts.Where(a => a.UserNameAttempted == "nobody"));
            Assert.False(attempt.Succeeded);
            Assert.Null(attempt.UserAccountId);
            Assert.Contains(db.AuditEntries, e => e.Action == AuditAction.LoginFailed && e.BusinessKey == "nobody");
        }

        [Fact]
        [BusinessRule("BR-SEC-002")]
        public async Task The_fifth_consecutive_failure_locks_the_account()
        {
            using var db = CreateContext();
            var service = CreateService(db);
            await service.SetTemporaryPasswordAsync(_accountId, "Correct1!Pass");

            for (var i = 0; i < 5; i++)
            {
                await Assert.ThrowsAsync<InvalidCredentialsException>(
                    () => service.AuthenticateAsync("t.ahmad", "WrongPass1!"));
            }

            // The 5th failure above already tripped the lockout (BR-SEC-002); this 6th
            // attempt is the first one to observe it before even checking the password.
            var lockedEx = await Assert.ThrowsAsync<AccountLockedOutException>(
                () => service.AuthenticateAsync("t.ahmad", "WrongPass1!"));
            Assert.Equal(_clock.UtcNow.AddMinutes(15), lockedEx.UnlocksAtUtc);

            // Locked out even with the correct password.
            await Assert.ThrowsAsync<AccountLockedOutException>(
                () => service.AuthenticateAsync("t.ahmad", "Correct1!Pass"));

            Assert.Equal(7, db.LoginAttempts.Count(a => !a.Succeeded));
        }

        [Fact]
        [BusinessRule("BR-SEC-002")]
        public async Task The_lockout_clears_on_its_own_once_the_window_passes()
        {
            using var db = CreateContext();
            var service = CreateService(db);
            await service.SetTemporaryPasswordAsync(_accountId, "Correct1!Pass");

            for (var i = 0; i < 5; i++)
            {
                await Assert.ThrowsAsync<InvalidCredentialsException>(
                    () => service.AuthenticateAsync("t.ahmad", "WrongPass1!"));
            }

            _clock.UtcNow = _clock.UtcNow.AddMinutes(16);

            var outcome = await service.AuthenticateAsync("t.ahmad", "Correct1!Pass");
            Assert.NotNull(outcome.Session);
            Assert.Equal(0, db.UserAccounts.Single(a => a.Id == _accountId).AccessFailedCount);
        }

        [Fact]
        [BusinessRule("BR-SEC-002")]
        public async Task A_successful_login_resets_the_failure_counter()
        {
            using var db = CreateContext();
            var service = CreateService(db);
            await service.SetTemporaryPasswordAsync(_accountId, "Correct1!Pass");

            await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.AuthenticateAsync("t.ahmad", "Bad!"));
            await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.AuthenticateAsync("t.ahmad", "Bad!"));

            await service.AuthenticateAsync("t.ahmad", "Correct1!Pass");

            Assert.Equal(0, db.UserAccounts.Single(a => a.Id == _accountId).AccessFailedCount);
            Assert.Contains(db.AuditEntries, e => e.Action == AuditAction.Login && e.EntityId == _accountId);
        }

        // --- BR-SEC-003: TOTP two-factor -------------------------------------

        [Fact]
        [BusinessRule("BR-SEC-003")]
        public async Task Confirming_enrollment_with_the_wrong_code_is_rejected_and_2FA_stays_off()
        {
            using var db = CreateContext();
            var service = CreateService(db);
            await service.BeginTwoFactorEnrollmentAsync(_accountId);

            await Assert.ThrowsAsync<InvalidTwoFactorCodeException>(
                () => service.ConfirmTwoFactorEnrollmentAsync(_accountId, "000000"));
            Assert.False(db.UserAccounts.Single(a => a.Id == _accountId).TwoFactorEnabled);
        }

        [Fact]
        [BusinessRule("BR-SEC-003")]
        public async Task A_confirmed_enrollment_requires_the_code_at_the_next_login()
        {
            using var db = CreateContext();
            var service = CreateService(db);
            await service.SetTemporaryPasswordAsync(_accountId, "Correct1!Pass");
            var enrollment = await service.BeginTwoFactorEnrollmentAsync(_accountId);
            var validCode = TwoFactorTotp.ComputeCode(enrollment.SecretKey, _clock.UtcNow);
            await service.ConfirmTwoFactorEnrollmentAsync(_accountId, validCode);
            Assert.True(db.UserAccounts.Single(a => a.Id == _accountId).TwoFactorEnabled);

            var outcome = await service.AuthenticateAsync("t.ahmad", "Correct1!Pass");
            Assert.True(outcome.RequiresTwoFactor);
            Assert.Null(outcome.Session);

            await Assert.ThrowsAsync<InvalidTwoFactorCodeException>(
                () => service.CompleteTwoFactorAsync(_accountId, "000000"));

            var nextCode = TwoFactorTotp.ComputeCode(enrollment.SecretKey, _clock.UtcNow);
            var session = await service.CompleteTwoFactorAsync(_accountId, nextCode);
            Assert.NotNull(session);
            Assert.Contains(db.AuditEntries, e => e.Action == AuditAction.Login && e.EntityId == _accountId);
        }

        // --- BR-SEC-004: sessions ---------------------------------------------

        [Fact]
        [BusinessRule("BR-SEC-004")]
        public async Task An_idle_session_stops_validating_after_the_staff_timeout()
        {
            using var db = CreateContext();
            var service = CreateService(db);
            await service.SetTemporaryPasswordAsync(_accountId, "Correct1!Pass");
            var outcome = await service.AuthenticateAsync("t.ahmad", "Correct1!Pass");

            _clock.UtcNow = _clock.UtcNow.AddMinutes(31);

            var validated = await service.ValidateSessionAsync(outcome.Session!.SessionToken);
            Assert.Null(validated);
            Assert.NotNull(db.UserSessions.Single(s => s.SessionToken == outcome.Session.SessionToken).RevokedAtUtc);
        }

        [Fact]
        [BusinessRule("BR-SEC-004")]
        public async Task Active_use_within_the_idle_window_keeps_the_session_alive_but_not_past_the_absolute_ceiling()
        {
            using var db = CreateContext();
            var service = CreateService(db);
            await service.SetTemporaryPasswordAsync(_accountId, "Correct1!Pass");
            var outcome = await service.AuthenticateAsync("t.ahmad", "Correct1!Pass");
            var token = outcome.Session!.SessionToken;

            UserSession? validated = null;
            for (var i = 0; i < 40; i++)
            {
                _clock.UtcNow = _clock.UtcNow.AddMinutes(20);
                validated = await service.ValidateSessionAsync(token);
                if (validated == null)
                {
                    break;
                }
            }

            // 20-minute steps never trip the 30-minute idle timeout on their own;
            // only the 12h (720min) absolute ceiling should end the session.
            Assert.Null(validated);
        }

        [Fact]
        [BusinessRule("BR-SEC-004")]
        public async Task A_role_enforcing_single_session_revokes_the_prior_session_on_a_new_login()
        {
            using var db = CreateContext();
            var role = new Role { Code = "FIN", Name = new LocalizedName("مالية", "Finance"), EnforceSingleSession = true };
            db.Roles.Add(role);
            db.SaveChanges();
            db.RoleAssignments.Add(new RoleAssignment { UserAccountId = _accountId, RoleId = role.Id });
            db.SaveChanges();

            var service = CreateService(db);
            await service.SetTemporaryPasswordAsync(_accountId, "Correct1!Pass");
            var first = await service.AuthenticateAsync("t.ahmad", "Correct1!Pass");
            var second = await service.AuthenticateAsync("t.ahmad", "Correct1!Pass");

            Assert.Null(await service.ValidateSessionAsync(first.Session!.SessionToken));
            Assert.NotNull(await service.ValidateSessionAsync(second.Session!.SessionToken));
        }

        [Fact]
        [BusinessRule("BR-SEC-004")]
        public async Task Logout_revokes_the_session_and_is_audited()
        {
            using var db = CreateContext();
            var service = CreateService(db);
            await service.SetTemporaryPasswordAsync(_accountId, "Correct1!Pass");
            var outcome = await service.AuthenticateAsync("t.ahmad", "Correct1!Pass");

            await service.LogoutAsync(outcome.Session!.SessionToken);

            Assert.Null(await service.ValidateSessionAsync(outcome.Session.SessionToken));
            Assert.Contains(db.AuditEntries, e => e.Action == AuditAction.Logout && e.EntityId == _accountId);
        }
    }
}
