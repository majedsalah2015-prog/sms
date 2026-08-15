using Microsoft.AspNetCore.Identity;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Security;

namespace Sms.Infrastructure.Security
{
    /// <summary>
    /// BR-SEC-001 adaptive hash: delegates to ASP.NET Core Identity's
    /// PBKDF2-HMAC-SHA256 implementation (doc 02 §3) rather than rolling our
    /// own. UserAccount is only a type parameter here — this bypasses the
    /// rest of the Identity framework (stores/managers), which the sec.*
    /// schema already replaces.
    /// </summary>
    public class PasswordHasher : IPasswordHasher
    {
        private readonly Microsoft.AspNetCore.Identity.PasswordHasher<UserAccount> _inner = new();

        public string Hash(string password) => _inner.HashPassword(default!, password);

        public bool Verify(string hash, string password)
            => _inner.VerifyHashedPassword(default!, hash, password) != PasswordVerificationResult.Failed;
    }
}
