using Microsoft.AspNetCore.DataProtection;
using Sms.Application.Common.Interfaces;

namespace Sms.Infrastructure.Common
{
    /// <summary>
    /// <see cref="ISecretProtector"/> over the host's data-protection key ring
    /// (BR-NTF-003).
    /// <para>
    /// The purpose string pins these ciphertexts to this use: a protected gateway token
    /// cannot be handed to any other consumer of the same key ring and read back, which
    /// is what stops a value from one column being replayed into another.
    /// </para>
    /// <para>
    /// <b>Where the keys live matters.</b> By default ASP.NET Core writes the ring to the
    /// application's own folder, which means a token protected on one machine is
    /// unreadable on another and unreadable again after a redeploy that does not carry
    /// the folder. That is a deployment decision, not this class's — and it is why
    /// <see cref="Unprotect"/> answers null rather than throwing: a school whose key ring
    /// moved should be asked to re-enter its token, not shown a stack trace on a screen
    /// it cannot fix.
    /// </para>
    /// </summary>
    public class DataProtectionSecretProtector : ISecretProtector
    {
        /// <summary>Versioned so a future change of scheme can be told apart from this one rather than silently failing to open old rows.</summary>
        private const string Purpose = "Sms.Notifications.ProviderCredentials.v1";

        private readonly IDataProtector _protector;

        public DataProtectionSecretProtector(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector(Purpose);
        }

        public string Protect(string plaintext) => _protector.Protect(plaintext);

        public string? Unprotect(string? cipher)
        {
            if (string.IsNullOrWhiteSpace(cipher))
            {
                return null;
            }

            try
            {
                return _protector.Unprotect(cipher);
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                // The key that sealed this is gone — rotated out, or belonged to another
                // machine. Nothing here can recover it and nothing should pretend to.
                return null;
            }
        }
    }
}
