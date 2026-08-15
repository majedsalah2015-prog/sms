using System;
using System.Collections.Generic;
using Sms.Application.Security;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>Bad username or password. Never distinguishes which, so login can't be used to enumerate accounts.</summary>
    public class InvalidCredentialsException : InvalidOperationException
    {
        public InvalidCredentialsException()
            : base("Invalid username or password.")
        {
        }
    }

    /// <summary>BR-SEC-002: the account is within its timed lockout window.</summary>
    public class AccountLockedOutException : InvalidOperationException
    {
        public AccountLockedOutException(DateTime unlocksAtUtc)
            : base($"Account is locked out until {unlocksAtUtc:O} (BR-SEC-002).")
        {
            UnlocksAtUtc = unlocksAtUtc;
        }

        public DateTime UnlocksAtUtc { get; }
    }

    /// <summary>BR-SEC-003: the TOTP code did not validate against the confirmed enrollment.</summary>
    public class InvalidTwoFactorCodeException : InvalidOperationException
    {
        public InvalidTwoFactorCodeException()
            : base("Invalid two-factor authentication code (BR-SEC-003).")
        {
        }
    }

    /// <summary>BR-SEC-001: the candidate password fails the policy or reuses a recent one.</summary>
    public class PasswordPolicyViolationException : InvalidOperationException
    {
        public PasswordPolicyViolationException(IReadOnlyList<PasswordPolicyViolation> violations)
            : base($"Password does not meet policy (BR-SEC-001): {string.Join(", ", violations)}.")
        {
            Violations = violations;
        }

        public IReadOnlyList<PasswordPolicyViolation> Violations { get; }
    }
}
